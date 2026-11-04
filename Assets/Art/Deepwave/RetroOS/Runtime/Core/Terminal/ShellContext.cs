using System;
using Deepwave.RetroOS.Core.FileSystem;

namespace Deepwave.RetroOS.Core.Terminal
{
    // State shared by every command.
    public class ShellContext
    {
        public VirtualFileSystem Fs { get; }
        public ITerminalOutput Out { get; }
        public VfsDirectory Cwd { get; set; }

        // Signed-in account, so `ls` cannot list another user's documents.
        public string Viewer { get; set; }
        public Action<VfsFile> OpenFile { get; set; }

        // Optional hook into the OS event bus: (eventId, argument).
        public Action<string, string> Raise { get; set; }

        // Typed file-system change (path, kind). Separate from Raise, whose untyped payload listeners tend to ignore.
        public Action<string, Events.FsChange> FsChanged { get; set; }

        public ShellContext(VirtualFileSystem fs, ITerminalOutput output)
        {
            Fs = fs;
            Out = output;
            Cwd = fs.Root;
        }
    }
}
