using System;

namespace Deepwave.RetroOS.Core.FileSystem
{
    /// <summary>
    /// Resolves "/a/b", relative, "." and ".." paths, and refuses another
    /// user's files on the way down (a path does not go through a listing).
    /// </summary>
    public class VirtualFileSystem
    {
        public VfsDirectory Root { get; }

        /// <summary>Who is asking, set by the bootstrap. <c>null</c> (no accounts) sees everything.</summary>
        public Func<string> Viewer { get; set; }

        public VirtualFileSystem(VfsDirectory root) => Root = root;

        public VfsNode Resolve(string path, VfsDirectory from = null) =>
            Resolve(path, from, ignoreAccess: false);

        /// <summary>Resolves a path, optionally without the ownership rule.</summary>
        /// <param name="ignoreAccess"><c>true</c> only for the OS's own bookkeeping; never for something the player typed.</param>
        public VfsNode Resolve(string path, VfsDirectory from, bool ignoreAccess)
        {
            from ??= Root;
            if (string.IsNullOrWhiteSpace(path)) return from;

            var viewer = ignoreAccess ? null : Viewer?.Invoke();

            VfsNode current = path.StartsWith("/") ? Root : from;
            foreach (var part in path.Split('/'))
            {
                if (part is "" or ".") continue;
                if (current is not VfsDirectory dir) return null;
                current = part == ".." ? dir.Parent ?? dir : dir.Find(part);
                if (current == null) return null;

                // Refused on the way past so a full path cannot step around an owned folder.
                if (!current.IsVisibleTo(viewer)) return null;
            }
            return current;
        }
    }
}
