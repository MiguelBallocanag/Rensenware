using System;
using Deepwave.RetroOS.Data;

namespace Deepwave.RetroOS.Core.Terminal.Commands
{
    /// <summary>Wraps one inspector-authored <see cref="TerminalCommandEntry"/>: prints its output, sets its flag, raises its event, fires its UnityEvent.</summary>
    public class ScriptedCommand : ITerminalCommand
    {
        readonly TerminalCommandEntry _entry;
        readonly Action<string> _setFlag;

        public ScriptedCommand(TerminalCommandEntry entry, Action<string> setFlag = null)
        {
            _entry = entry;
            _setFlag = setFlag;
        }

        public string Name => _entry.Word;

        // An empty description keeps the command out of help (a hidden command).
        public string Help => _entry.description ?? "";

        public void Execute(string[] args, ShellContext ctx)
        {
            if (_entry.once && _entry.hasRun)
            {
                if (!string.IsNullOrEmpty(_entry.alreadyRunText))
                    ctx.Out.WriteLine(_entry.alreadyRunText);
                return;
            }

            _entry.hasRun = true;

            if (!string.IsNullOrEmpty(_entry.output))
                foreach (var line in _entry.output.Split('\n'))
                    ctx.Out.WriteLine(line.TrimEnd('\r'));

            var argument = args != null && args.Length > 0 ? string.Join(" ", args) : "";

            if (!string.IsNullOrWhiteSpace(_entry.setFlag)) _setFlag?.Invoke(_entry.setFlag.Trim());
            if (!string.IsNullOrWhiteSpace(_entry.raiseEvent))
                ctx.Raise?.Invoke(_entry.raiseEvent.Trim(), argument);

            _entry.onRun?.Invoke(argument);
        }
    }
}
