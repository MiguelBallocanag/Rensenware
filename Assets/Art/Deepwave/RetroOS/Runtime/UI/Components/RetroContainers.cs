using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Deepwave.RetroOS.UI.Components
{
    // Etched frame with a caption notched into the top edge - the Win95 group box.
    public class RetroGroupBox : RetroWidget
    {
        public RectTransform Body { get; private set; }

        public static RetroGroupBox Create(Transform parent, string caption, int spacing = 4)
        {
            var box = Spawn<RetroGroupBox>(parent, "GroupBox");
            box.Build(caption, spacing);
            return box;
        }

        void Build(string caption, int spacing)
        {
            var frame = RetroUi.NewRect("Frame", transform);
            RetroUi.Stretch(frame);
            frame.offsetMax = new Vector2(0f, -7f);
            RetroUi.Bevel(frame, false, 1f);

            var labelBg = RetroUi.Panel(transform, PaletteSlot.Face, "CaptionBg");
            var lrt = labelBg.rectTransform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0f, 1f);
            lrt.pivot = new Vector2(0f, 1f);
            lrt.sizeDelta = new Vector2(RetroUi.TextWidth(caption, 12f) + 12f, 14f);
            lrt.anchoredPosition = new Vector2(8f, 0f);
            labelBg.raycastTarget = false;

            var label = RetroUi.NoWrap(
                RetroUi.Label(labelBg.transform, caption, 12, TextAnchor.MiddleCenter));
            RetroUi.Stretch(label.rectTransform);

            Body = RetroUi.NewRect("Body", transform);
            RetroUi.Stretch(Body);
            Body.offsetMin = new Vector2(8f, 8f);
            Body.offsetMax = new Vector2(-8f, -18f);
            RetroUi.VLayout(Body.gameObject, spacing, 0);
        }
    }

    // Win95 tab control: raised tabs along the top, the selected one lifted into the page.
    public class RetroTabControl : RetroWidget
    {
        class Tab
        {
            public Image Bg;
            public Text Label;
            public RectTransform Holder;
        }

        readonly List<Tab> _tabs = new();
        RectTransform _strip;
        RetroPanelHost _pages;
        int _selected = -1;

        public event Action<int> SelectionChanged;

        public int Selected => _selected;

        public static RetroTabControl Create(Transform parent)
        {
            var control = Spawn<RetroTabControl>(parent, "TabControl");
            control.Build();
            return control;
        }

        void Build()
        {
            _strip = RetroUi.NewRect("Tabs", transform);
            _strip.anchorMin = new Vector2(0f, 1f);
            _strip.anchorMax = new Vector2(1f, 1f);
            _strip.pivot = new Vector2(0.5f, 1f);
            _strip.sizeDelta = new Vector2(0f, 22f);
            RetroUi.HLayout(_strip.gameObject, 2, 0);

            var body = RetroUi.Panel(transform, PaletteSlot.Face, "Page");
            var brt = body.rectTransform;
            RetroUi.Stretch(brt);
            brt.offsetMax = new Vector2(0f, -20f);
            RetroUi.Bevel(brt, true, RetroSkin.BevelThickness);

            // A panel host, so tab bodies cross-fade and hidden pages cost nothing.
            var pageArea = RetroUi.Stretch(RetroUi.NewRect("Pages", body.transform), 4f);
            _pages = RetroPanelHost.Create(pageArea);
        }

        // Adds a tab and returns the (vertically laid out) page to fill.
        public RectTransform AddTab(string caption)
        {
            var index = _tabs.Count;

            var holder = RetroUi.NewRect(caption, _strip);

            // Crowd and clip tabs in a narrow window rather than spill outside the frame.
            RetroUi.SoftSize(holder, RetroUi.TextWidth(caption) + 24f);

            var bg = RetroUi.Panel(holder, PaletteSlot.Face, "TabBg");
            RetroUi.Stretch(bg.rectTransform);
            RetroUi.Bevel(bg.rectTransform, true, 1f);

            var label = RetroUi.NoWrap(RetroUi.Label(bg.transform, caption, 13, TextAnchor.MiddleCenter));
            RetroUi.Stretch(label.rectTransform);

            bg.gameObject.AddComponent<RetroPointerRelay>().Clicked = () => Select(index);

            var page = _pages.AddPanel(caption);

            _tabs.Add(new Tab { Bg = bg, Label = label, Holder = holder });
            if (_selected < 0) { _selected = 0; StyleTabs(0); }
            return page;
        }

        public void Select(int index)
        {
            if (index < 0 || index >= _tabs.Count) return;
            if (_selected == index) return;
            _selected = index;
            Click();

            _pages.Show(index);
            StyleTabs(index);
            SelectionChanged?.Invoke(index);
        }

        void StyleTabs(int index)
        {
            for (var i = 0; i < _tabs.Count; i++)
            {
                var on = i == index;
                _tabs[i].Label.fontStyle = on ? FontStyle.Bold : FontStyle.Normal;

                // The active tab grows a few pixels to read as lifted. Set on the
                // element directly - RetroUi.Size would also pin the width.
                var element = _tabs[i].Holder.GetComponent<LayoutElement>();
                if (element)
                {
                    element.preferredHeight = element.minHeight = on ? 22f : 19f;
                    element.flexibleHeight = 0f;
                }
                if (on) RetroPalette.Paint(_tabs[i].Bg, PaletteSlot.Face);
                else RetroPalette.PaintOff(_tabs[i].Bg,
                    Color.Lerp(RetroSkin.Face, RetroSkin.FaceDark, 0.18f));
            }
        }
    }

    // Toolbar strip: a raised rail of icon buttons with group separators.
    // For Explorer/Notepad/Browser command bars.
    public class RetroToolStrip : RetroWidget
    {
        public static RetroToolStrip Create(Transform parent)
        {
            var strip = Spawn<RetroToolStrip>(parent, "ToolStrip");
            strip.Build();
            return strip;
        }

        void Build()
        {
            var bg = Background(transform, PaletteSlot.Face, "Bg");
            RetroUi.Bevel(bg.rectTransform, true, 1f);
            RetroUi.HLayout(gameObject, 2, 2);
            RetroUi.Size(this, h: RetroSkin.ToolbarHeight);
        }

        public RetroPushButton Add(Sprite icon, string tooltip, Action onClick, string caption = "")
        {
            var button = RetroPushButton.Create(transform, caption, onClick, icon, BorderStyle.RaisedThin);
            // An icon eats the first 26px; a captioned button needs room for both.
            // SoftSize, not Size: Size pins minWidth, so a too-full strip cannot
            // give anything back and runs off the edge. Under pressure a captioned
            // button gives back its caption and keeps its icon.
            var width = string.IsNullOrEmpty(caption)
                ? 26f
                : (icon ? 30f : 8f) + RetroUi.TextWidth(caption, 14f) + 8f;
            RetroUi.SoftSize(button, width, icon ? 26f : 24f);
            if (!string.IsNullOrEmpty(tooltip)) RetroToolTip.Attach(button.gameObject, tooltip);
            return button;
        }

        public RetroPushButton AddToggle(Sprite icon, string tooltip, bool on, Action<bool> onToggled)
        {
            var button = Add(icon, tooltip, null);
            button.IsToggle = true;
            button.On = on;
            if (onToggled != null) button.Toggled += onToggled;
            return button;
        }

        public void AddSeparator()
        {
            var gap = RetroUi.NewRect("Sep", transform);
            RetroUi.Size(gap, w: 7f);
            var line = RetroUi.Panel(gap, PaletteSlot.FaceDark, "Line");
            var rt = line.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(1f, -4f);
            rt.anchoredPosition = new Vector2(-0.5f, 0f);
            var hi = RetroUi.Panel(gap, PaletteSlot.FaceLight, "Hi");
            var hrt = hi.rectTransform;
            hrt.anchorMin = new Vector2(0.5f, 0f);
            hrt.anchorMax = new Vector2(0.5f, 1f);
            hrt.sizeDelta = new Vector2(1f, -4f);
            hrt.anchoredPosition = new Vector2(0.5f, 0f);
        }

        public void AddSpacer() => RetroUi.Spacer(transform);
    }

    // Status bar with several sunken panes, the way Explorer and Word had them.
    public class RetroStatusStrip : RetroWidget
    {
        readonly List<Text> _panes = new();

        public static RetroStatusStrip Create(Transform parent, params float[] paneWidths)
        {
            var strip = Spawn<RetroStatusStrip>(parent, "StatusStrip");
            strip.Build(paneWidths);
            return strip;
        }

        void Build(float[] widths)
        {
            Background(transform, PaletteSlot.Face, "Bg");
            RetroUi.HLayout(gameObject, 2, 2);
            RetroUi.Size(this, h: RetroSkin.StatusBarHeight);

            if (widths == null || widths.Length == 0) widths = new[] { -1f };
            foreach (var width in widths)
            {
                var pane = RetroUi.Panel(transform, PaletteSlot.Face, "Pane");
                RetroUi.Bevel(pane.rectTransform, false, 1f);
                if (width > 0f) RetroUi.Size(pane, w: width);
                else RetroUi.Flexible(pane);

                var label = RetroUi.NoWrap(RetroUi.Label(pane.transform, "", 12, TextAnchor.MiddleLeft));
                RetroUi.Stretch(label.rectTransform, 4f);
                _panes.Add(label);
            }
        }

        public void Set(int pane, string text)
        {
            if (pane >= 0 && pane < _panes.Count) _panes[pane].text = text ?? "";
        }

        public string this[int pane]
        {
            get => pane >= 0 && pane < _panes.Count ? _panes[pane].text : "";
            set => Set(pane, value);
        }
    }
}
