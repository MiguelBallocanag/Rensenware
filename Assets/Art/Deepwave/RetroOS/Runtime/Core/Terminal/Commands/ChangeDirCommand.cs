using Deepwave.RetroOS.Core.FileSystem;

namespace Deepwave.RetroOS.Core.Terminal.Commands
{
    // Moves the working directory.
    public class ChangeDirCommand : ITerminalCommand
    {
        public string Name => "cd";
        public string Help => "Change directory.";

        public void Execute(string[] args, ShellContext ctx)
        {
            if (args.Length == 0) { ctx.Cwd = ctx.Fs.Root; return; }

            if (ctx.Fs.Resolve(args[0], ctx.Cwd) is not VfsDirectory dir)
            {
                ctx.Out.WriteLine("No such directory.");
                return;
            }
            if (dir.Locked) { ctx.Out.WriteLine("Access denied (encrypted)."); return; }
            ctx.Cwd = dir;
        }
    }
}
