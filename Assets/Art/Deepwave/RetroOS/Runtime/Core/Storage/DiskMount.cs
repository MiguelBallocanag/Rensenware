using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Deepwave.RetroOS.Core.FileSystem;

namespace Deepwave.RetroOS.Core.Storage
{
    // Turns real folders on disk into the OS's trees and drives. Names, structure
    // and file types come from the file system; hidden/encrypted/letter/label
    // come from a `.rmeta` sidecar. Contents load only when a file is opened.
    public static class DiskMount
    {
        static readonly HashSet<string> SkipExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".meta",                 // Unity's
            RetroMeta.Extension,     // ours
            ".ds_store"
        };

        // --- drives ---

        // Every drive of one machine, from the top-level folders of its folder.
        public static List<RetroDrive> MountDrives(string machineFolder,
            IReadOnlyList<string> accounts = null)
        {
            var drives = new List<RetroDrive>();
            if (string.IsNullOrEmpty(machineFolder) || !Directory.Exists(machineFolder))
                return drives;

            // The data root can be a share that dropped or is unreadable: that
            // is a machine with no drives, not a boot that throws.
            string[] folders;
            try { folders = Directory.GetDirectories(machineFolder); }
            catch (Exception e)
            {
                Debug.LogWarning($"RetroOS: could not read '{machineFolder}'. {e.Message}");
                return drives;
            }

            foreach (var path in folders.OrderBy(p => p))
            {
                var name = Path.GetFileName(path);
                if (name.EndsWith("~")) continue;
                drives.Add(MountDrive(path, name, drives, accounts));
            }

            return drives;
        }

        public static RetroDrive MountDrive(string path, string folderName,
            IReadOnlyCollection<RetroDrive> taken = null, IReadOnlyList<string> accounts = null)
        {
            var meta = RetroMeta.Read(path);

            var letter = FirstLetter(meta.Drive) ?? FirstLetter(folderName) ?? NextFreeLetter(taken);
            var kind = ParseKind(meta.Kind) ?? InferKind(letter);
            var label = meta.Label ?? "";

            var drive = new RetroDrive
            {
                Letter = letter,
                Kind = kind,
                Label = label,
                Path = path,
                Accounts = accounts
            };

            Remount(drive);
            return drive;
        }

        // Re-reads a drive's folder, e.g. after a disc swap or a rule reveal.
        public static void Remount(RetroDrive drive)
        {
            if (drive == null) return;

            var hasFolder = !string.IsNullOrEmpty(drive.Path) && Directory.Exists(drive.Path);
            drive.Root = hasFolder
                ? Mount(drive.Path, drive.NodeName, NodeRole.Drive, null, drive.Accounts)
                : new VfsDirectory(drive.NodeName) { Role = NodeRole.Drive };

            // A floppy or CD with an empty folder is a drive with no disk in it.
            drive.Ready = !drive.Removable || (hasFolder && drive.Root.Children.Count > 0);
        }

        static char? FirstLetter(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var c = char.ToUpperInvariant(value.Trim()[0]);
            return c is >= 'A' and <= 'Z' ? c : null;
        }

        static char NextFreeLetter(IReadOnlyCollection<RetroDrive> taken)
        {
            for (var c = 'C'; c <= 'Z'; c++)
                if (taken == null || taken.All(d => d.Letter != c)) return c;
            return 'Z';
        }

        static DriveKind? ParseKind(string value) => value?.Trim().ToLowerInvariant() switch
        {
            "fixed" or "hdd" or "harddisk" => DriveKind.Fixed,
            "floppy" or "diskette" => DriveKind.Floppy,
            "cd" or "cdrom" or "dvd" => DriveKind.CdRom,
            "removable" or "zip" => DriveKind.Removable,
            "network" or "share" => DriveKind.Network,
            _ => null
        };

        // The era's convention: A and B are the floppy drives, C is the disk.
        static DriveKind InferKind(char letter) => letter is 'A' or 'B'
            ? DriveKind.Floppy
            : DriveKind.Fixed;

        // --- trees ---

        /// <summary>A folder on disk as a lazy browsable tree - only the top level is read now.</summary>
        /// <param name="accounts">The owning machine's accounts; a home is recognised by matching one, not only by a <c>Users</c> parent.</param>
        public static VfsDirectory Mount(string path, string name, NodeRole role = NodeRole.Normal,
            string owner = null, IReadOnlyList<string> accounts = null)
        {
            var dir = new VfsDirectory(name) { Role = role, Owner = owner };
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return dir;
            dir.SourcePath = path.Replace('\\', '/');
            dir.FillWith(d => Fill(d, path, accounts));
            return dir;
        }

