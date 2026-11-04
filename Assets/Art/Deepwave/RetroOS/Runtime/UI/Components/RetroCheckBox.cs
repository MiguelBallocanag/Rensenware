using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Deepwave.RetroOS.UI.Components
{
    // Win95 check box: a 13px sunken white square with a black tick, plus a
    // clickable caption. Set Radio = true for the round option-button variant
    // (RetroRadioGroup wires exclusivity).
    public class RetroCheckBox : RetroWidget, IPointerClickHandler
    {
        public event Action<bool> Changed;

        const float BoxSize = 13f;

        Image _box;
        RectTransform _tick;
        Text _label;
        bool _checked;
        bool _radio;

        public bool Radio => _radio;

        public bool Checked
        {
            get => _checked;
            set => SetChecked(value, notify: false);
        }

        public string Caption
        {
            get => _label ? _label.text : "";
            set { if (_label) _label.text = value; }
        }

        public static RetroCheckBox Create(Transform parent, string caption, bool value,
            Action<bool> onChanged, bool radio = false)
        {
            var box = Spawn<RetroCheckBox>(parent, radio ? "Radio" : "CheckBox");
            box.Build(caption, value, radio);
            if (onChanged != null) box.Changed += onChanged;
            return box;
        }

        void Build(string caption, bool value, bool radio)
        {
            _radio = radio;

            // Transparent hit area so the whole row is clickable.
            var hit = gameObject.AddComponent<Image>();
            hit.color = Color.clear;

            _box = RetroUi.Panel(transform, PaletteSlot.Field, "Box");
            var brt = _box.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(0f, 0.5f);
            brt.pivot = new Vector2(0f, 0.5f);
            brt.sizeDelta = new Vector2(BoxSize, BoxSize);
            brt.anchoredPosition = new Vector2(2f, 0f);
            RetroUi.Bevel(brt, false, 1f);

            _tick = RetroUi.NewRect("Mark", _box.transform);
            RetroUi.Stretch(_tick);
            BuildMark();

            _label = RetroUi.Label(transform, caption, 13, TextAnchor.MiddleLeft);
            RetroUi.Stretch(_label.rectTransform);
            _label.rectTransform.offsetMin = new Vector2(BoxSize + 8f, 0f);

            _checked = value;
            _tick.gameObject.SetActive(value);
        }

        // Checkbox: a two-stroke tick. Radio: a filled dot.
        void BuildMark()
        {
            if (_radio)
            {
                Dot(new Vector2(0f, 0f), new Vector2(5f, 5f));
                Dot(new Vector2(0f, 0f), new Vector2(3f, 7f));
                Dot(new Vector2(0f, 0f), new Vector2(7f, 3f));
                return;
            }

            // Short down-stroke then the long up-stroke: a hand-drawn tick.
            for (var i = 0; i < 3; i++) Dot(new Vector2(-4f + i, 1f - i), new Vector2(2f, 2f));
            for (var i = 0; i < 5; i++) Dot(new Vector2(-1f + i, -1f + i), new Vector2(2f, 2f));
        }

        void Dot(Vector2 pos, Vector2 size)
        {
            var img = RetroUi.NewRect("Px", _tick).gameObject.AddComponent<Image>();
            RetroPalette.Paint(img, PaletteSlot.Text);
            img.raycastTarget = false;
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            img.rectTransform.sizeDelta = size;
            img.rectTransform.anchoredPosition = pos;
        }

        public void SetChecked(bool value, bool notify)
        {
            if (_checked == value) return;
            _checked = value;
            if (_tick) _tick.gameObject.SetActive(value);
            if (notify) Changed?.Invoke(value);
        }

        protected override void OnInteractableChanged(bool on)
        {
            if (_label) RetroPalette.Paint(_label,
                on ? PaletteSlot.Text : PaletteSlot.TextDisabled);
        }

        public override void ApplyTheme()
        {
            if (_box) RetroPalette.Paint(_box, PaletteSlot.Field);
            if (_label) RetroPalette.Paint(_label,
                Interactable ? PaletteSlot.Text : PaletteSlot.TextDisabled);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (!Interactable || !RetroUi.IsPrimary(e)) return;
            Click();
            // Radio buttons only turn on; the group turns the others off.
            SetChecked(_radio || !_checked, notify: true);
        }
    }

    // Makes a set of RetroCheckBox radios mutually exclusive.
    public class RetroRadioGroup
    {
        readonly System.Collections.Generic.List<RetroCheckBox> _options = new();

        public event Action<int> SelectionChanged;

        public int Selected { get; private set; } = -1;

        public RetroCheckBox Add(Transform parent, string caption)
        {
            var index = _options.Count;
            var option = RetroCheckBox.Create(parent, caption, false, null, radio: true);
            option.Changed += _ => Select(index);
            _options.Add(option);
            return option;
        }

        public void Select(int index)
        {
            Selected = index;
            for (var i = 0; i < _options.Count; i++) _options[i].SetChecked(i == index, notify: false);
            SelectionChanged?.Invoke(index);
        }
    }
}
