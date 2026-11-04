using System;
using System.Collections.Generic;
using System.Linq;

namespace Deepwave.RetroOS.Core.Terminal
{
    // Parses lines and dispatches to registered commands.
    public class TerminalShell
    {
        readonly Dictionary<string, ITerminalCommand> _commands = new();

        // Inspector-authored words: replacing a built-in is silent, replacing another authored one warns.
        readonly HashSet<string> _authored = new();

        public ShellContext Context { get; }
        public IEnumerable<ITerminalCommand> Commands => _commands.Values;

        public TerminalShell(ShellContext ctx) => Context = ctx;

        /// <summary>Adds a command, replacing any that already answers to the same word.</summary>
        /// <param name="authored">From the inspector, not the package. Two authored commands sharing a word warns; an authored one replacing a built-in is silent.</param>
        public TerminalShell Register(ITerminalCommand cmd, bool authored = false)
        {
            if (cmd == null || string.IsNullOrWhiteSpace(cmd.Name))
            {
                UnityEngine.Debug.LogWarning("RetroOS: a terminal command with no word was " +
                                             "skipped. Give it one in the inspector.");
                return this;
            }

            var word = cmd.Name.ToLowerInvariant();

            // Only the authored-over-authored case: one word runs one command, silently.
            if (authored && _authored.Contains(word))
                UnityEngine.Debug.LogWarning($"RetroOS: two terminal commands are both '{word}'. " +
                                             "Only the last one will run; give one of them a " +
                                             "different word.");

            if (authored) _authored.Add(word);
            _commands[word] = cmd;
            return this;
        }

        public void Run(string line)
        {
            var parts = line?.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts == null || parts.Length == 0) return;

            var name = parts[0].ToLowerInvariant();
            if (_commands.TryGetValue(name, out var cmd)) cmd.Execute(parts.Skip(1).ToArray(), Context);
            else Context.Out.WriteLine($"'{name}' is not recognized.");
        }
    }
}
