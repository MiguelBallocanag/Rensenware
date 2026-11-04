using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Deepwave.RetroOS.UI.Components
{
    // Sunken white text field matching Win95 edit controls. Wraps uGUI InputField.
    public class RetroTextBox : RetroWidget
    {
        InputField _input;
        Image _frame;

        public InputField Field => _input;

        public string Value
        {
            get => _input ? _input.text : "";
            set { if (_input) _input.text = value; }
        }

        public event Action<string> Submitted;
        public event Action<string> Edited;

        public static RetroTextBox Create(Transform parent, string text = "", bool password = false,
            bool multiline = false, Font font = null)
        {
            var box = Spawn<RetroTextBox>(parent, "TextBox");
            box.Build(text, password, multiline, font);
            return box;
        }

        void Build(string text, bool password, bool multiline, Font font)
        {
            _frame = Background(transform, PaletteSlot.Field, "Frame");
            RetroUi.Bevel(_frame.rectTransform, false, RetroSkin.BevelThickness);

            _input = gameObject.AddComponent<InputField>();
            _input.targetGraphic = _frame;

            // The label goes inside the frame: uGUI sends the caret (and selection
            // highlight) to the back of the text component's parent, so a sibling
            // label would leave the caret hidden behind the field's own paper.
            var label = RetroUi.Label(_frame.transform, "", 14,
                multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft, font);
            RetroUi.Stretch(label.rectTransform, 4f);
            label.supportRichText = false;
            _input.textComponent = label;

            if (multiline) _input.lineType = InputField.LineType.MultiLineNewline;
            if (password) _input.contentType = InputField.ContentType.Password;
            _input.text = text;

            // Softened selection colour: uGUI draws the quad behind the letters,
            // and a solid navy behind black text hides them.
            _input.selectionColor = Tint(RetroSkin.Selection, 0.55f);
            _input.customCaretColor = true;
            _input.caretColor = RetroSkin.Text;
            _input.caretWidth = 1;

            // onSubmit, not onEndEdit: onEndEdit also fires on focus loss.
            _input.onSubmit.AddListener(v => Submitted?.Invoke(v));
            _input.onValueChanged.AddListener(v => Edited?.Invoke(v));
        }

        protected override void OnInteractableChanged(bool on)
        {
            if (_input) _input.interactable = on;
            if (_frame) RetroPalette.Paint(_frame,
                on ? PaletteSlot.Field : PaletteSlot.Face);
        }

        public override void ApplyTheme()
        {
            if (_frame) RetroPalette.Paint(_frame,
                Interactable ? PaletteSlot.Field : PaletteSlot.Face);
            if (!_input) return;
            if (_input.textComponent) RetroPalette.Paint(_input.textComponent, PaletteSlot.Text);
            _input.selectionColor = Tint(RetroSkin.Selection, 0.55f);
            _input.caretColor = RetroSkin.Text;
        }

        static Color Tint(Color c, float alpha) => new(c.r, c.g, c.b, alpha);

        public void Focus()
        {
            if (!_input) return;
            _input.ActivateInputField();
            _input.Select();
        }
    }

    // Win95 drop-down list: a sunken field showing the selection plus a raised
    // arrow button that opens the list as a popup.
    public class RetroComboBox : RetroWidget
    {
        readonly List<string> _items = new();

        Text _label;
        Image _frame;
        int _index = -1;

        public event Action<int> SelectionChanged;

        public int Index => _index;
        public string Value => _index >= 0 && _index < _items.Count ? _items[_index] : "";
        public IReadOnlyList<string> Items => _items;

        public static RetroComboBox Create(Transform parent, IEnumerable<string> items, int selected,
            Action<int> onChanged)
        {
            var combo = Spawn<RetroComboBox>(parent, "ComboBox");
            combo.Build(items, selected);
            if (onChanged != null) combo.SelectionChanged += onChanged;
            return combo;
        }

        void Build(IEnumerable<string> items, int selected)
        {
            if (items != null) _items.AddRange(items);

            _frame = Background(transform, PaletteSlot.Field, "Frame");
            RetroUi.Bevel(_frame.rectTransform, false, RetroSkin.BevelThickness);

            _label = RetroUi.NoWrap(RetroUi.Label(transform, "", 13, TextAnchor.MiddleLeft));
            RetroUi.Stretch(_label.rectTransform, 4f);
            _label.rectTransform.offsetMax = new Vector2(-20f, -2f);

            // Painted arrow, not typed (like the menu's tick).
            var button = RetroPushButton.Create(transform, "", Open,
                RetroTextures.Arrow(1), BorderStyle.Raised);
            var brt = button.Rect;
            brt.anchorMin = new Vector2(1f, 0f);
            brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot = new Vector2(1f, 0.5f);
            brt.sizeDelta = new Vector2(17f, -4f);
            brt.anchoredPosition = new Vector2(-2f, 0f);
            if (button.Label) button.Label.fontSize = 9;

            gameObject.AddComponent<RetroPointerRelay>().Clicked = Open;

            SetIndex(Mathf.Clamp(selected, -1, _items.Count - 1), notify: false);
        }

        public void SetItems(IEnumerable<string> items, int selected = 0)
        {
            _items.Clear();
            if (items != null) _items.AddRange(items);
            SetIndex(Mathf.Clamp(selected, -1, _items.Count - 1), notify: false);
        }

        public void SetIndex(int index, bool notify)
        {
            _index = index;
            if (_label) _label.text = Value;
            if (notify) SelectionChanged?.Invoke(index);
        }

        void Open()
        {
            if (!Interactable || _items.Count == 0) return;
            var menu = new RetroMenu();
            for (var i = 0; i < _items.Count; i++)
            {
                var index = i;
                menu.Check(_items[i], i == _index, () => SetIndex(index, notify: true));
            }
            RetroMenuPopup.Show(menu.Items, Rect, RetroMenuPopup.Placement.Below, null);
        }

        public override void ApplyTheme()
        {
            if (_frame) RetroPalette.Paint(_frame, PaletteSlot.Field);
            if (_label) RetroPalette.Paint(_label, PaletteSlot.Text);
        }
    }

    // Yellow hint box shown after a short hover, like Win95 tooltips. Attach to any GameObject.
    public class RetroToolTip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        const float Delay = 0.5f;

        static GameObject s_tip;
        static Text s_label;

        string _text;
        float _showAt = -1f;

        public static RetroToolTip Attach(GameObject target, string text)
        {
            var tip = target.GetComponent<RetroToolTip>() ?? target.AddComponent<RetroToolTip>();
            tip._text = text;
            return tip;
        }

        public void OnPointerEnter(PointerEventData e) => _showAt = Time.unscaledTime + Delay;

        public void OnPointerExit(PointerEventData e)
        {
            _showAt = -1f;
            Hide();
        }

        void OnDisable() => Hide();

        void Update()
        {
            if (_showAt < 0f || Time.unscaledTime < _showAt) return;
            _showAt = -1f;
            ShowTip();
        }

        void ShowTip()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (!canvas || string.IsNullOrEmpty(_text)) return;

            Hide();
            var root = RetroUi.FloatingLayer((RectTransform)transform);
            if (!root) return;
            var panel = RetroUi.Panel(root, new Color(1f, 1f, 0.88f), "ToolTip");
            panel.raycastTarget = false;
            RetroUi.Bevel(panel.rectTransform, true, 1f);
            s_tip = panel.gameObject;

            s_label = RetroUi.Label(panel.transform, _text, 12, TextAnchor.MiddleCenter);
            RetroUi.Stretch(s_label.rectTransform, 4f);

            var rt = panel.rectTransform;
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(_text.Length * 6.6f + 12f, 18f);

            // Under the hovered control, or above it when near the screen bottom.
            var corners = new Vector3[4];
            ((RectTransform)transform).GetWorldCorners(corners); // 0 BL, 1 TL, 2 TR, 3 BR
            var cam = RetroUi.CanvasCamera(this);

            Vector2 Local(Vector3 world)
            {
                var screen = RectTransformUtility.WorldToScreenPoint(cam, world);
                return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    root, screen, cam, out var lp) ? lp : Vector2.zero;
            }

            var below = Local(corners[0]) + new Vector2(4f, -2f);
            var size = rt.sizeDelta;
            var area = root.rect;

            if (below.y - size.y < area.yMin) below.y = Local(corners[1]).y + size.y + 2f;

            // And never past either side, however long the text is.
            below.x = Mathf.Clamp(below.x, area.xMin, Mathf.Max(area.xMin, area.xMax - size.x));
            below.y = Mathf.Clamp(below.y, Mathf.Min(area.yMax, area.yMin + size.y), area.yMax);

            rt.anchoredPosition = below;
            rt.SetAsLastSibling();

            // Fades rather than snaps, so a delayed tip does not read as a glitch.
            RetroPopup.Play(s_tip, PopupAnimation.Fade, 16f);
        }

        static void Hide()
        {
            if (s_tip) Destroy(s_tip);
            s_tip = null;
            s_label = null;
        }
    }

    // Draggable divider between two panes; reports the new split fraction.
    public class RetroSplitter : RetroWidget, IDragHandler
    {
        public event Action<float> Moved;

        RectTransform _container;
        bool _vertical;
        float _fraction = 0.5f;

        public float Fraction => _fraction;

        public static RetroSplitter Create(Transform parent, RectTransform container, bool vertical,
            float fraction, Action<float> onMoved)
        {
            var splitter = Spawn<RetroSplitter>(parent, "Splitter");
            splitter._container = container;
            splitter._vertical = vertical;
            splitter._fraction = fraction;
            if (onMoved != null) splitter.Moved += onMoved;
            splitter.Build();
            return splitter;
        }

        void Build()
        {
            var bg = Background(transform, PaletteSlot.Face, "Bg");
            bg.raycastTarget = true;
            if (_vertical) RetroUi.Size(this, h: 5f);
            else RetroUi.Size(this, w: 5f);
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_container || !RetroUi.IsPrimary(e)) return;
            var delta = RetroUi.LocalPointerDelta(e, _container);
            var size = _vertical ? _container.rect.height : _container.rect.width;
            if (size <= 1f) return;
            var step = _vertical ? -delta.y / size : delta.x / size;
            _fraction = Mathf.Clamp(_fraction + step, 0.1f, 0.9f);
            Moved?.Invoke(_fraction);
        }
    }
}
