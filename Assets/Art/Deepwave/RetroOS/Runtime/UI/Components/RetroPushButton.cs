using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Deepwave.RetroOS.UI.Components
{
    // The Win95 push button: a raised face that goes sunken (and nudges its
    // caption down-right by a pixel) while held, with an optional icon and a
    // toggle mode for toolbars.
    public class RetroPushButton : RetroWidget, IPointerDownHandler, IPointerUpHandler,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public event Action Clicked;

        // Fired by toggle buttons with the new pressed state.
        public event Action<bool> Toggled;

        Image _face;
        RetroBevel _bevel;
        Text _label;
        Image _icon;
        RectTransform _inner;
        bool _held, _hover, _toggle, _on;

        public Text Label => _label;

        public string Caption
        {
            get => _label ? _label.text : "";
            set { if (_label) _label.text = value; }
        }

        public Sprite Icon
        {
            get => _icon ? _icon.sprite : null;
            set
            {
                if (!_icon) return;
                _icon.sprite = value;
                _icon.enabled = value;
            }
        }

        // Toggle buttons stay pressed until clicked again (toolbars, CCTV modes).
        public bool IsToggle
        {
            get => _toggle;
            set { _toggle = value; Repaint(); }
        }

        public bool On
        {
            get => _on;
            set { _on = value; Repaint(); }
        }

        public static RetroPushButton Create(Transform parent, string caption, Action onClick,
            Sprite icon = null, BorderStyle style = BorderStyle.Raised)
        {
            var button = Spawn<RetroPushButton>(parent, "Button");
            button.Build(caption, icon, style);
            if (onClick != null) button.Clicked += onClick;
            return button;
        }

        void Build(string caption, Sprite icon, BorderStyle style)
        {
            _face = Background(transform, PaletteSlot.Face, "Face");
            _bevel = Border(_face.rectTransform, style);

            // Everything inside shifts by 1px on press - the classic depress.
            _inner = RetroUi.Stretch(RetroUi.NewRect("Inner", transform));

            _icon = RetroUi.Icon(_inner, icon, 16);
            _icon.enabled = icon;
            var irt = _icon.rectTransform;
            irt.anchorMin = irt.anchorMax = new Vector2(0, 0.5f);
            irt.pivot = new Vector2(0, 0.5f);
            irt.anchoredPosition = new Vector2(6, 0);

            _label = RetroUi.Label(_inner, caption, 14,
                icon ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
            RetroUi.NoWrap(_label);
            RetroUi.Stretch(_label.rectTransform);
            if (icon) _label.rectTransform.offsetMin = new Vector2(26, 0);
        }

        protected override void OnInteractableChanged(bool on)
        {
            if (_label) RetroPalette.Paint(_label,
                on ? PaletteSlot.Text : PaletteSlot.TextDisabled);
            if (_icon) _icon.color = on ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            if (!on) { _held = false; _hover = false; }
            Repaint();
        }

        public override void ApplyTheme()
        {
            if (_label) RetroPalette.Paint(_label,
                Interactable ? PaletteSlot.Text : PaletteSlot.TextDisabled);
            Repaint();
        }

        void Repaint()
        {
            var down = _held || (_toggle && _on);
            if (_bevel) _bevel.SetRaised(!down);
            if (_inner) _inner.anchoredPosition = down ? new Vector2(1, -1) : Vector2.zero;
            if (!_face) return;

            if (_toggle && _on)
                RetroPalette.PaintOff(_face, Color.Lerp(RetroSkin.Face, RetroSkin.FaceLight, 0.5f));
            else if (_hover && Interactable && RetroSkin.HoverHighlights)
                RetroPalette.PaintOff(_face,
                    Color.Lerp(RetroSkin.Face, RetroSkin.FaceLight, 0.25f));
            else RetroPalette.Paint(_face, PaletteSlot.Face);
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (Interactable && RetroUi.IsPrimary(e)) { _held = true; Repaint(); }
        }
        public void OnPointerUp(PointerEventData e) { _held = false; Repaint(); }
        public void OnPointerEnter(PointerEventData e) { _hover = true; Repaint(); }
        public void OnPointerExit(PointerEventData e) { _hover = false; _held = false; Repaint(); }

        public void OnPointerClick(PointerEventData e)
        {
            if (!Interactable || !RetroUi.IsPrimary(e)) return;
            Click();
            if (_toggle) { _on = !_on; Repaint(); Toggled?.Invoke(_on); }
            Clicked?.Invoke();
        }
    }
}
