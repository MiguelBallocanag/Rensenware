using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Deepwave.RetroOS.Audio;

namespace Deepwave.RetroOS.UI
{
    // Code-only factory for flat Win95 widgets (no prefabs, no animation).
    public static partial class RetroUi
    {
        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(RectTransform rt, float pad = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
            return rt;
        }

        /// <summary>Whether a pointer event is the primary (left) button. Every OS activation is primary; right-click is for context menus.</summary>
        public static bool IsPrimary(PointerEventData e) =>
            e == null || e.button == PointerEventData.InputButton.Left;

        /// <summary>Whether a text field has the keyboard. Code that reads keys directly must check this.</summary>
        public static bool IsTyping()
        {
            var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            return selected && selected.GetComponent<InputField>();
        }

        /// <summary>The OS work area a menu, tool-tip or fly-out should be built into - inside the machine's CRT warp and a sibling of the window/taskbar layers.</summary>
        public static RectTransform FloatingLayer(RectTransform element)
        {
            if (!element) return null;

            var canvas = element.GetComponentInParent<Canvas>();
            if (!canvas) return null;

            var root = (RectTransform)canvas.rootCanvas.transform;

            // The outermost rect that is still inside the canvas.
            var layer = element;
            while (layer.parent is RectTransform parent && parent != root) layer = parent;
            return layer == element ? root : layer;
        }

        /// <summary>The camera to convert screen points with for this element's canvas, or <c>null</c> for a screen-space overlay (where a real camera misplaces things).</summary>
        public static Camera CanvasCamera(Component element)
        {
            var canvas = element ? element.GetComponentInParent<Canvas>() : null;
            if (!canvas || canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
            return canvas.worldCamera ? canvas.worldCamera : Camera.main;
        }

        // Pointer movement in canvas-local units, so windows track the cursor on
        // an overlay or a world-space (3D monitor) canvas alike.
        public static Vector2 LocalPointerDelta(PointerEventData e, RectTransform area)
        {
            if (!area) return e.delta;
            var cam = e.pressEventCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(area, e.position, cam, out var now) &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(area, e.position - e.delta, cam, out var prev))
                return now - prev;
            return e.delta;
        }

