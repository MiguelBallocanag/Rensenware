namespace Deepwave.RetroOS.Core.Terminal.Commands
{
    // Prints the working directory path.
    public class PwdCommand : ITerminalCommand
    {
        public string Name => "pwd";
        public string Help => "Print working directory.";

        public void Execute(string[] args, ShellContext ctx) => ctx.Out.WriteLine(ctx.Cwd.Path);
    }
}
