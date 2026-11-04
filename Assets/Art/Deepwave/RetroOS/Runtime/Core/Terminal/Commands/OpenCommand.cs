using Deepwave.RetroOS.Core.FileSystem;

namespace Deepwave.RetroOS.Core.Terminal.Commands
{
    // Opens a file in its matching viewer app.
    public class OpenCommand : ITerminalCommand
    {
        public string Name => "open";
        public string Help => "Open a file in its viewer.";

        public void Execute(string[] args, ShellContext ctx)
        {
            if (args.Length == 0) { ctx.Out.WriteLine("Usage: open <file>"); return; }

            if (ctx.Fs.Resolve(args[0], ctx.Cwd) is not VfsFile file)
            {
                ctx.Out.WriteLine("No such file.");
                return;
            }
            if (file.Locked) { ctx.Out.WriteLine("File is encrypted."); return; }

            ctx.OpenFile?.Invoke(file);
        }
    }
}
