using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Screens
{
    /// <summary>A full-screen state of the machine itself, as opposed to anything the desktop is showing: the blue screen, and the screens a shutdown ends on. They cover everything the OS has drawn, swallow input, and are torn down again by <see cref="Dismiss"/>.</summary>
    public class SystemScreen : MonoBehaviour
    {
        Action _onDismiss;
        bool _dismissOnKey;

        /// <summary>Raised when the screen is cleared - by the player on a blue screen that allows it, or by code.</summary>
        public event Action Dismissed;

        /// <summary>The system screen on <paramref name="layer"/>, or <c>null</c>.</summary>
        public static SystemScreen On(Transform layer) =>
            layer ? layer.GetComponentInChildren<SystemScreen>(true) : null;

        /// <summary>Whether a machine has a system screen up.</summary>
        public static bool IsShowingOn(Transform layer) => On(layer);

        /// <summary>The classic blue screen. With <paramref name="canContinue"/> on, the player can press a key or click to return to the desktop, which is how a scripted scare hands control back.</summary>
        public static SystemScreen Bsod(Transform layer, string title, string message,
            bool canContinue = true)
        {
            var screen = Create(layer, "BSOD", Hex("0000AA"));
            var column = screen.Column(48, 24);

            // The title sits in a light bar, centred, exactly as wide as its text.
            var titleRow = RetroUi.NewRect("TitleRow", column);
            RetroUi.Size(titleRow, h: 22);
            RetroUi.HLayout(titleRow.gameObject, 0, 0);
            titleRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;

            var plate = RetroUi.Panel(titleRow, Hex("AAAAAA"), "Plate");
            RetroUi.Size(plate, RetroUi.TextWidth(title, 15) + 24, 22);

            var heading = RetroUi.Label(plate.transform, title, 15, TextAnchor.MiddleCenter, RetroSkin.Mono);
            heading.color = Hex("0000AA");
            RetroUi.NoWrap(heading);
            RetroUi.Stretch(heading.rectTransform);

            Gap(column, 18);

            var body = RetroUi.Label(column, message, 14, TextAnchor.UpperLeft, RetroSkin.Mono);
            RetroPalette.PaintOff(body, Color.white);
            RetroUi.Flexible(body);

            if (canContinue)
            {
                var footer = RetroUi.Label(column,
                    "Press any key to continue _", 14, TextAnchor.MiddleCenter, RetroSkin.Mono);
                RetroPalette.PaintOff(footer, Color.white);
                RetroUi.Size(footer, h: 22);
                screen.EnableDismiss();
            }

            return screen;
        }

        /// <summary>The please-wait notice a machine shows on its way down, and the one it shows while it signs a user out.</summary>
        public static SystemScreen ShuttingDown(Transform layer) =>
            Notice(layer, "ShuttingDown", "Please wait while your computer shuts down.");

        /// <summary>The please-wait notice shown while a user is signed out.</summary>
        public static SystemScreen LoggingOff(Transform layer) =>
            Notice(layer, "LoggingOff", "Please wait while the system logs you off.");

        static SystemScreen Notice(Transform layer, string name, string message)
        {
            var screen = Create(layer, name, Hex("008080"));
            var panel = RetroUi.Panel(screen.transform, PaletteSlot.Face, "Plate");
            var rt = panel.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(380, 90);
            RetroUi.Bevel(rt, true);

            var label = RetroUi.Label(panel.transform, message, 14, TextAnchor.MiddleCenter);
            RetroUi.Stretch(label.rectTransform, 16);
            return screen;
        }

        /// <summary>A bare black screen - the monitor with nothing on it.</summary>
        public static SystemScreen Blackout(Transform layer) => Create(layer, "Blackout", Color.black);

        /// <summary>Fades this screen to black over <paramref name="seconds"/> and runs <paramref name="onFaded"/> when the glass is dark.</summary>
        public SystemScreen FadeToBlack(float seconds, Action onFaded = null)
        {
            var cover = RetroUi.Panel(transform, new Color(0, 0, 0, 0), "Fade");
            RetroUi.Stretch(cover.rectTransform);
            cover.raycastTarget = true;
            StartCoroutine(Fade(cover, seconds, onFaded));
            return this;
        }

        static IEnumerator Fade(Image cover, float seconds, Action onFaded)
        {
            for (var t = 0f; cover && t < seconds; t += Time.unscaledDeltaTime)
            {
                cover.color = new Color(0, 0, 0, Mathf.Clamp01(t / Mathf.Max(seconds, 0.0001f)));
                yield return null;
            }
            // A screen torn down mid-fade (the machine was switched back on)
            // never reached black, so nothing downstream should act as if it did.
            if (!cover) yield break;

            cover.color = Color.black;
            onFaded?.Invoke();
        }

        static SystemScreen Create(Transform layer, string name, Color background)
        {
            // Only this machine's, because this layer is only this machine's.
            var existing = On(layer);
            if (existing) existing.Dismiss();

            // A menu open when the machine goes down is parented beside this layer; nothing outranks a system screen, so it takes the top on the way in.
            if (layer) layer.SetAsLastSibling();

            var panel = RetroUi.Panel(layer, background, name);
            RetroUi.Stretch(panel.rectTransform);
            panel.raycastTarget = true;          // swallow clicks meant for the desktop

            return panel.gameObject.AddComponent<SystemScreen>();
        }

        // A centred column the screens lay their text out in.
        Transform Column(float sidePadding, float topPadding)
        {
            var column = RetroUi.Stretch(RetroUi.NewRect("Column", transform));
            column.offsetMin = new Vector2(sidePadding, topPadding);
            column.offsetMax = new Vector2(-sidePadding, -topPadding);
            RetroUi.VLayout(column.gameObject, 0, 0);
            return column;
        }

        static void Gap(Transform parent, float height) =>
            RetroUi.Size(RetroUi.NewRect("Gap", parent), h: height);

        void EnableDismiss()
        {
            _dismissOnKey = true;
            var button = gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(Dismiss);
        }

        /// <summary>Runs a callback when the screen is cleared. Chainable.</summary>
        public SystemScreen OnDismiss(Action action)
        {
            _onDismiss = action;
            return this;
        }

        /// <summary>Whether a key press should clear the screen. Set by the machine to its own focus, so a computer that crashed across the room is not cleared by the player walking past it.</summary>
        public Func<bool> CanDismiss { get; set; }

        void Update()
        {
            if (!_dismissOnKey) return;
            if (CanDismiss != null && !CanDismiss()) return;
            if (AnyKeyThisFrame()) Dismiss();
        }

        // Input System when it is present, legacy input otherwise, so the
        // package drops into a project on either backend.
        static bool AnyKeyThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            return keyboard != null && keyboard.anyKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.anyKeyDown;
#else
            return false;
#endif
        }

        /// <summary>Clears the screen and destroys it, running the <see cref="OnDismiss"/> callback and raising <see cref="Dismissed"/>.</summary>
        public void Dismiss()
        {
            var callback = _onDismiss;
            _onDismiss = null;
            _dismissOnKey = false;

            Dismissed?.Invoke();
            callback?.Invoke();

            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            Destroy(gameObject);
        }

        static Color Hex(string rgb) =>
            ColorUtility.TryParseHtmlString("#" + rgb, out var c) ? c : Color.magenta;
    }
}
