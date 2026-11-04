using System.Linq;
using Deepwave.RetroOS.Core.FileSystem;

namespace Deepwave.RetroOS.Core.Terminal.Commands
{
    // Lists a directory; "-a" reveals hidden entries.
    public class ListCommand : ITerminalCommand
    {
        public string Name => "ls";
        public string Help => "List directory (-a shows hidden).";

        public void Execute(string[] args, ShellContext ctx)
        {
            var showHidden = args.Contains("-a");
            var path = args.FirstOrDefault(a => !a.StartsWith("-"));
            var node = path == null ? ctx.Cwd : ctx.Fs.Resolve(path, ctx.Cwd);

            if (node is not VfsDirectory dir) { ctx.Out.WriteLine("Not a directory."); return; }

            foreach (var c in dir.List(showHidden, ctx.Viewer))
            {
                var tag = c is VfsDirectory ? "<DIR> " : "      ";
                ctx.Out.WriteLine($"{tag}{c.Name}{(c.Locked ? "  [LOCKED]" : "")}");
            }
        }
    }
}
