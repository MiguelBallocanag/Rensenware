using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Deepwave.RetroOS.Core.FileSystem;
using Deepwave.RetroOS.Events;

namespace Deepwave.RetroOS.Core.Storage
{
    // The machine's drives, and the root of everything the OS can browse.
    //
    // Each drive is a child of the file-system root named after its letter, so
    // a path reads the way it did in 1995: /C:/Users/operator/Documents.
    // Swapping a disc re-mounts one drive without disturbing the others.
    public class DriveBay
    {
        readonly List<RetroDrive> _drives = new();
        readonly VfsDirectory _root;
        readonly Action<string, string> _raise;

        // Set by the bootstrap; the bay does not know what a RetroOsContext is.
        Action<string, Events.FsChange> _fsChanged;

        // Non-drive branches (Network Neighborhood, My Documents) and how to
        // rebuild them. Keeping the builder lets Refresh() re-read them from disk.
        readonly Dictionary<string, Func<VfsDirectory>> _branches =
            new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<RetroDrive> Drives => _drives;
        public VfsDirectory Root => _root;

        /// <summary>What the story changed about this machine's files. On the bay, not the tree, so a <see cref="Refresh"/> remount keeps it.</summary>
        public NodeStateStore States { get; } = new();

        // Fired when a disc goes in or out, or a share appears.
        public event Action Changed;

        public DriveBay(Action<string, string> raise = null,
            Action<string, Events.FsChange> fsChanged = null)
        {
            _root = new VfsDirectory("") { States = States };
            _raise = raise;
            _fsChanged = fsChanged;
        }

        /// <summary>Where to report file-system changes, set once the OS exists.</summary>
        public void ReportTo(Action<string, Events.FsChange> fsChanged) => _fsChanged = fsChanged;

        public void Mount(IEnumerable<RetroDrive> drives)
        {
            foreach (var drive in drives) Add(drive);
            Changed?.Invoke();
        }

        public RetroDrive Add(RetroDrive drive)
        {
            if (drive == null) return null;

            // A second drive claiming the same letter takes the next free one.
            if (_drives.Any(d => d.Letter == drive.Letter))
                for (var c = 'C'; c <= 'Z'; c++)
                    if (_drives.All(d => d.Letter != c)) { drive.Letter = c; break; }

            _drives.Add(drive);
            _drives.Sort((a, b) => a.Letter.CompareTo(b.Letter));
            Attach(drive);
            return drive;
        }

        public RetroDrive Find(char letter) =>
            _drives.FirstOrDefault(d => d.Letter == char.ToUpperInvariant(letter));

        // The drive a real path sits on. Slashes are levelled first: drive Path
        // keeps the platform separator, mounted SourcePath is normalised.
        public RetroDrive FindByPath(string sourcePath)
        {
            var wanted = Normalize(sourcePath);
            if (wanted == null) return null;

            return _drives.FirstOrDefault(d =>
            {
                var folder = Normalize(d.Path);
                return folder != null &&
                       (string.Equals(folder, wanted, StringComparison.OrdinalIgnoreCase) ||
                        wanted.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase));
            });
        }

        static string Normalize(string path) => string.IsNullOrWhiteSpace(path)
            ? null
            : path.Replace('\\', '/').TrimEnd('/');

        // Puts a folder into a removable drive - a disc, a floppy, a memory card.
        public bool Insert(char letter, string folderPath, string label = null)
        {
            var drive = Find(letter);
            if (drive == null)
            {
                Debug.LogWarning($"RetroOS: nothing to put a disc into - this machine has no " +
                                 $"{letter}: drive. Add one to its drive list, or check the letter.");
                return false;
            }

            if (!drive.Removable)
            {
                Debug.LogWarning($"RetroOS: {letter}: is a fixed drive, so no disc went in. Mark " +
                                 "it removable to make it a floppy or a CD-ROM.");
                return false;
            }

            drive.Path = folderPath;
            if (label != null) drive.Label = label;
            DiskMount.Remount(drive);
            Attach(drive);

            _raise?.Invoke(OsEvents.DiscInserted, drive.Label ?? drive.NodeName);
            Report(drive.Root.Path, Events.FsChange.Mounted);
            Changed?.Invoke();
            return true;
        }

        public bool Eject(char letter)
        {
            var drive = Find(letter);
            if (drive == null || !drive.Removable)
            {
                Debug.LogWarning($"RetroOS: nothing to eject - this machine has no removable " +
                                 $"{letter}: drive.");
                return false;
            }

            // Not a warning: ejecting an empty drive is the ordinary case.
            if (!drive.Ready) return false;

            var label = drive.Label;
            drive.Path = null;
            drive.Label = "";
            DiskMount.Remount(drive);
            Attach(drive);

            _raise?.Invoke(OsEvents.DiscEjected, label ?? drive.NodeName);
            Report(drive.Root.Path, Events.FsChange.Unmounted);
            Changed?.Invoke();
            return true;
        }

