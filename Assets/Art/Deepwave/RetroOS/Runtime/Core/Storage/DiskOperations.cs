using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Deepwave.RetroOS.Core.Storage
{
    /// <summary>What a file operation answers with. The message is for the player, not the log.</summary>
    public readonly struct DiskResult
    {
        public bool Ok { get; }
        public string Message { get; }

        /// <summary>Where the thing ended up, when the operation made one.</summary>
        public string Path { get; }

        DiskResult(bool ok, string message, string path)
        {
            Ok = ok;
            Message = message;
            Path = path;
        }

        public static DiskResult Success(string path = null) => new(true, null, path);
        public static DiskResult Fail(string message) => new(false, message, null);
    }

    /// <summary>
    /// Create/rename/copy/delete on the machine's real disk. Every path is
    /// checked to be inside the OS data root; nothing here touches the
    /// in-memory tree, so callers refresh the drive bay afterwards.
    /// </summary>
    public static class DiskOperations
    {
        /// <summary>Makes a folder inside another.</summary>
        public static DiskResult CreateFolder(string parentPath, string name)
        {
            var target = Combine(parentPath, name, out var error);
            if (target == null) return DiskResult.Fail(error);

            var blocked = ProtectedResult(parentPath);
            if (!blocked.Ok) return blocked;

            if (Directory.Exists(target) || File.Exists(target))
                return DiskResult.Fail($"'{name}' already exists here.");

            try
            {
                Directory.CreateDirectory(target);
                return DiskResult.Success(target);
            }
            catch (Exception e)
            {
                return Refuse("create", name, e);
            }
        }

        /// <summary>Makes an empty file inside a folder.</summary>
        public static DiskResult CreateFile(string parentPath, string name, string content = "")
        {
            var target = Combine(parentPath, name, out var error);
            if (target == null) return DiskResult.Fail(error);

            var blocked = ProtectedResult(parentPath);
            if (!blocked.Ok) return blocked;

            if (File.Exists(target) || Directory.Exists(target))
                return DiskResult.Fail($"'{name}' already exists here.");

            try
            {
                File.WriteAllText(target, content ?? "");
                return DiskResult.Success(target);
            }
            catch (Exception e)
            {
                return Refuse("create", name, e);
            }
        }

        /// <summary>Renames a file or folder in place. The sidecar follows it.</summary>
        public static DiskResult Rename(string path, string newName)
        {
            if (!Inside(path)) return DiskResult.Fail("That item is not on this machine.");

            var parent = Path.GetDirectoryName(path);
            var target = Combine(parent, newName, out var error);
            if (target == null) return DiskResult.Fail(error);

            var blocked = ProtectedResult(path);
            if (!blocked.Ok) return blocked;

            if (string.Equals(path, target, StringComparison.OrdinalIgnoreCase))
                return DiskResult.Success(path);

            if (File.Exists(target) || Directory.Exists(target))
                return DiskResult.Fail($"'{newName}' already exists here.");

            try
            {
                if (Directory.Exists(path)) Directory.Move(path, target);
                else if (File.Exists(path)) File.Move(path, target);
                else return DiskResult.Fail("That item is not there any more.");

                MoveMeta(path, target);
                return DiskResult.Success(target);
            }
            catch (Exception e)
            {
                return Refuse("rename", Path.GetFileName(path), e);
            }
        }

        /// <summary>Deletes a file, or a folder and its contents. No recycle bin - callers ask first.</summary>
        public static DiskResult Delete(string path)
        {
            if (!Inside(path)) return DiskResult.Fail("That item is not on this machine.");

            // Refuse to delete a whole drive or the data root.
            var full = Path.GetFullPath(path).TrimEnd('/', '\\');
            if (IsRootish(full)) return DiskResult.Fail("That cannot be deleted.");

            var blocked = ProtectedResult(path);
            if (!blocked.Ok) return blocked;

            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
                else if (File.Exists(path)) File.Delete(path);
                else return DiskResult.Fail("That item is not there any more.");

                DeleteMeta(path);
                return DiskResult.Success();
            }
            catch (Exception e)
            {
                return Refuse("delete", Path.GetFileName(path), e);
            }
        }

        /// <summary>Copies a file or folder into another folder.</summary>
        public static DiskResult Copy(string path, string intoFolder) =>
            Transfer(path, intoFolder, move: false);

        /// <summary>Moves a file or folder into another folder.</summary>
        public static DiskResult Move(string path, string intoFolder) =>
            Transfer(path, intoFolder, move: true);

        static DiskResult Transfer(string path, string intoFolder, bool move)
        {
            if (!Inside(path)) return DiskResult.Fail("That item is not on this machine.");
            if (!Inside(intoFolder) || !Directory.Exists(intoFolder))
                return DiskResult.Fail("Files cannot be put there.");

            var name = Path.GetFileName(path.TrimEnd('/', '\\'));
            var target = Combine(intoFolder, name, out var error);
            if (target == null) return DiskResult.Fail(error);

            // Both ends: writing into the destination, and (for a move) removing the source.
            var blocked = ProtectedResult(intoFolder);
            if (!blocked.Ok) return blocked;
            if (move)
            {
                blocked = ProtectedResult(path);
                if (!blocked.Ok) return blocked;
            }

            // No copy-onto-self, no move-onto-self.
            if (string.Equals(Path.GetFullPath(path), Path.GetFullPath(target),
                    StringComparison.OrdinalIgnoreCase))
                return DiskResult.Fail($"'{name}' is already here.");

            if (Directory.Exists(path) && RetroStorage.IsInside(path, intoFolder))
                return DiskResult.Fail($"'{name}' cannot be put inside itself.");

            target = FreeName(intoFolder, name);

            try
            {
                if (Directory.Exists(path))
                {
                    if (move) Directory.Move(path, target);
                    else CopyTree(path, target);
                }
                else if (File.Exists(path))
                {
                    if (move) File.Move(path, target);
                    else File.Copy(path, target);
                }
                else
                {
                    return DiskResult.Fail("That item is not there any more.");
                }

                if (move) MoveMeta(path, target);
                else CopyMeta(path, target);

                return DiskResult.Success(target);
            }
            catch (Exception e)
            {
                return Refuse(move ? "move" : "copy", name, e);
            }
        }

        // A clash becomes "Copy of report.txt", then "Copy (2) of report.txt".
        static string FreeName(string folder, string name)
        {
            var candidate = Path.Combine(folder, name);
            if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;

            for (var n = 1; n < 100; n++)
            {
                var prefix = n == 1 ? "Copy of " : $"Copy ({n}) of ";
                candidate = Path.Combine(folder, prefix + name);
                if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
            }

            return Path.Combine(folder, Guid.NewGuid().ToString("N")[..8] + " " + name);
        }

        static void CopyTree(string from, string to)
        {
            Directory.CreateDirectory(to);

            foreach (var file in Directory.GetFiles(from))
                File.Copy(file, Path.Combine(to, Path.GetFileName(file)), true);

            foreach (var dir in Directory.GetDirectories(from))
                CopyTree(dir, Path.Combine(to, Path.GetFileName(dir)));
        }

        // --- sidecars ---
        // A node's .rmeta lives beside it, so every operation carries it along.

        static string MetaOf(string path) => path + RetroMeta.Extension;

        static void MoveMeta(string from, string to)
        {
            var source = MetaOf(from);
            if (!File.Exists(source)) return;
            try { File.Move(source, MetaOf(to)); }
            catch (Exception e) { WarnMeta("move", source, e); }
        }

        static void CopyMeta(string from, string to)
        {
            var source = MetaOf(from);
            if (!File.Exists(source)) return;
            try { File.Copy(source, MetaOf(to), true); }
            catch (Exception e) { WarnMeta("copy", source, e); }
        }

        static void DeleteMeta(string path)
        {
            var source = MetaOf(path);
            if (!File.Exists(source)) return;
            try { File.Delete(source); }
            catch (Exception e) { WarnMeta("delete", source, e); }
        }

        // The node already moved, so this is a developer warning, not a player dialog:
        // a stranded sidecar means a file that lost its password, owner or hidden flag.
        static void WarnMeta(string operation, string sidecar, Exception e) =>
            Debug.LogWarning($"RetroOS: could not {operation} '{sidecar}'. The file itself is " +
                             $"fine, but its RetroOS properties did not follow it. {e.Message}");

        /// <summary>Whether a string is a valid file name here - the same rule the write operations enforce, for a dialog to check up front.</summary>
        public static bool IsPlainName(string name, out string error) =>
            Validate(name, out error) != null;

        /// <summary>DOS device names Windows still reserves (any extension, any case). A folder called CON lists but cannot be opened.</summary>
        static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        /// <summary>The one name rule every write goes through: the cleaned name, or <c>null</c> with a player-facing message.</summary>
        static string Validate(string name, out string error)
        {
            error = null;
            name = (name ?? "").Trim();

            if (name.Length == 0) { error = "Type a name."; return null; }

            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                name.Contains('/') || name.Contains('\\') ||
                name == "." || name == "..")
            {
                error = $"'{name}' is not a name a file can have.";
                return null;
            }

            // Windows silently drops a trailing dot/space, so the name on disk would differ.
            if (name.EndsWith(".") || name.EndsWith(" "))
            {
                error = "A name cannot end with a dot or a space.";
                return null;
            }

            var stem = name;
            var dot = stem.IndexOf('.');
            if (dot > 0) stem = stem[..dot];
            if (ReservedNames.Contains(stem))
            {
                error = $"'{stem}' is a name the system keeps for itself.";
                return null;
            }

            return name;
        }

        // --- guards ---

        // Builds a child path, refusing anything that is not a plain name.
        static string Combine(string parent, string name, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(parent) || !Directory.Exists(parent))
            {
                error = "Files cannot be put there.";
                return null;
            }

            name = Validate(name, out error);
            if (name == null) return null;

            var target = Path.Combine(parent, name);
            if (!Inside(target)) { error = "That is outside this machine."; return null; }
            return target;
        }

        static bool Inside(string path) =>
            !string.IsNullOrWhiteSpace(path) && RetroStorage.IsInside(RetroStorage.Root, path);

        // --- read-only ---

        /// <summary>Whether a file or folder refuses writes, and the line to tell the player. Read-only is inherited from any parent folder.</summary>
        public static bool IsProtected(string path, out string why)
        {
            why = null;
            if (string.IsNullOrWhiteSpace(path)) return false;

            var full = SafeFullPath(path);
            if (full == null) return false;

            // Walk up to the data root: a read-only folder protects its contents.
            var root = SafeFullPath(RetroStorage.Root);
            for (var at = full; at != null; at = Path.GetDirectoryName(at))
            {
                var meta = RetroMeta.Read(at);

                if (meta.ReadOnly)
                {
                    why = string.Equals(at, full, StringComparison.OrdinalIgnoreCase)
                        ? $"'{Path.GetFileName(full)}' is read-only."
                        : $"'{Path.GetFileName(at)}' is read-only.";
                    return true;
                }

                // A disc is read-only by nature. Its drive sidecar is on the way up
                // and says the medium, so the answer is here, not in a drive bay this class cannot see.
                if (IsDisc(meta.Kind))
                {
                    why = $"'{Path.GetFileName(at)}' is a disc, and cannot be written to.";
                    return true;
                }

                if (root != null && string.Equals(at, root, StringComparison.OrdinalIgnoreCase))
                    break;
            }

            return false;
        }

        static bool IsDisc(string kind) => kind?.Trim().ToLowerInvariant() switch
        {
            "cd" or "cdrom" or "dvd" => true,
            _ => false
        };

        // null = a path this machine cannot reason about; callers treat that as protected.
        static string SafeFullPath(string path)
        {
            try { return Path.GetFullPath(path).TrimEnd('/', Convert.ToChar(92)); }
            catch (Exception e)
            {
                Debug.LogWarning($"RetroOS: '{path}' is not a usable path, so it is being " +
                                 $"treated as off limits. {e.Message}");
                return null;
            }
        }

        // The write guard, as the refusal it becomes.
        static DiskResult ProtectedResult(string path)
        {
            return IsProtected(path, out var why) ? DiskResult.Fail(why) : DiskResult.Success();
        }

        // A drive folder and everything above it are not the player's to delete.
        static bool IsRootish(string full)
        {
            var root = Path.GetFullPath(RetroStorage.Root).TrimEnd('/', '\\');
            if (string.Equals(full, root, StringComparison.OrdinalIgnoreCase)) return true;

            var parent = Path.GetDirectoryName(full);
            if (parent == null) return true;

            var grandparent = Path.GetDirectoryName(parent);
            var machines = Path.GetFullPath(RetroStorage.Machines).TrimEnd('/', '\\');

            // <root>/machines/<machine>            - the machine itself
            if (string.Equals(parent.TrimEnd('/', '\\'), machines, StringComparison.OrdinalIgnoreCase))
                return true;

            // <root>/machines/<machine>/<DRIVE>    - a whole drive
            return grandparent != null &&
                   string.Equals(grandparent.TrimEnd('/', '\\'), machines,
                       StringComparison.OrdinalIgnoreCase);
        }

        static DiskResult Refuse(string verb, string name, Exception e)
        {
            Debug.LogWarning($"RetroOS: could not {verb} '{name}'. {e.Message}");

            // The player is told what happened, not what the exception said.
            return DiskResult.Fail(e switch
            {
                UnauthorizedAccessException => $"'{name}' is write-protected.",
                IOException => $"'{name}' is in use.",
                _ => $"'{name}' could not be {verb}d."
            });
        }
    }
}
