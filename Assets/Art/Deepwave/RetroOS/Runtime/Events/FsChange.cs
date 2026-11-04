using System;

namespace Deepwave.RetroOS.Events
{
    /// <summary>What happened to a node.</summary>
    public enum FsChange
    {
        /// <summary>A file's contents were written.</summary>
        Written,

        /// <summary>A file or folder appeared.</summary>
        Created,

        /// <summary>A file or folder went away.</summary>
        Deleted,

        /// <summary>A file or folder was renamed, moved or copied.</summary>
        Moved,

        /// <summary>A rule hid a node, or brought one back.</summary>
        Revealed,

        /// <summary>A password or a rule opened a sealed node.</summary>
        Unlocked,

        /// <summary>A disc went in, a share came up: a whole branch appeared.</summary>
        Mounted,

        /// <summary>A disc came out, a machine went down.</summary>
        Unmounted
    }

    /// <summary>What changed on the file system, and where.</summary>
    public readonly struct FsChangedArgs
    {
        /// <summary>The node's absolute path, e.g. <c>/C:/Docs/note.txt</c>.</summary>
        public readonly string Path;

        /// <summary>What happened to it.</summary>
        public readonly FsChange Kind;

        public FsChangedArgs(string path, FsChange kind)
        {
            // Empty is the root, not nothing - a listener handed "" must not decide it is unaffected and stop refreshing.
            Path = string.IsNullOrEmpty(path) ? "/" : path;
            Kind = kind;
        }

        /// <summary>Whether a view showing <paramref name="folder"/> has to redraw.</summary>
        public bool Touches(string folder)
        {
            if (string.IsNullOrEmpty(folder) || folder == "/") return true;
            if (Path == "/") return true;

            return Under(Path, folder) || Under(folder, Path);
        }

        // Whether `path` is `folder` or something inside it. The separator
        // test is what keeps /C:/Documents from matching /C:/Doc.
        static bool Under(string path, string folder)
        {
            if (!path.StartsWith(folder, StringComparison.OrdinalIgnoreCase)) return false;
            return path.Length == folder.Length || path[folder.Length] == '/';
        }

        public override string ToString() => Kind + " " + Path;
    }
}
