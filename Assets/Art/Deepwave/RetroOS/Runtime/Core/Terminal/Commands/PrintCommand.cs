using Deepwave.RetroOS.Core.FileSystem;

namespace Deepwave.RetroOS.Core.Terminal.Commands
{
    // Prints a text file to the terminal.
    public class PrintCommand : ITerminalCommand
    {
        public string Name => "cat";
        public string Help => "Print a text file.";

        public void Execute(string[] args, ShellContext ctx)
        {
            if (args.Length == 0) { ctx.Out.WriteLine("Usage: cat <file>"); return; }

            if (ctx.Fs.Resolve(args[0], ctx.Cwd) is not VfsFile file)
            {
                ctx.Out.WriteLine("No such file.");
                return;
            }
            if (file.Locked) { ctx.Out.WriteLine("Encrypted. Use: decrypt <path> <password>"); return; }
            if (file.Kind != FileKind.Text) { ctx.Out.WriteLine("Not a text file."); return; }

            if (file.TryRead(ctx.Viewer, out var text)) ctx.Out.WriteLine(text);
            else ctx.Out.WriteLine("Access denied.");
        }
    }
}
