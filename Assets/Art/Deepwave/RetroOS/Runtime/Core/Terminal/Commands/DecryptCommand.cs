using Deepwave.RetroOS.Events;

namespace Deepwave.RetroOS.Core.Terminal.Commands
{
    // Unlocks an encrypted file or folder.
    public class DecryptCommand : ITerminalCommand
    {
        public string Name => "decrypt";
        public string Help => "Unlock a node: decrypt <path> <password>.";

        public void Execute(string[] args, ShellContext ctx)
        {
            if (args.Length < 2) { ctx.Out.WriteLine("Usage: decrypt <path> <password>"); return; }

            var node = ctx.Fs.Resolve(args[0], ctx.Cwd);
            if (node == null) { ctx.Out.WriteLine("No such node."); return; }

            var wasLocked = node.Locked;
            if (node.Unlock(args[1]))
            {
                ctx.Out.WriteLine("Unlocked.");
                if (wasLocked)
                {
                    ctx.Raise?.Invoke(OsEvents.NodeUnlocked, node.Path);
                    if (ctx.FsChanged != null) ctx.FsChanged(node.Path, FsChange.Unlocked);
                    else ctx.Raise?.Invoke(OsEvents.FsChanged, node.Path);
                }
            }
            else
            {
                ctx.Out.WriteLine("Wrong password.");
            }
        }
    }
}
