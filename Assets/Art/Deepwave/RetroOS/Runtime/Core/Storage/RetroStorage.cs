using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Deepwave.RetroOS.Core.Storage
{
    // Where the OS keeps the real files it shows: two folders you can open in Explorer.
    //
    //   <root>/machines/<machine>/C/   one folder per machine, one per drive inside it
    //   <root>/internet/<site>/        one folder per web site
    //
    // A LAN share is just a folder on another machine (`share: NAME` in its .rmeta).
    // In the editor <root> is StreamingAssets/<FolderName>; in a player it is the
    // user's Documents folder, seeded from StreamingAssets on first run.
    public static class RetroStorage
    {
        public const string MachinesFolder = "machines";
        public const string InternetFolder = "internet";

        // Folder name used under StreamingAssets and Documents.
        public static string FolderName
        {
            get => s_folderName;
            set { s_folderName = value; s_cachedRoot = null; }
        }

        // Set from the profile before boot to redirect the whole data set.
        public static string OverrideRoot
        {
            get => s_overrideRoot;
            set { s_overrideRoot = value; s_cachedRoot = null; }
        }

        // Documents subfolder used by built players ("<Documents>/<this>").
        public static string DocumentsFolder
        {
            get => s_documentsFolder;
            set { s_documentsFolder = value; s_cachedRoot = null; }
        }

        static string s_folderName = "RetroOS";
        static string s_overrideRoot;
        static string s_documentsFolder;

        // Resolving Root hits disk, so cache it. Every setter above clears it -
        // these statics outlive a play session when domain reload is off.
        static string s_cachedRoot;

        // The folder shipped with the project, always read-only in a player.
        public static string SourceRoot =>
            Path.Combine(Application.streamingAssetsPath, FolderName).Replace('\\', '/');

        /// <summary>Whether <see cref="SourceRoot"/> is a folder this platform can read with <c>System.IO</c>. False on WebGL, where StreamingAssets is a URL, and on Android, where it is an entry in the .apk - the two builds that used to come up with nothing on every drive.</summary>
        public static bool SourceFolderReadable
        {
            get
            {
                try { return Directory.Exists(SourceRoot); }
                catch (Exception) { return false; }
            }
        }

        // The folder the OS actually reads from.
        public static string Root
        {
            get
            {
                if (!string.IsNullOrEmpty(OverrideRoot)) return OverrideRoot.Replace('\\', '/');
                if (!string.IsNullOrEmpty(s_cachedRoot)) return s_cachedRoot;
                return s_cachedRoot = Application.isEditor ? SourceRoot : PlayerRoot();
            }
        }

        public static string Machines => Path.Combine(Root, MachinesFolder).Replace('\\', '/');
        public static string Internet => Path.Combine(Root, InternetFolder).Replace('\\', '/');

        public static string MachineFolder(string name) => Under(Machines, name);

        public static string SiteFolder(string domain) => Under(Internet, domain);

        // Turns a machine name or domain into a real path. Sanitised and then
        // bounds-checked; a name that escapes returns the parent, not the escape.
        static string Under(string parent, string name)
        {
            var safe = Sanitize(name);
            if (safe.Length == 0) return parent;

            var full = Path.Combine(parent, safe).Replace('\\', '/');
            return IsInside(parent, full) ? full : parent;
        }

        static string PlayerRoot()
        {
            // A Documents folder is a desktop idea. A phone has none and a browser
            // tab has none, and asking anyway answers "" or a path in a virtual file
            // system nothing survives - persistentDataPath is the one place those
            // platforms let a game keep files between sessions.
            if (!HasDocumentsFolder)
                return Path.Combine(Application.persistentDataPath,
                    string.IsNullOrWhiteSpace(DocumentsFolder) ? FolderName : DocumentsFolder)
                    .Replace('\\', '/');

            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrEmpty(documents)) documents = Application.persistentDataPath;

            var folder = string.IsNullOrWhiteSpace(DocumentsFolder)
                ? Path.Combine(Application.productName ?? FolderName, FolderName)
                : DocumentsFolder;
            return Path.Combine(documents, folder).Replace('\\', '/');
        }

        // Named platforms, not "everything except the ones I thought of": a
        // platform nobody has tried gets the folder that always exists.
        static bool HasDocumentsFolder =>
            Application.platform is RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor
                or RuntimePlatform.OSXPlayer or RuntimePlatform.OSXEditor
                or RuntimePlatform.LinuxPlayer or RuntimePlatform.LinuxEditor;

        // Ensures the two roots exist, copying the project's content across on
        // a player's first run. Safe to call every boot.
        public static void EnsureReady(bool seedFromStreamingAssets = true)
        {
            var root = Root;
            try
            {
                Directory.CreateDirectory(root);

                if (seedFromStreamingAssets && !Application.isEditor &&
                    !string.Equals(root, SourceRoot, StringComparison.OrdinalIgnoreCase))
                    Seed(root);

                Directory.CreateDirectory(Machines);
                Directory.CreateDirectory(Internet);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"RetroOS: could not prepare the data folder at '{root}'. {e.Message}");
            }
        }

        // The shipped content, by whichever route this platform has to it: the
        // folder where StreamingAssets is one, the baked pack where it is not.
        // Both copy only what the player lacks, so a patch cannot clobber edits.
        static void Seed(string root)
        {
            if (SourceFolderReadable)
            {
                CopyMissing(SourceRoot, root);
                return;
            }

            Unpack(root);
        }

        // WebGL and Android read the pack out of Resources. The stamp holds the
        // pack's own hash, so an unchanged one is not unpacked again on every boot
        // and a patched one is.
        static void Unpack(string root)
        {
            var stamp = Path.Combine(root, RetroContentPack.StampName).Replace('\\', '/');

            var pack = Resources.Load<TextAsset>(RetroContentPack.ResourceName);
            var bytes = pack ? pack.bytes : null;

            if (bytes == null || bytes.Length == 0)
            {
                if (pack) Resources.UnloadAsset(pack);
                Debug.LogError(
                    $"RetroOS: this is a {Application.platform} build, where StreamingAssets " +
                    "cannot be opened as a folder, and no baked content was found - so every " +
                    "drive, web site, picture and sound is missing. Bake it into the build " +
                    "with Deepwave > RetroOS > Bake Content For Web, then build again.");
                return;
            }

            var hash = RetroContentPack.HashIn(bytes);

            try
            {
                if (File.Exists(stamp) && File.ReadAllText(stamp).Trim() == hash)
                {
                    Resources.UnloadAsset(pack);
                    return;
                }
            }
            catch (Exception)
            {
                // An unreadable stamp means "unpack again", which is safe.
            }

            var written = RetroContentPack.Unpack(bytes, root);
            Resources.UnloadAsset(pack);

            if (written < 0) return;

            try { File.WriteAllText(stamp, hash); }
            catch (Exception e)
            {
                Debug.LogWarning($"RetroOS: could not write '{stamp}', so the baked content is " +
                                 $"read again on every boot. {e.Message}");
            }
        }

        // Copies only files the player lacks, so a content patch does not clobber edits.
        static void CopyMissing(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (var file in Directory.GetFiles(from))
            {
                if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;

                var target = Path.Combine(to, Path.GetFileName(file));
                if (File.Exists(target)) continue;

                // One file that will not copy is one missing file. Letting it throw
                // took the rest of the machine with it: the copy stopped where it
                // stood and the player got an OS with nothing on any drive.
                try
                {
                    File.Copy(file, target);

                    // The copy is the player's own disk and they save to it, so it
                    // must not inherit a read-only bit from an installed folder.
                    var attributes = File.GetAttributes(target);
                    if ((attributes & FileAttributes.ReadOnly) != 0)
                        File.SetAttributes(target, attributes & ~FileAttributes.ReadOnly);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"RetroOS: could not copy '{file}' into the data folder, " +
                                     $"so it is missing from this machine. {e.Message}");
                }
            }

            foreach (var dir in Directory.GetDirectories(from))
                CopyMissing(dir, Path.Combine(to, Path.GetFileName(dir)));
        }

        // Creates one machine with a 1995 PC's drives, so a fresh project has something to browse.
        public static string CreateMachine(string name, params string[] users)
        {
            EnsureReady(seedFromStreamingAssets: false);

            var machine = MachineFolder(name);
            var c = Path.Combine(machine, "C");
            Directory.CreateDirectory(Path.Combine(c, "Windows"));
            Directory.CreateDirectory(Path.Combine(c, "Program Files"));

            foreach (var user in users.Length > 0 ? users : new[] { "operator" })
            {
                var home = Path.Combine(c, "Users", Sanitize(user));
                Directory.CreateDirectory(Path.Combine(home, "Documents"));
                Directory.CreateDirectory(Path.Combine(home, "Desktop"));
                Directory.CreateDirectory(Path.Combine(home, "Pictures"));
            }

            // Never overwrite a sidecar somebody has already edited.
            Describe(c, "C", "fixed", "MAIN");

            var floppy = Path.Combine(machine, "A");
            Directory.CreateDirectory(floppy);
            Describe(floppy, "A", "floppy", null);

            var cd = Path.Combine(machine, "D");
            Directory.CreateDirectory(cd);
            Describe(cd, "D", "cdrom", null);

            return machine;
        }

        static void Describe(string folder, string letter, string kind, string label)
        {
            if (File.Exists(RetroMeta.SidecarFor(folder))) return;
            new RetroMeta { Drive = letter, Kind = kind, Label = label }.Write(folder);
        }

        // Every machine folder on disk, component or not.
        public static string[] MachineNames() => FolderNames(Machines);

        public static string[] SiteNames() => FolderNames(Internet);

        /// <summary>Sites a machine is told about: every folder under internet/ except those whose sidecar says <c>hidden</c> (still served by address).</summary>
        public static string[] ListedSiteNames() => SiteNames()
            .Where(n => !RetroMeta.Read(SiteFolder(n)).Hidden)
            .ToArray();

        // Subfolder names, minus Unity-hidden ones. An unreadable root gives an
        // empty list - the data folder may be a share that went away.
        static string[] FolderNames(string root)
        {
            try
            {
                if (!Directory.Exists(root)) return Array.Empty<string>();
                return Directory.GetDirectories(root)
                    .Select(Path.GetFileName)
                    .Where(n => !n.EndsWith("~"))
                    .OrderBy(n => n)
                    .ToArray();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"RetroOS: could not list '{root}'. {e.Message}");
                return Array.Empty<string>();
            }
        }

        // Machine and host names are folder names, so strip what a folder cannot hold.
        public static string Sanitize(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            name = name.Trim().ToLowerInvariant();
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');

            // Strip leading/trailing dots: a domain of ".." resolved SiteFolder
            // to the data root and served another machine's private disk.
            name = name.Trim('.', ' ');

            return name;
        }

        // Keeps a resolved path inside its folder, so "../../secrets" in a
        // page link or a share path cannot walk out of the sandbox.
        public static bool IsInside(string root, string candidate)
        {
            try
            {
                var a = Path.GetFullPath(root).TrimEnd('/', '\\') + Path.DirectorySeparatorChar;
                var b = Path.GetFullPath(candidate);
                return b.StartsWith(a, StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(Path.GetFullPath(root), b, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception e)
            {
                // Cannot resolve = refused. The safe answer to "I cannot tell" is no.
                Debug.LogWarning($"RetroOS: could not tell whether '{candidate}' is inside " +
                                 $"'{root}', so access to it was refused. {e.Message}");
                return false;
            }
        }
    }
}
