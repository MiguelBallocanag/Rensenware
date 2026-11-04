using System;
using System.Linq;

namespace Deepwave.RetroOS.Core.Terminal.Commands
{
    // Lists every registered command.
    public class HelpCommand : ITerminalCommand
    {
        readonly TerminalShell _shell;

        public HelpCommand(TerminalShell shell) => _shell = shell;

        public string Name => "help";
        public string Help => "List available commands.";

        public void Execute(string[] args, ShellContext ctx)
        {
            // A command with no help text is hidden - help does not give it away.
            var listed = _shell.Commands
                .Where(c => !string.IsNullOrWhiteSpace(c.Help))
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var command in listed)
                ctx.Out.WriteLine($"{command.Name,-10}{command.Help}");
        }
    }
}