        static void Fill(VfsDirectory dir, string path, IReadOnlyList<string> accounts = null)
        {
            try
            {
                foreach (var childPath in Directory.GetDirectories(path).OrderBy(p => p))
                {
                    var childName = Path.GetFileName(childPath);
                    if (childName.EndsWith("~")) continue;      // Unity ignores these too

                    var child = dir.Add(new VfsDirectory(childName));
                    child.SourcePath = childPath.Replace('\\', '/');

                    // Role and owner from position + accounts: a home's contents are all its owner's.
                    child.Role = NodeRoles.Of(dir.Role, childName, accounts);
                    child.Owner = NodeRoles.OwnerOf(child.Role, childName, dir.Owner);

                    Apply(child, RetroMeta.Read(childPath));

                    // Lazy: a deep disk would otherwise be walked in full on every remount.
                    var here = childPath;
                    child.FillWith(d => Fill(d, here, accounts));
                }

                foreach (var filePath in Directory.GetFiles(path).OrderBy(p => p))
                {
                    var fileName = Path.GetFileName(filePath);
                    if (SkipExtensions.Contains(Path.GetExtension(fileName))) continue;

                    var file = dir.Add(BuildFile(filePath, fileName));
                    file.Owner = dir.Owner;
                    Apply(file, RetroMeta.Read(filePath));
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"RetroOS: could not read '{path}'. {e.Message}");
            }
        }

        static void Apply(VfsNode node, RetroMeta meta)
        {
            node.HiddenByDefault = meta.Hidden;
            node.Encrypted = meta.Encrypted;
            node.Password = meta.Password ?? "";
            node.Note = meta.Note;
            if (!string.IsNullOrEmpty(meta.Label)) node.DisplayName = meta.Label;

            // `owner: everyone` in a sidecar clears the owner.
            if (!string.IsNullOrEmpty(meta.Owner))
                node.Owner = meta.Owner.Equals("everyone", StringComparison.OrdinalIgnoreCase)
                    ? null : meta.Owner;
        }

        static VfsFile BuildFile(string filePath, string fileName)
        {
            var ext = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
            var kind = KindOf(ext);

            var info = new FileInfo(filePath);
            var file = new VfsFile(fileName, kind)
            {
                SourcePath = filePath.Replace('\\', '/'),
                Extension = ext,
                Size = info.Exists ? info.Length : 0,
                Modified = info.Exists ? info.LastWriteTime : DateTime.MinValue
            };

            if (kind is FileKind.Text or FileKind.Web)
                file.ContentLoader = f => SafeReadText(f.SourcePath);
            else
                file.AssetLoader = f => MediaLoader.Load(f.SourcePath, f.Kind);

            return file;
        }

        public static FileKind KindOf(string extension) => extension switch
        {
            "txt" or "log" or "md" or "ini" or "cfg" or "nfo" or "diz" or "bat" or "json" or "csv" or "lrc"
                => FileKind.Text,
            "htm" or "html" => FileKind.Web,
            "png" or "jpg" or "jpeg" or "bmp" or "gif" or "tga" => FileKind.Image,
            "wav" or "ogg" or "mp3" or "aif" or "aiff" or "mod" or "mid" => FileKind.Audio,
            "mp4" or "webm" or "avi" or "mov" or "mpg" => FileKind.Video,
            "exe" or "com" => FileKind.Executable,
            "" => FileKind.Unknown,
            _ => FileKind.Binary
        };

        public static string SafeReadText(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path) : null; }
            catch (Exception e)
            {
                Debug.LogWarning($"RetroOS: could not read '{path}'. {e.Message}");
                return null;
            }
        }

        /// <summary>Writes a file inside the machine's data folder. The containment check is the point; it also creates missing folders.</summary>
        public static bool SafeWriteText(string path, string text)
        {
            if (!RetroStorage.IsInside(RetroStorage.Root, path))
            {
                Debug.LogWarning($"RetroOS: refused to write '{path}' - it is outside " +
                                 $"the data folder ({RetroStorage.Root}).");
                return false;
            }

            // Story-authored half: a folder marked read-only in its sidecar.
            // DiskOperations guards Explorer; this guards Notepad, the printer and Save As.
            if (DiskOperations.IsProtected(path, out var why))
            {
                Debug.Log($"RetroOS: refused to write '{path}' - {why}");
                return false;
            }

            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, text ?? "");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"RetroOS: could not write '{path}'. {e.Message}");
                return false;
            }
        }
    }
}
