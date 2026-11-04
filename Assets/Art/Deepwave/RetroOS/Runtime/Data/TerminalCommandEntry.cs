using System;
using UnityEngine;
using UnityEngine.Events;

namespace Deepwave.RetroOS.Data
{
    /// <summary>A terminal command authored in the inspector: a word, its output, and what it triggers.</summary>
    [Serializable]
    public class TerminalCommandEntry
    {
        [Tooltip("The word the player types. One word, no spaces.")]
        public string command = "unlock";

        [Tooltip("What 'help' says about it. Empty hides it from help.")]
        public string description = "";

        [Tooltip("What the terminal prints. One line per line.")]
        [TextArea(2, 8)] public string output = "";

        [Tooltip("Only works once. Afterwards it answers alreadyRunText.")]
        public bool once;

        [Tooltip("What it prints when a once-only command is typed again.")]
        public string alreadyRunText = "Nothing further.";

        [Tooltip("Story flag set when it runs. Empty sets nothing.")]
        public string setFlag = "";

        [Tooltip("Event id raised when it runs, e.g. 'story.vault.opened'. " +
                 "Empty raises nothing.")]
        public string raiseEvent = "";

        [Tooltip("Runs with whatever the player typed after the command.")]
        public UnityEvent<string> onRun = new();

        /// <summary>Whether a once-only command has already run. Session state, not serialized.</summary>
        [NonSerialized] public bool hasRun;

        /// <summary>Whether this entry is filled in enough to register.</summary>
        public bool IsValid => !string.IsNullOrWhiteSpace(command);

        /// <summary>The word, normalised the way the shell looks it up.</summary>
        public string Word => command?.Trim().ToLowerInvariant() ?? "";
    }
}
