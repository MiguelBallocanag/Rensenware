using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Deepwave.RetroOS.Core.Terminal;
using Deepwave.RetroOS.Core.Terminal.Commands;
using Deepwave.RetroOS.Events;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Apps
{
    // Command-line app hosting the core shell.
    public class TerminalView : AppView, ITerminalOutput
    {
        // Scrollback is capped at 400 lines: a uGUI Text is one mesh and would blow past the 65k vertex limit.
        const int MaxLines = 400;
        const int MaxHistory = 64;

        readonly List<string> _lines = new();
        readonly List<string> _history = new();

        Text _output;
        bool _dirty;
        ScrollRect _scroll;
        InputField _input;
        TerminalShell _shell;
        Coroutine _typing;
        int _historyAt = -1;
        string _draft = "";

        ITerminalSource Source => Config<ITerminalSource>();

        protected override void Build()
        {
            var screen = RetroUi.Panel(Root, PaletteSlot.Screen);
            RetroUi.Stretch(screen.rectTransform);
            RetroUi.VLayout(screen.gameObject, 2, 4);

            var content = RetroUi.VScroll(screen.transform, out _scroll);
            RetroUi.Flexible(RetroUi.ScrollFrame(_scroll));
            RetroPalette.Paint(RetroUi.ScrollBackdrop(_scroll), PaletteSlot.Screen);
            _output = RetroUi.Label(content, "", 14, TextAnchor.UpperLeft, RetroSkin.Mono);
            RetroPalette.Paint(_output, PaletteSlot.ScreenText);

            var row = RetroUi.NewRect("Prompt", screen.transform);
            RetroUi.Size(row, h: 24);
            RetroUi.HLayout(row.gameObject, 4, 0);
            var caret = RetroUi.Label(row, ">", 14, TextAnchor.MiddleLeft, RetroSkin.Mono);
            RetroPalette.Paint(caret, PaletteSlot.ScreenText);
            RetroUi.Size(caret, w: 12);
            _input = RetroUi.Input(row, "", false, RetroSkin.Mono);
            RetroUi.Flexible(_input);
            RetroPalette.Paint(_input.textComponent, PaletteSlot.ScreenText);
            RetroPalette.Paint((Image)_input.targetGraphic, PaletteSlot.Screen);
            // onSubmit only: onEndEdit would also run the line when the field
            // lost focus, so clicking the title bar executed what was typed.
            _input.onSubmit.AddListener(OnSubmit);

            BuildShell();

            var banner = Source?.Banner;
            WriteLine(string.IsNullOrWhiteSpace(banner) ? "RetroOS Terminal. Type 'help'." : banner);
            WriteLine();
            Flush();
        }

        void BuildShell()
        {
            var ctx = new ShellContext(Os.Fs, this)
            {
                Viewer = Os.CurrentUser,
                OpenFile = Os.OpenFile,
                Raise = (id, arg) => Os.Events.Raise(id, arg),
                FsChanged = Os.NotifyFsChanged
            };
            _shell = new TerminalShell(ctx);
            _shell.Register(new HelpCommand(_shell))
                  .Register(new ListCommand())
                  .Register(new ChangeDirCommand())
                  .Register(new PrintCommand())
                  .Register(new OpenCommand())
                  .Register(new DecryptCommand())
                  .Register(new PwdCommand())
                  .Register(new ClearCommand());

            // The machine's own commands, authored in the inspector. They come
            // last so a game can override a built-in word if it wants to.
            var authored = Source?.Commands;
            if (authored == null) return;

            foreach (var entry in authored)
            {
                if (entry == null || !entry.IsValid) continue;
                _shell.Register(new ScriptedCommand(entry, flag => Os.Flags.Set(flag)),
                    authored: true);
            }
        }

        void OnSubmit(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;

            Remember(line);
            WriteLine("> " + line);
            Os.Events.Raise(OsEvents.TerminalCommand, line.Trim());
            _shell.Run(line);
            WriteLine();
            _input.text = "";
            Flush();
            StartCoroutine(Refocus());
        }

        IEnumerator Refocus()
        {
            yield return null;
            _input.ActivateInputField();
            _input.Select();
        }

        // --- history and completion -------------------------------------------

        void Remember(string line)
        {
            line = line.Trim();
            // A line typed twice in a row is one entry, the way every shell
            // since has treated it.
            if (_history.Count == 0 || _history[^1] != line) _history.Add(line);
            if (_history.Count > MaxHistory) _history.RemoveRange(0, _history.Count - MaxHistory);
            _historyAt = -1;
            _draft = "";
        }

        void Recall(int direction)
        {
            if (_history.Count == 0) return;

            // Stepping off the end of the history puts back whatever was
            // half-typed when the player started walking it.
            if (_historyAt < 0)
            {
                if (direction > 0) return;
                _draft = _input.text;
                _historyAt = _history.Count;
            }

            _historyAt = Mathf.Clamp(_historyAt + direction, 0, _history.Count);

            var text = _historyAt >= _history.Count ? _draft : _history[_historyAt];
            if (_historyAt >= _history.Count) _historyAt = -1;

            _input.text = text;
            _input.caretPosition = text.Length;
            _input.selectionAnchorPosition = _input.selectionFocusPosition = text.Length;
        }

        // Tab completes the command word against everything registered.
        void Complete()
        {
            var typed = _input.text ?? "";
            if (typed.Contains(' ')) return;               // only the first word

            var prefix = typed.Trim().ToLowerInvariant();
            var matches = _shell.Commands
                .Select(c => c.Name)
                .Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n)
                .ToList();

            if (matches.Count == 0) return;

            if (matches.Count == 1)
            {
                _input.text = matches[0] + " ";
                _input.caretPosition = _input.text.Length;
                return;
            }

            // Several: print them the way a shell does, and fill in as far as
            // they all agree.
            WriteLine("> " + typed);
            WriteLine(string.Join("   ", matches));
            WriteLine();
            Flush();

            var common = matches[0];
            foreach (var match in matches)
            {
                var i = 0;
                while (i < common.Length && i < match.Length &&
                       char.ToLowerInvariant(common[i]) == char.ToLowerInvariant(match[i])) i++;
                common = common[..i];
            }

            _input.text = common;
            _input.caretPosition = common.Length;
        }

        void Update()
        {
            // The prompt only listens while this window is the one on top and
            // the player is actually at this machine.
            if (Os?.Windows?.Active != Window || !Os.IsFocused()) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.upArrowKey.wasPressedThisFrame) Recall(-1);
            else if (keyboard.downArrowKey.wasPressedThisFrame) Recall(1);
            else if (keyboard.tabKey.wasPressedThisFrame) Complete();
        }

        // --- output -----------------------------------------------------------

        public void WriteLine(string line = "")
        {
            _lines.Add(line ?? "");
            if (_lines.Count > MaxLines) _lines.RemoveRange(0, _lines.Count - MaxLines);
            _dirty = true;
        }

        // One rebuild per command, not one per line: a command like `ls` in a
        // full folder used to force a whole canvas rebuild for every entry.
        void Flush()
        {
            if (!_dirty || !_output) return;
            _dirty = false;

            var text = string.Join("\n", _lines);

            if (Source is { Typewriter: true } && isActiveAndEnabled)
            {
                if (_typing != null) StopCoroutine(_typing);
                _typing = StartCoroutine(Type(text));
                return;
            }

            _output.text = text;
            ScrollToEnd();
        }

        // Types out the newest output a character at a time; only the tail is animated.
        IEnumerator Type(string full)
        {
            var already = _output.text ?? "";
            var from = full.StartsWith(already, StringComparison.Ordinal) ? already.Length : 0;
            var delay = Mathf.Max(0.002f, Source?.TypewriterDelay ?? 0.012f);

            _output.text = full[..from];
            ScrollToEnd();

            for (var i = from; i < full.Length; i++)
            {
                _output.text = full[..(i + 1)];

                // A newline is where the eye rests, so that is where the
                // scroll is worth the layout rebuild.
                if (full[i] == '\n') ScrollToEnd();
                yield return new WaitForSecondsRealtime(delay);
            }

            _output.text = full;
            ScrollToEnd();
            _typing = null;
        }

        void ScrollToEnd()
        {
            Canvas.ForceUpdateCanvases();
            if (_scroll) _scroll.verticalNormalizedPosition = 0f;
        }

        public void Clear()
        {
            if (_typing != null) { StopCoroutine(_typing); _typing = null; }
            _lines.Clear();
            _output.text = "";
            _dirty = false;
        }
    }
}