        // Ejects whatever is in the first ready removable drive.
        public bool EjectAny()
        {
            var drive = _drives.FirstOrDefault(d => d.Removable && d.Ready);
            return drive != null && Eject(drive.Letter);
        }

        /// <summary>One user's home folder, and the drive it is on. Found by role, not by folder name.</summary>
        public VfsDirectory HomeOf(string user, out RetroDrive drive)
        {
            drive = null;
            if (string.IsNullOrEmpty(user)) return null;

            foreach (var candidate in _drives)
            {
                var home = FindByRole(candidate.Root, NodeRole.UserRoot, NodeRole.UserHome, user);
                if (home == null) continue;
                drive = candidate;
                return home;
            }
            return null;
        }

        /// <summary>One user's Documents folder, and the drive it is on.</summary>
        public VfsDirectory DocumentsOf(string user, out RetroDrive drive)
        {
            var home = HomeOf(user, out drive);
            if (home == null) return null;

            foreach (var child in home.Children)
                if (child is VfsDirectory dir && dir.Role == NodeRole.Documents) return dir;

            return null;
        }

        /// <summary>One user's home folder.</summary>
        public VfsDirectory HomeOf(string user) => HomeOf(user, out _);

        /// <summary>One user's Documents folder.</summary>
        public VfsDirectory DocumentsOf(string user) => DocumentsOf(user, out _);

        // The `wanted` child named `name` inside a `container` folder.
        static VfsDirectory FindByRole(VfsDirectory root, NodeRole container, NodeRole wanted,
            string name)
        {
            foreach (var child in root.Children)
            {
                if (child is not VfsDirectory holder || holder.Role != container) continue;

                foreach (var entry in holder.Children)
                    if (entry is VfsDirectory dir && dir.Role == wanted &&
                        string.Equals(dir.Name, name, StringComparison.OrdinalIgnoreCase))
                        return dir;
            }
            return null;
        }

        /// <summary>Re-reads only the drive a real path sits on. Falls back to <see cref="Refresh"/> for paths not on a local drive.</summary>
        public void RefreshAt(string sourcePath)
        {
            var drive = FindByPath(sourcePath);
            if (drive == null) { Refresh(); return; }

            DiskMount.Remount(drive);
            Attach(drive);
            Changed?.Invoke();
        }

        // Re-reads every drive. Cheap enough to call after any file-system change.
        public void Refresh()
        {
            foreach (var drive in _drives)
            {
                DiskMount.Remount(drive);
                Attach(drive);
            }

            foreach (var name in _branches.Keys.ToList()) Rebuild(name);

            Changed?.Invoke();
        }

        /// <summary>Adds a non-drive branch and how to rebuild it; the builder returns <c>null</c> to hide it.</summary>
        public VfsDirectory AddBranch(string name, Func<VfsDirectory> build)
        {
            if (string.IsNullOrEmpty(name) || build == null) return null;
            _branches[name] = build;
            return Rebuild(name);
        }

        // Adds a branch built once, for callers that hold the tree themselves.
        public VfsDirectory AddBranch(VfsDirectory branch)
        {
            if (branch == null) return null;
            Attach(branch);
            Changed?.Invoke();
            return branch;
        }

        // Always (path, kind) so a listener can skip work by inspecting the payload.
        void Report(string path, Events.FsChange kind)
        {
            if (_fsChanged != null) _fsChanged(path, kind);
            else _raise?.Invoke(OsEvents.FsChanged, path);
        }

        /// <summary>Re-runs one branch's builder. Silent if there is no such branch.</summary>
        public VfsDirectory Rebuild(string name)
        {
            if (name == null || !_branches.TryGetValue(name, out var build)) return null;

            var branch = build();
            if (branch == null) { Detach(name); Changed?.Invoke(); return null; }

            Attach(branch);
            Changed?.Invoke();
            return branch;
        }

        // Takes a branch away for good: the tree and the builder behind it.
        public void RemoveBranch(string name)
        {
            if (name == null) return;
            _branches.Remove(name);
            if (!Detach(name)) return;
            Changed?.Invoke();
        }

        bool Detach(string name)
        {
            var existing = _root.Find(name);
            if (existing == null) return false;
            _root.Remove(existing);
            return true;
        }

        void Attach(VfsDirectory branch)
        {
            var existing = _root.Find(branch.Name);
            if (existing != null) _root.Remove(existing);
            _root.Add(branch);
        }

        // Swaps the drive's node under the root, keeping its position stable.
        void Attach(RetroDrive drive)
        {
            drive.Root.DisplayName = drive.DisplayName;
            Attach(drive.Root);
        }
    }
}
