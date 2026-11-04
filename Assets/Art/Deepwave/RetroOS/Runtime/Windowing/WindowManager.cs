using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Windowing
{
    /// <summary>Builds the Windows 95 window chrome and owns focus and z-order for every window on the desktop.</summary>
    public class WindowManager
    {
        readonly RectTransform _layer;
        readonly List<Window> _windows = new();
        Vector2 _cascade = new(-160, 120);

        /// <summary>The look these windows are drawn in - this machine's, not the process's.</summary>
        public MachineSkin Skin { get; internal set; }

        public event Action<Window> Opened;
        public event Action<Window> Closed;
        public event Action<Window> Focused;

        public WindowManager(RectTransform windowLayer) => _layer = windowLayer;

        public Window Open(string title, Vector2 size, Sprite icon = null) =>
            Open(title, size, icon, WindowOptions.Default);

        public Window Open(string title, Vector2 size, Sprite icon, WindowOptions opts)
        {
            Skin?.Claim();
            size = FitToLayer(Vector2.Max(size, opts.MinSize));

            // The host: full work area, draws nothing, carries the window's
            // depth, its transition and its alpha.
            var host = RetroUi.Stretch(RetroUi.NewRect("Window", _layer));
            host.gameObject.AddComponent<CanvasGroup>();
            var transition = host.gameObject.AddComponent<WindowTransition>();
            var win = host.gameObject.AddComponent<Window>();
            win.MinSize = opts.MinSize;
            win.BottomInset = RetroSkin.TaskbarHeight;

            // The frame: the chrome the player sees, floating inside the host.
            var frame = RetroUi.NewRect("Frame", host);
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = size;
            frame.localPosition = _cascade;
            Advance();

            RetroPalette.Paint(frame.gameObject.AddComponent<Image>(), PaletteSlot.Face);
            RetroUi.Bevel(frame, true);

            var bar = BuildTitleBar(frame, win, title, icon, out var titleText);

            var content = RetroUi.NewRect("Content", frame);
            RetroUi.Stretch(content);
            content.offsetMin = new Vector2(4, 4);
            content.offsetMax = new Vector2(-4, -(RetroSkin.TitleBarHeight + 4));

            // The chrome zooms; the host only carries alpha, so a corner window opens where it sits.
            transition.Target = frame;

            win.Bind(frame, content, bar, titleText, icon);
            win.SetTitle(title);
            win.Focused += () => Focus(win);
            win.Closed += () => Remove(win);

            var dragger = bar.gameObject.AddComponent<WindowDragger>();
            dragger.dragObject = frame;
            dragger.dragArea = host;
            dragger.bottomInset = RetroSkin.TaskbarHeight;
            dragger.onFocus = win.RequestFocus;
            dragger.onDoubleClick = win.ToggleMaximize;
            dragger.canDrag = () => !win.Maximized;

            var grip = opts.Resizable ? BuildResizeGrip(frame, host, win) : null;
            win.BindHandles(dragger, grip);

            win.ClampToArea();
            _windows.Add(win);
            Opened?.Invoke(win);
            Focus(win);
            if (opts.StartMaximized) win.ToggleMaximize();
            win.PlayOpen();
            return win;
        }

        // Requested sizes never exceed the desktop working area.
        Vector2 FitToLayer(Vector2 size)
        {
            var avail = _layer.rect.size;
            if (avail.x < 100 || avail.y < 100) return size;
            return new Vector2(
                Mathf.Min(size.x, avail.x - 16),
                Mathf.Min(size.y, avail.y - RetroSkin.TaskbarHeight - 16));
        }

        /// <summary>The rect every window lives in: the desktop work area.</summary>
        public RectTransform Layer => _layer;

        /// <summary>Every window currently open, back to front.</summary>
        public IReadOnlyList<Window> Windows => _windows;

        /// <summary>The window on top, or <c>null</c> when the desktop is bare.</summary>
        public Window Active { get; private set; }

        /// <summary>Brings a window to the front and gives it the active title bar.</summary>
        public void Focus(Window win)
        {
            if (!win) return;

            // Focus repaints every title bar on this desktop, so claim this machine's palette first.
            Skin?.Claim();

            win.transform.SetAsLastSibling();
            Active = win;
            foreach (var w in _windows)
                if (w) w.SetFocused(w == win);

            Focused?.Invoke(win);
        }

        Image BuildTitleBar(RectTransform frame, Window win, string title, Sprite icon, out Text titleText)
        {
            var bar = RetroUi.Panel(frame, PaletteSlot.TitleActive, "TitleBar");
            var barRt = bar.rectTransform;
            barRt.anchorMin = new Vector2(0, 1);
            barRt.anchorMax = new Vector2(1, 1);
            barRt.pivot = new Vector2(0.5f, 1);
            barRt.sizeDelta = new Vector2(-6, RetroSkin.TitleBarHeight - 4);
            barRt.anchoredPosition = new Vector2(0, -3);

            if (icon)
            {
                var ic = RetroUi.Icon(bar.transform, icon, 16);
                var irt = ic.rectTransform;
                irt.anchorMin = irt.anchorMax = new Vector2(0, 0.5f);
                irt.pivot = new Vector2(0, 0.5f);
                irt.anchoredPosition = new Vector2(3, 0);
            }

            // The caption is clipped between the icon and the buttons - a web page title is as long as someone made it and would wrap into the content.
            var caption = RetroUi.Clipped(bar.transform);
            caption.offsetMin = new Vector2(icon ? 23 : 5, 0);
            caption.offsetMax = new Vector2(-58, 0);

            // NoWrap, not Truncate: a title bar clipped a pixel short beats one that loses its title entirely.
            titleText = RetroUi.NoWrap(RetroUi.Label(caption, title, 13, TextAnchor.MiddleLeft));
            RetroPalette.Paint(titleText, PaletteSlot.TitleText);
            titleText.fontStyle = FontStyle.Bold;
            RetroUi.Stretch(titleText.rectTransform);

            var close = TitleButton(bar.transform, -2, win.Close);
            var x = RetroUi.Label(close.transform, "X", 11, TextAnchor.MiddleCenter);
            x.fontStyle = FontStyle.Bold;
            RetroUi.Stretch(x.rectTransform);

            var maxBtn = TitleButton(bar.transform, -20, win.ToggleMaximize);
            var maxGlyph = MaxGlyph(maxBtn.transform);
            var restoreGlyph = RestoreGlyph(maxBtn.transform);
            restoreGlyph.SetActive(false);
            win.MaximizedChanged += m =>
            {
                maxGlyph.SetActive(!m);
                restoreGlyph.SetActive(m);
            };

            var minBtn = TitleButton(bar.transform, -36, win.Minimize);
            Box(minBtn.transform, 8, 2, new Vector2(-1, -3.5f));

            return bar;
        }

        static Button TitleButton(Transform bar, float x, Action onClick)
        {
            var img = RetroUi.Panel(bar, PaletteSlot.Face, "TitleBtn");
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1, 0.5f);
            rt.pivot = new Vector2(1, 0.5f);
            rt.sizeDelta = new Vector2(16, 14);
            rt.anchoredPosition = new Vector2(x, 0);
            RetroUi.Bevel(rt, true, 1);

            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());
            return btn;
        }

        // Classic dotted triangle in the bottom-right corner.
        static WindowResizeAnchor BuildResizeGrip(RectTransform frame, RectTransform host, Window win)
        {
            var grip = RetroUi.NewRect("ResizeGrip", frame);
            grip.sizeDelta = new Vector2(16, 16);

            var hit = grip.gameObject.AddComponent<Image>();
            hit.color = Color.clear;

            var anchor = grip.gameObject.AddComponent<WindowResizeAnchor>();
            anchor.targetRect = frame;
            anchor.minSize = win.MinSize;
            // Asked for as the drag happens: the desktop has no size yet here.
            anchor.maxSize = () =>
                new Vector2(host.rect.width, host.rect.height - RetroSkin.TaskbarHeight);
            anchor.onFocus = win.RequestFocus;
            anchor.onResized = win.ClampToArea;
            anchor.SetCorner(ResizeCorner.BottomRight);

            for (var i = 0; i < 3; i++)
            {
                for (var j = 0; j <= i; j++)
                {
                    var x = -2f - j * 4f;
                    var y = 2f + (i - j) * 4f;
                    Dot(grip, new Vector2(x - 1, y + 1), PaletteSlot.FaceLight);
                    Dot(grip, new Vector2(x, y), PaletteSlot.FaceDark);
                }
            }

            return anchor;
        }

        static void Dot(Transform parent, Vector2 pos, PaletteSlot slot)
        {
            var img = RetroUi.NewRect("Dot", parent).gameObject.AddComponent<Image>();
            RetroPalette.Paint(img, slot);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(1, 0);
            rt.sizeDelta = new Vector2(2, 2);
            rt.anchoredPosition = pos;
        }

        // Maximize glyph: bordered square with a thick top edge.
        static GameObject MaxGlyph(Transform parent)
        {
            var g = RetroUi.NewRect("Max", parent);
            g.sizeDelta = Vector2.zero;
            Box(g, 9, 8, Vector2.zero);
            Box(g, 7, 5, new Vector2(0, -0.5f), PaletteSlot.Face);
            return g.gameObject;
        }

        // Restore glyph: two overlapping small squares.
        static GameObject RestoreGlyph(Transform parent)
        {
            var g = RetroUi.NewRect("Restore", parent);
            g.sizeDelta = Vector2.zero;
            Box(g, 6, 5, new Vector2(1.5f, 2));
            Box(g, 4, 2, new Vector2(1.5f, 1.5f), PaletteSlot.Face);
            Box(g, 6, 5, new Vector2(-1.5f, -1.5f));
            Box(g, 4, 2, new Vector2(-1.5f, -2), PaletteSlot.Face);
            return g.gameObject;
        }

        // A glyph's ink is the outline of a drawing, not the palette's black - so the default is a colour, not a slot.
        static void Box(Transform parent, float w, float h, Vector2 pos,
            PaletteSlot slot = PaletteSlot.None)
        {
            var img = RetroUi.NewRect("Box", parent).gameObject.AddComponent<Image>();
            if (slot == PaletteSlot.None) img.color = Color.black;
            else RetroPalette.Paint(img, slot);
            img.raycastTarget = false;
            img.rectTransform.sizeDelta = new Vector2(w, h);
            img.rectTransform.anchoredPosition = pos;
        }

        void Remove(Window win)
        {
            _windows.Remove(win);
            if (Active == win) Active = null;
            Closed?.Invoke(win);

            // Windows 95 hands focus back to whatever was underneath.
            FocusTopmost();
        }

        // Focuses the frontmost window that is still open and not minimized.
        void FocusTopmost()
        {
            for (var i = _windows.Count - 1; i >= 0; i--)
            {
                var candidate = _windows[i];
                if (!candidate || candidate.Minimized) continue;
                Focus(candidate);
                return;
            }
        }

        void Advance()
        {
            _cascade += new Vector2(26, -26);
            if (_cascade.y < -160) _cascade = new Vector2(-160, 120);
        }
    }
}
