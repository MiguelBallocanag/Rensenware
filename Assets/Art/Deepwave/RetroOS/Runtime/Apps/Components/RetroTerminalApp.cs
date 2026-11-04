using System.Collections.Generic;
using UnityEngine;
using Deepwave.RetroOS.Data;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Apps.Components
{
    // The command line, walking the same file system as Explorer.
    [AddComponentMenu("")]
    public class RetroTerminalApp : RetroAppBehaviour, ITerminalSource
    {
        [Header("Terminal")]
        [Tooltip("Printed when the terminal opens.")]
        [TextArea(2, 5)]
        [SerializeField] string banner = "RetroOS Terminal. Type 'help'.";

        [Tooltip("Print output a character at a time, the way a slow terminal " +
                 "did. Off is instant.")]
        [SerializeField] bool typewriter;

        [Tooltip("Seconds per character while the typewriter is on.")]
        [Range(0.002f, 0.1f)]
        [SerializeField] float typewriterDelay = 0.012f;

        [Header("Commands")]
        [Tooltip("Commands this machine answers, on top of the built-in ones. " +
                 "A word, what it prints, and what it sets off.")]
        [SerializeField] List<TerminalCommandEntry> commands = new();

        public string Banner => banner;
        public bool Typewriter => typewriter;
        public float TypewriterDelay => typewriterDelay;
        public List<TerminalCommandEntry> Commands => commands;

        public override void BuildContent(AppContext ctx) => AppView.Attach<TerminalView>(ctx);

        public override void ApplyDefaults()
        {
            app.appId = "terminal";
            app.title = "Terminal";
            app.builtinIcon = BuiltinIcon.Terminal;
            app.windowSize = new Vector2(620, 440);
            app.minWindowSize = new Vector2(380, 260);
        }
    }
}
