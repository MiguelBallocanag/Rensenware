using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Screens
{
    /// <summary>The machine starting up: the memory count and the device list on a black screen, then the maker's splash, then the logon prompt.</summary>
    public class BootScreen : MonoBehaviour
    {
        Text _log;
        Action _onFinished;
        Coroutine _running;
        bool _done;

        /// <summary>Runs the boot sequence over everything, then calls <paramref name="onFinished"/> - which is where the logon screen goes.</summary>
        public static BootScreen Play(RetroOsContext os, Transform layer,
            Data.BootSettings settings, Action onFinished)
        {
            if (!layer || settings == null || !settings.enabled)
            {
                onFinished?.Invoke();
                return null;
            }

            var panel = RetroUi.Panel(layer, Color.black, "Boot");
            RetroUi.Stretch(panel.rectTransform);
            panel.raycastTarget = true;          // swallow clicks meant for the desktop

            var screen = panel.gameObject.AddComponent<BootScreen>();
            screen._onFinished = onFinished;
            screen.Build(panel.transform, os, settings);
            return screen;
        }

        void Build(Transform panel, RetroOsContext os, Data.BootSettings settings)
        {
            var column = RetroUi.Stretch(RetroUi.NewRect("Column", panel));
            column.offsetMin = new Vector2(28f, 28f);
            column.offsetMax = new Vector2(-28f, -28f);

            _log = RetroUi.Label(column, "", 14, TextAnchor.UpperLeft, RetroSkin.Mono);
            RetroPalette.Paint(_log, PaletteSlot.ScreenText);
            RetroUi.Stretch(_log.rectTransform);

            _running = StartCoroutine(Run(os, settings));
        }

        IEnumerator Run(RetroOsContext os, Data.BootSettings settings)
        {
            var lines = PostLines(os, settings);
            var pause = Mathf.Max(0.01f, settings.lineDelay);

            var text = "";
            foreach (var line in lines)
            {
                text += line + "\n";
                _log.text = text;
                yield return new WaitForSecondsRealtime(pause);
                if (_done) yield break;
            }

            yield return new WaitForSecondsRealtime(Mathf.Max(0f, settings.holdSeconds));
            if (!_done) Finish();
        }

        // The lines a period machine printed on its way up; the numbers come from the machine,
        // so a game can make it old or damaged. Host is the machine's name; the OS is named only on the last line.
        static List<string> PostLines(RetroOsContext os, Data.BootSettings settings)
        {
            var machine = os != null && os.Machine ? os.Machine.MachineName : "MACHINE";
            var system = os != null ? os.OsName : "RetroOS";
            var lines = new List<string>
            {
                settings.firmwareBanner,
                "",
                $"CPU  : {settings.cpuName}",
                $"Memory Test : {settings.memoryKilobytes:n0} KB OK",
                ""
            };

            var drives = os?.Drives?.Drives;
            if (drives != null)
                foreach (var drive in drives)
                    lines.Add($"  {drive.Letter}:  {drive.DisplayName}" +
                              (drive.Ready ? "" : "   (no disk)"));

            lines.Add("");
            lines.Add($"Host   : {machine}");

            var address = os?.Machine ? os.Machine.Address : null;
            lines.Add(string.IsNullOrEmpty(address)
                ? "Network: not connected"
                : $"Network: {address}");

            lines.Add("");
            foreach (var extra in settings.extraLines)
                if (!string.IsNullOrWhiteSpace(extra)) lines.Add(extra);

            lines.Add("");
            lines.Add("Starting " + system + "...");
            return lines;
        }

        /// <summary>Whether a key press should skip the screen. Set by the machine to its own focus, so a computer starting up across the room is not skipped by the player walking past it.</summary>
        public Func<bool> CanSkip { get; set; }

        // Any key or click gets past it, because a boot screen the player has
        // already read twice is just a wait.
        void Update()
        {
            if (_done) return;
            if (CanSkip != null && !CanSkip()) return;
            if (!AnyInputThisFrame()) return;
            Finish();
        }

        static bool AnyInputThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) return true;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.anyKeyDown;
#else
            return false;
#endif
        }

        void Finish()
        {
            if (_done) return;
            _done = true;

            if (_running != null) { StopCoroutine(_running); _running = null; }

            var callback = _onFinished;
            _onFinished = null;
            callback?.Invoke();

            Destroy(gameObject);
        }
    }
}
