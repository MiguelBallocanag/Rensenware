using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deepwave.RetroOS.UI.Components
{
    /// <summary>
    /// A stack of panels of which exactly one is shown, cross-fading between them
    /// (a tab body, a nav-drawer page). Hidden panels are switched off but keep
    /// their state - scroll position, selection, half-typed text.
    /// </summary>
    public class RetroPanelHost : RetroWidget
    {
        class Panel
        {
            public string Name;
            public RectTransform Page;
            public RetroPopup Popup;
        }

        readonly List<Panel> _panels = new();
        int _current = -1;

        /// <summary>Raised with the index of the panel that came forward.</summary>
        public event Action<int> Changed;

        /// <summary>The panel on show, or -1 before the first one is added.</summary>
        public int Current => _current;

        /// <summary>How many panels the host holds.</summary>
        public int Count => _panels.Count;

        /// <summary>How a panel comes into view.</summary>
        public PopupAnimation Transition { get; set; } = PopupAnimation.Fade;

        /// <summary>How quickly it does so.</summary>
        public float TransitionSpeed { get; set; } = 12f;

        public static RetroPanelHost Create(Transform parent)
        {
            var host = Spawn<RetroPanelHost>(parent, "Panels");
            RetroUi.Stretch(host.Rect);
            return host;
        }

        /// <summary>Adds a panel and returns the page to fill. The first panel is shown at once, no transition.</summary>
        public RectTransform AddPanel(string name)
        {
            var page = RetroUi.Stretch(RetroUi.NewRect(name, transform));
            RetroUi.VLayout(page.gameObject, 4, 0);

            var group = page.gameObject.AddComponent<CanvasGroup>();
            var popup = page.gameObject.AddComponent<RetroPopup>();
            popup.mode = Transition;
            popup.speed = TransitionSpeed;
            popup.deactivateOnHide = true;

            var first = _panels.Count == 0;
            _panels.Add(new Panel { Name = name, Page = page, Popup = popup });

            if (first)
            {
                _current = 0;
                group.alpha = 1f;
            }
            else
            {
                page.gameObject.SetActive(false);
            }

            return page;
        }

        /// <summary>The page behind a panel, or <c>null</c> if there is no such panel.</summary>
        public RectTransform Page(string name)
        {
            var index = IndexOf(name);
            return index < 0 ? null : _panels[index].Page;
        }

        /// <summary>Brings a panel forward by name.</summary>
        public bool Show(string name)
        {
            var index = IndexOf(name);
            if (index < 0) return false;
            Show(index);
            return true;
        }

        /// <summary>Brings a panel forward. Does nothing if it is already showing.</summary>
        public void Show(int index)
        {
            if (index < 0 || index >= _panels.Count || index == _current) return;

            if (_current >= 0 && _current < _panels.Count)
            {
                var outgoing = _panels[_current];
                outgoing.Popup.mode = Transition;
                outgoing.Popup.speed = TransitionSpeed;
                outgoing.Popup.PlayOut();
            }

            _current = index;
            var incoming = _panels[index];
            incoming.Popup.mode = Transition;
            incoming.Popup.speed = TransitionSpeed;
            incoming.Popup.PlayIn();

            Changed?.Invoke(index);
        }

        /// <summary>Steps to the next panel, stopping at the last.</summary>
        public void Next()
        {
            if (_current < _panels.Count - 1) Show(_current + 1);
        }

        /// <summary>Steps to the previous panel, stopping at the first.</summary>
        public void Previous()
        {
            if (_current > 0) Show(_current - 1);
        }

        int IndexOf(string name)
        {
            for (var i = 0; i < _panels.Count; i++)
                if (string.Equals(_panels[i].Name, name, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }
    }
}
