using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Deepwave.RetroOS.Core.Storage;

namespace Deepwave.RetroOS.UI.Components
{
    /// <summary>
    /// A side pane that folds away (a mail tray, a messenger's contacts). Animates
    /// its own width so the content slides, and remembers its open state in the
    /// machine's settings.
    /// </summary>
    public class RetroNavDrawer : RetroWidget
    {
        /// <summary>Fill this. It is the pane's body, under its header.</summary>
        public RectTransform Body { get; private set; }

        /// <summary>Whether the pane is showing.</summary>
        public bool IsOpen { get; private set; } = true;

        /// <summary>Raised when it folds or unfolds.</summary>
        public event Action<bool> Toggled;

        LayoutElement _size;
        RetroPushButton _handle;
        Coroutine _running;
        float _wide = 150f;
        float _narrow = 22f;
        string _key;
        MachinePrefs _prefs;

        /// <summary>Builds a folding pane as the first child of a horizontal row.</summary>
        /// <param name="rememberAs">Settings key its state is stored under; empty = opens every time.</param>
        /// <param name="prefs">The machine whose settings remember it (pass <c>Os.Prefs</c>).</param>
        public static RetroNavDrawer Create(Transform parent, string caption, float wide = 150f,
            string rememberAs = null, MachinePrefs prefs = null)
        {
            var drawer = Spawn<RetroNavDrawer>(parent, "NavDrawer");
            drawer.Build(caption, wide, rememberAs, prefs);
            return drawer;
        }

        void Build(string caption, float wide, string rememberAs, MachinePrefs prefs)
        {
            _wide = wide;
            _key = rememberAs;
            _prefs = prefs;

            _size = gameObject.AddComponent<LayoutElement>();
            RetroUi.VLayout(gameObject, 2, 0);

            // Clips the body so it slides out past the edge instead of squashing.
            // Menus and tool-tips sit on the floating layer, so they are unaffected.
            gameObject.AddComponent<RectMask2D>();

            var header = RetroUi.NewRect("Handle", transform);
            RetroUi.Size(header, h: 20f);
            _handle = RetroPushButton.Create(header, caption, Toggle);
            RetroUi.Stretch(_handle.Rect);
            if (_handle.Label)
            {
                _handle.Label.fontSize = 12;
                _handle.Label.alignment = TextAnchor.MiddleLeft;
            }
            RetroUi.ToolTip(_handle, "Show or hide this pane");

            Body = RetroUi.NewRect("Body", transform);
            RetroUi.Flexible(Body);
            RetroUi.VLayout(Body.gameObject, 2, 0);

            _caption = caption;

            // A pane the player folded away last time stays folded.
            var open = string.IsNullOrEmpty(_key) || (_prefs?.GetBool(_key, true) ?? true);
            Apply(open, animate: false);
        }

        string _caption = "";

        public void Toggle() => Apply(!IsOpen, animate: true);

        /// <summary>Folds or unfolds the pane.</summary>
        public void Apply(bool open, bool animate)
        {
            IsOpen = open;

            // Show the body while opening so it slides out; keep it until folded so it slides away.
            if (open && Body) Body.gameObject.SetActive(true);
            if (_handle?.Label)
            {
                // Folded, the handle is a 22px sliver - no room for the caption.
                _handle.Label.text = open ? _caption : "»";
                _handle.Label.alignment = open ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            }

            if (!string.IsNullOrEmpty(_key) && _prefs != null)
            {
                _prefs.SetBool(_key, open);
                _prefs.Save();
            }

            var target = open ? _wide : _narrow;

            if (!animate || !isActiveAndEnabled)
            {
                SetWidth(target);
                Settle();
                Toggled?.Invoke(open);
                return;
            }

            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(Slide(target));
        }

        IEnumerator Slide(float target)
        {
            var from = _size.preferredWidth;
            if (from <= 0f) from = target;

            for (var t = 0f; t < 1f;)
            {
                t += Time.unscaledDeltaTime * 8f;
                SetWidth(Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t))));
                yield return null;
            }

            _running = null;
            SetWidth(target);
            Settle();
            Toggled?.Invoke(IsOpen);
        }

        // A folded pane stops taking clicks; the mask only hides it.
        void Settle()
        {
            if (Body) Body.gameObject.SetActive(IsOpen);
        }

        void SetWidth(float width)
        {
            _size.preferredWidth = _size.minWidth = width;
            _size.flexibleWidth = 0f;
        }
    }
}
