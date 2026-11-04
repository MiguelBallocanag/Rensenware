namespace Deepwave.RetroOS.Core.Terminal.Commands
{
    // Wipes the terminal screen.
    public class ClearCommand : ITerminalCommand
    {
        public string Name => "clear";
        public string Help => "Clear the screen.";

        public void Execute(string[] args, ShellContext ctx) => ctx.Out.Clear();
    }
}