        /// <summary>A flat rect painted a caller colour (transparent, a card face). Not repainted by a scheme change.</summary>
        public static Image Panel(Transform parent, Color color, string name = "Panel")
        {
            var img = NewRect(name, parent).gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        /// <summary>A flat rect painted from the palette; the slot is recorded so a scheme change repaints it as the same thing.</summary>
        public static Image Panel(Transform parent, PaletteSlot slot, string name = "Panel") =>
            RetroPalette.Paint(NewRect(name, parent).gameObject.AddComponent<Image>(), slot);

        public static Text Label(Transform parent, string text, int size = 14,
            TextAnchor anchor = TextAnchor.UpperLeft, Font font = null)
        {
            var t = NewRect("Label", parent).gameObject.AddComponent<Text>();
            t.text = text;
            t.font = font ? font : RetroSkin.Ui;
            t.fontSize = size;
            RetroPalette.Paint(t, PaletteSlot.Text);
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = false;
            t.raycastTarget = false;
            return t;
        }

        // Every icon comes through here: painted at draw size, not resampled into it.
        public static Image Icon(Transform parent, Sprite sprite, float size)
        {
            var img = NewRect("Icon", parent).gameObject.AddComponent<Image>();
            img.sprite = RetroIcons.AtSize(sprite, Mathf.RoundToInt(size));
            img.raycastTarget = false;
            img.rectTransform.sizeDelta = new Vector2(size, size);

            // Painted square at a pixel size, so never draw it at another aspect
            // (a layout group can otherwise stretch it into an ellipse).
            img.preserveAspect = true;
            return img;
        }

        // Raised gray push button; optional 16px icon pins the label left.
        // The caption never wraps (uGUI would drop it into a second line and lose it).
        public static Button Button(Transform parent, string text, Action onClick, Sprite icon = null)
        {
            var img = Panel(parent, PaletteSlot.Face, "Button");
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            Bevel(img.rectTransform, true);

            var label = NoWrap(Label(img.transform, text, 14, TextAnchor.MiddleCenter));
            Stretch(label.rectTransform);
            if (icon)
            {
                var ic = Icon(img.transform, icon, 16);
                var irt = ic.rectTransform;
                irt.anchorMin = irt.anchorMax = new Vector2(0, 0.5f);
                irt.pivot = new Vector2(0, 0.5f);
                irt.anchoredPosition = new Vector2(5, 0);
                label.alignment = TextAnchor.MiddleLeft;
                label.rectTransform.offsetMin = new Vector2(25, 0);
            }

            btn.onClick.AddListener(RetroSfx.Click);
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        // Button that fires every frame while held (pan/zoom controls).
        public static Button HoldButton(Transform parent, string text, Action<float> whileHeld)
        {
            var btn = Button(parent, text, null);
            var repeat = btn.gameObject.AddComponent<RepeatPress>();
            repeat.WhileHeld = whileHeld;
            return btn;
        }

        // Underlined-blue hyperlink row (uGUI Text has no underline; colour + hover invert).
        public static Button Link(Transform parent, string text, Action onClick)
        {
            var bg = Panel(parent, Color.clear, "Link");
            var btn = bg.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = bg;

            var label = RetroPalette.Paint(
                Label(bg.transform, "> " + text, 14, TextAnchor.MiddleLeft), PaletteSlot.Link);
            Stretch(label.rectTransform, 2);

            bg.gameObject.AddComponent<HoverHighlight>().Init(bg, label,
                PaletteSlot.None, PaletteSlot.Link);
            btn.onClick.AddListener(RetroSfx.Click);
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        // Flat menu/list row: icon + label, navy highlight on hover.
        public static (Button button, Text label) ItemButton(Transform parent, Sprite icon, string text,
            Action onClick, PaletteSlot bgSlot, PaletteSlot textSlot)
        {
            var bg = bgSlot == PaletteSlot.None
                ? Panel(parent, Color.clear, "Item")
                : Panel(parent, bgSlot, "Item");
            var btn = bg.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = bg;
            btn.onClick.AddListener(RetroSfx.Click);
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            if (icon)
            {
                var ic = Icon(bg.transform, icon, 16);
                var irt = ic.rectTransform;
                irt.anchorMin = irt.anchorMax = new Vector2(0, 0.5f);
                irt.pivot = new Vector2(0, 0.5f);
                irt.anchoredPosition = new Vector2(4, 0);
            }

            var label = RetroPalette.Paint(
                Label(bg.transform, text, 13, TextAnchor.MiddleLeft), textSlot);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(24, 0);

            bg.gameObject.AddComponent<HoverHighlight>().Init(bg, label, bgSlot, textSlot);
            return (btn, label);
        }

        // Decorative Win95 menu bar (File Edit View ...).
        public static RectTransform MenuBar(Transform parent, params string[] items)
        {
            var row = NewRect("MenuBar", parent);
            Size(row, h: RetroSkin.MenuBarHeight);
            HLayout(row.gameObject, 12, 4);
            foreach (var item in items) Label(row, item, 13, TextAnchor.MiddleLeft);
            return row;
        }

        // Sunken 2px horizontal divider.
        public static RectTransform Separator(Transform parent)
        {
            var row = NewRect("Separator", parent);
            Size(row, h: 2);
            var dark = Panel(row, PaletteSlot.FaceDark, "Dark").rectTransform;
            dark.anchorMin = new Vector2(0, 0.5f);
            dark.anchorMax = Vector2.one;
            dark.offsetMin = dark.offsetMax = Vector2.zero;
            var light = Panel(row, PaletteSlot.FaceLight, "Light").rectTransform;
            light.anchorMin = Vector2.zero;
            light.anchorMax = new Vector2(1, 0.5f);
            light.offsetMin = light.offsetMax = Vector2.zero;
            return row;
        }

        // Sunken status strip at a window bottom.
        public static Text StatusBar(Transform parent, string text)
        {
            var box = Panel(parent, PaletteSlot.Face, "StatusBar");
            Size(box, h: RetroSkin.StatusBarHeight);
            Bevel(box.rectTransform, false, 1);
            var label = Label(box.transform, text, 12, TextAnchor.MiddleLeft);
            Stretch(label.rectTransform, 4);
            return label;
        }

        public static InputField Input(Transform parent, string text = "", bool password = false, Font font = null)
        {
            var img = Panel(parent, PaletteSlot.Field, "Input");
            Bevel(img.rectTransform, false);
            var input = img.gameObject.AddComponent<InputField>();
            var txt = Label(img.transform, "", 14, TextAnchor.MiddleLeft, font);
            Stretch(txt.rectTransform, 4);
            input.textComponent = txt;
            input.targetGraphic = img;
            input.text = text;
            if (password) input.contentType = InputField.ContentType.Password;
            return input;
        }

        public static Image Picture(Transform parent, Sprite sprite)
        {
            var img = NewRect("Picture", parent).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            return img;
        }

        public static RawImage Raw(Transform parent)
        {
            var raw = NewRect("Raw", parent).gameObject.AddComponent<RawImage>();
            raw.color = Color.white;
            return raw;
        }

        // Vertical scroll area; returns the content transform to fill. Comes with a
        // scroll bar that hides when content fits. Pass scrollbar: false to page it yourself.
        public static RectTransform VScroll(Transform parent, out ScrollRect scroll,
            bool autoLayout = true, bool scrollbar = true)
        {
            var root = Panel(parent, PaletteSlot.Field, "Scroll");
            Bevel(root.rectTransform, false);

            // Inside the border, not on it: a self-hiding scroll bar drives the
            // viewport offsets, which would lose anything inset on the viewport.
            var client = Stretch(NewRect("Client", root.transform), 2);
            var content = ScrollBody(client, out scroll, scrollbar);

            if (autoLayout)
            {
                VLayout(content.gameObject, 2, 4);
                content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                    ContentSizeFitter.FitMode.PreferredSize;
            }

            return content;
        }

        /// <summary>The inside of a vertical scroll area: a clamped ScrollRect, a masked viewport, a top-hung content rect, and an optional scroll bar. Shared by <see cref="VScroll"/> and the list/tree widgets.</summary>
        /// <returns>The content rect to fill.</returns>
        public static RectTransform ScrollBody(RectTransform host, out ScrollRect scroll,
            bool scrollbar = true)
        {
            scroll = host.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24;

            var viewport = Stretch(NewRect("Viewport", host));
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport;

            var content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            scroll.content = content;

            if (scrollbar) Components.RetroScrollBar.Create(scroll);
            return content;
        }

        /// <summary>The panel painted behind a scroll area's contents (page background, terminal screen, tab frame). The ScrollRect sits on a client rect inside it, not on this.</summary>
        public static Image ScrollBackdrop(ScrollRect scroll)
        {
            if (!scroll) return null;
            var own = scroll.GetComponent<Image>();
            if (own) return own;
            var parent = scroll.transform.parent;
            return parent ? parent.GetComponent<Image>() : null;
        }

        /// <summary>The bevelled outer rect of a scroll area - the object a parent layout group sizes. Size and stretch a scroll area through this, not through its ScrollRect.</summary>
        public static RectTransform ScrollFrame(ScrollRect scroll)
        {
            if (!scroll) return null;
            var client = (RectTransform)scroll.transform;
            return client.parent as RectTransform ?? client;
        }

        public static HorizontalLayoutGroup HLayout(GameObject go, int spacing = 2, int pad = 2, bool expand = false)
        {
            // Reuse an existing group: AddComponent returns null if one is attached.
            var l = go.GetComponent<HorizontalLayoutGroup>();
            if (!l) l = go.AddComponent<HorizontalLayoutGroup>();
            l.spacing = spacing;
            l.padding = new RectOffset(pad, pad, pad, pad);
            l.childControlWidth = l.childControlHeight = true;
            l.childForceExpandWidth = expand;
            l.childForceExpandHeight = true;
            return l;
        }

        public static VerticalLayoutGroup VLayout(GameObject go, int spacing = 2, int pad = 2)
        {
            var l = go.GetComponent<VerticalLayoutGroup>();
            if (!l) l = go.AddComponent<VerticalLayoutGroup>();
            l.spacing = spacing;
            l.padding = new RectOffset(pad, pad, pad, pad);
            l.childControlWidth = l.childControlHeight = true;
            l.childForceExpandWidth = true;
            l.childForceExpandHeight = false;
            return l;
        }

        // Fixes size on one/both axes, and pins flexible size to 0 there so a
        // child layout group cannot balloon a "fixed" row.
        public static LayoutElement Size(Component c, float w = -1, float h = -1)
        {
            var le = c.gameObject.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            if (w >= 0) { le.preferredWidth = le.minWidth = w; le.flexibleWidth = 0; }
            if (h >= 0) { le.preferredHeight = le.minHeight = h; le.flexibleHeight = 0; }
            return le;
        }

        /// <summary>A rect that clips whatever runs past its edges - for a caption whose length is not ours (window title, mail subject). Put a non-wrapping label inside.</summary>
        public static RectTransform Clipped(Transform parent, float padding = 0f)
        {
            var rect = Stretch(NewRect("Clip", parent), padding);
            rect.gameObject.AddComponent<RectMask2D>();
            return rect;
        }

        // Asks for a width but gives most of it back rather than pushing its
        // neighbours out of the row - for content-counted strips (tabs, attachments).
        public static LayoutElement SoftSize(Component c, float w, float min = 34f)
        {
            var le = c.gameObject.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = w;
            le.minWidth = Mathf.Min(min, w);
            le.flexibleWidth = 0;
            return le;
        }

        public static LayoutElement Flexible(Component c, float w = 1, float h = 1)
        {
            // A ScrollRect is never the object a layout group sizes (VScroll nests it),
            // so redirect to ScrollFrame or the frame collapses to its scroll bar's height.
            if (c is ScrollRect scroll) c = ScrollFrame(scroll);

            var le = c.gameObject.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = w;
            le.flexibleHeight = h;
            return le;
        }

        // Empty stretchy gap for layout rows.
        public static RectTransform Spacer(Transform parent)
        {
            var rt = NewRect("Spacer", parent);
            Flexible(rt);
            return rt;
        }

        // Which sides of a bevel to draw. A panel flush to a screen edge wants
        // only the edge facing the desktop.
        [Flags]
        public enum BevelEdges
        {
            None = 0,
            Top = 1,
            Left = 2,
            Bottom = 4,
            Right = 8,
            All = Top | Left | Bottom | Right
        }

        // Four-edge raised/sunken border, the signature Win95 look.
        public static RetroBevel Bevel(RectTransform rt, bool raised, float t = 2f,
            BevelEdges edges = BevelEdges.All)
        {
            var light = new List<Image>();
            var dark = new List<Image>();

            if ((edges & BevelEdges.Top) != 0)
                light.Add(Edge(rt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, t), new Vector2(0, -t / 2)));
            if ((edges & BevelEdges.Left) != 0)
                light.Add(Edge(rt, new Vector2(0, 0), new Vector2(0, 1), new Vector2(t, 0), new Vector2(t / 2, 0)));
            if ((edges & BevelEdges.Bottom) != 0)
                dark.Add(Edge(rt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, t), new Vector2(0, t / 2)));
            if ((edges & BevelEdges.Right) != 0)
                dark.Add(Edge(rt, new Vector2(1, 0), new Vector2(1, 1), new Vector2(t, 0), new Vector2(-t / 2, 0)));

            var bevel = rt.gameObject.AddComponent<RetroBevel>();
            bevel.Init(light.ToArray(), dark.ToArray());
            bevel.SetRaised(raised);
            return bevel;
        }

        static Image Edge(Transform parent, Vector2 aMin, Vector2 aMax, Vector2 size, Vector2 pos)
        {
            var img = NewRect("Bevel", parent).gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            img.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var rt = img.rectTransform;
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return img;
        }
    }

    // Invokes a callback every frame while the pointer holds the control.
    public class RepeatPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Action<float> WhileHeld;
        bool _held;

        public void OnPointerDown(PointerEventData e) { if (RetroUi.IsPrimary(e)) _held = true; }
        public void OnPointerUp(PointerEventData e) => _held = false;
        public void OnPointerExit(PointerEventData e) => _held = false;

        void Update()
        {
            if (_held) WhileHeld?.Invoke(Time.deltaTime);
        }

        void OnDisable() => _held = false;
    }
}
