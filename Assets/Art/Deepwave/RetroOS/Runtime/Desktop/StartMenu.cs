using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Desktop
{
    // Win95 Start menu. The main panel auto-fits, so it never overflows; programs live
    // in a "Programs" fly-out. Fly-outs clamp to the work area and scroll when too tall.
    public class StartMenu : MonoBehaviour
    {
        RetroOsContext _os;
        RectTransform _root;      // stretches the overlay (blocker + panel)
        RectTransform _panel;     // the compact main menu
        RetroPopup _popup;        // the panel's grow-out-of-the-taskbar transition
        readonly List<GameObject> _flyouts = new();   // open submenus, outermost first
        bool _open;

        public bool IsOpen => _open;

        public static StartMenu Create(RetroOsContext os)
        {
            var go = new GameObject("StartMenu", typeof(RectTransform));
            go.transform.SetParent(os.Overlay, false);
            var menu = go.AddComponent<StartMenu>();
            menu.Build(os);
            return menu;
        }

        void Build(RetroOsContext os)
        {
            _os = os;
            _root = RetroUi.Stretch((RectTransform)transform);

            // Click-catcher behind the menu: clicking anywhere else closes it.
            var blocker = RetroUi.Panel(transform, new Color(0, 0, 0, 0), "Blocker");
            RetroUi.Stretch(blocker.rectTransform);
            var blockerBtn = blocker.gameObject.AddComponent<Button>();
            blockerBtn.transition = Selectable.Transition.None;
            blockerBtn.onClick.AddListener(Close);

            BuildPanel();
            gameObject.SetActive(false);
        }

        void BuildPanel()
        {
            var panel = RetroUi.Panel(transform, PaletteSlot.Face, "StartPanel");
            RetroUi.Bevel(panel.rectTransform, true);
            _panel = panel.rectTransform;
            _panel.anchorMin = _panel.anchorMax = _panel.pivot = new Vector2(0, 0);
            _panel.sizeDelta = new Vector2(200, 0);
            _panel.anchoredPosition = new Vector2(2, RetroSkin.TaskbarHeight + 2);

            // Auto-fit height to the rows; leave the left column for the banner.
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 1;
            layout.padding = new RectOffset(30, 3, 4, 4);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            BuildBanner(panel.transform);

            Submenu(RetroIcons.Flag, "Programs", FillApps);
            if (_os.Drives.Drives.Any(d => d.Removable))
                Submenu(RetroIcons.Disc, "Removable Drives", FillDrives);

            RetroUi.Separator(_panel);
            if (ShowsLogOff()) Leaf(RetroIcons.Get(BuiltinIcon.Key), "Log Off...", LogOff);
            Leaf(RetroIcons.Computer, "Shut Down...", Shutdown);
        }

        // Logging off only makes sense when the machine can ask who you are.
        bool ShowsLogOff()
        {
            if (_os.Accounts.Count == 0) return false;
            var mode = _os.Login?.mode ?? Data.LoginMode.Automatic;
            return mode != Data.LoginMode.SkipAlways;
        }

        // The signature vertical "RetroOS 95" band down the left edge.
        void BuildBanner(Transform panel)
        {
            var banner = RetroUi.Panel(panel, PaletteSlot.TitleActive, "Banner");
            banner.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var brt = banner.rectTransform;
            brt.anchorMin = new Vector2(0, 0);
            brt.anchorMax = new Vector2(0, 1);
            brt.pivot = new Vector2(0, 0.5f);
            brt.sizeDelta = new Vector2(24, -6);
            brt.anchoredPosition = new Vector2(3, 0);

            // The title-bar wash on its side: the caption reads bottom-to-top, so the title bar's right end is the top here. Flat with gradients off.
            RetroPalette.PaintGradient(banner, PaletteSlot.TitleActiveEnd, PaletteSlot.TitleActive,
                GradientDirection.Vertical);

            // The name is turned on its side; clipped to the banner it is simply cut off, like the strip down a real Start menu.
            var text = RetroUi.NoWrap(RetroUi.Label(RetroUi.Clipped(banner.transform),
                _os.OsName, 14, TextAnchor.MiddleLeft));
            RetroPalette.Paint(text, PaletteSlot.TitleText);
            text.fontStyle = FontStyle.Bold;
            var trt = text.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0);
            trt.pivot = new Vector2(0, 0.5f);
            trt.sizeDelta = new Vector2(200, 20);
            trt.anchoredPosition = new Vector2(0, 8);
            trt.localEulerAngles = new Vector3(0, 0, 90);
        }

        // --- rows -------------------------------------------------------

        void Leaf(Sprite icon, string text, Action onClick)
        {
            var (button, _) = RetroUi.ItemButton(_panel, icon, text,
                () => { CloseFlyout(); onClick(); Close(); }, PaletteSlot.Face, PaletteSlot.Text);
            RetroUi.Size(button, h: 26);
            HoverAction.Add(button.gameObject, () => CloseFlyout());
        }

        void Submenu(Sprite icon, string text, Action<Transform> fill)
        {
            var item = RetroUi.NewRect("Submenu", _panel);
            RetroUi.Size(item, h: 26);
            var (button, _) = RetroUi.ItemButton(item, icon, text,
                () => OpenFlyout((RectTransform)item, fill), PaletteSlot.Face, PaletteSlot.Text);
            RetroUi.Stretch(button.GetComponent<RectTransform>());

            var arrow = RetroUi.Label(button.transform, "»", 14, TextAnchor.MiddleRight);
            RetroPalette.Paint(arrow, PaletteSlot.Text);
            RetroUi.Stretch(arrow.rectTransform);
            arrow.rectTransform.offsetMax = new Vector2(-6, 0);

            HoverAction.Add(button.gameObject, () => OpenFlyout((RectTransform)item, fill));
        }

        // --- fly-outs ---------------------------------------------------

        // Programs grouped like 95: related ones into a submenu (Accessories, Games), the rest directly under Programs.
        void FillApps(Transform content)
        {
            var groups = new List<string>();
            foreach (var app in _os.Apps.All)
            {
                if (!app.ShowInStartMenu) continue;
                var category = app.Category?.Trim();
                if (string.IsNullOrEmpty(category)) continue;
                if (!groups.Contains(category, StringComparer.OrdinalIgnoreCase)) groups.Add(category);
            }

            foreach (var group in groups)
            {
                var name = group;
                FlyoutSubmenu(content, RetroIcons.FolderOpen, name,
                    inner => FillCategory(inner, name));
            }

            if (groups.Count > 0) RetroUi.Separator((RectTransform)content);

            foreach (var app in _os.Apps.All)
            {
                if (!app.ShowInStartMenu) continue;
                if (!string.IsNullOrWhiteSpace(app.Category)) continue;
                var a = app;
                FlyoutItem(content, a.IconSprite, a.Title, () => _os.Apps.Launch(a));
            }
        }

        void FillCategory(Transform content, string category)
        {
            foreach (var app in _os.Apps.All)
            {
                if (!app.ShowInStartMenu) continue;
                if (!string.Equals(app.Category?.Trim(), category, StringComparison.OrdinalIgnoreCase))
                    continue;
                var a = app;
                FlyoutItem(content, a.IconSprite, a.Title, () => _os.Apps.Launch(a));
            }
        }

        // A row inside a fly-out that opens a further one beside it.
        void FlyoutSubmenu(Transform content, Sprite icon, string text, Action<Transform> fill)
        {
            var item = RetroUi.NewRect("Submenu", content);
            RetroUi.Size(item, h: 24);
            var (button, _) = RetroUi.ItemButton(item, icon, text,
                () => OpenFlyout((RectTransform)item, fill, depth: 1),
                PaletteSlot.Face, PaletteSlot.Text);
            RetroUi.Stretch(button.GetComponent<RectTransform>());

            var arrow = RetroUi.Label(button.transform, "»", 13, TextAnchor.MiddleRight);
            RetroPalette.Paint(arrow, PaletteSlot.Text);
            RetroUi.Stretch(arrow.rectTransform);
            arrow.rectTransform.offsetMax = new Vector2(-6, 0);

            HoverAction.Add(button.gameObject, () => OpenFlyout((RectTransform)item, fill, depth: 1));
        }

        // The floppy and CD drives, with whatever is currently in them.
        void FillDrives(Transform content)
        {
            foreach (var drive in _os.Drives.Drives.Where(d => d.Removable))
            {
                var letter = drive.Letter;
                var caption = drive.Ready
                    ? $"Eject {drive.DisplayName}"
                    : $"({letter}:) no disk";
                FlyoutItem(content, RetroIcons.Disc, caption,
                    drive.Ready ? () => _os.Drives.Eject(letter) : null);
            }
        }

        void FlyoutItem(Transform content, Sprite icon, string text, Action onClick)
        {
            var (button, _) = RetroUi.ItemButton(content, icon, text,
                () => { onClick(); Close(); }, PaletteSlot.Face, PaletteSlot.Text);
            RetroUi.Size(button, h: 24);
        }

        void OpenFlyout(RectTransform anchorItem, Action<Transform> fill, int depth = 0)
        {
            // Opening at a depth closes whatever was open at that depth and below, so hovering a sibling replaces its neighbour's submenu.
            CloseFlyout(depth);

            var panel = RetroUi.Panel(transform, PaletteSlot.Face, "Flyout");
            RetroUi.Bevel(panel.rectTransform, true);
            var prt = panel.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0, 0);
            prt.pivot = new Vector2(0, 1);
            prt.sizeDelta = new Vector2(190, 100);

            // A scrolling item list so many programs never overflow the screen.
            var scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24;

            var viewport = RetroUi.NewRect("Viewport", panel.transform);
            RetroUi.Stretch(viewport, 3);
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport;

            var content = RetroUi.NewRect("Items", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            scroll.content = content;
            var v = RetroUi.VLayout(content.gameObject, 1, 2);
            v.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            fill(content);

            // Size the panel to its content, clamped to the available height.
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            var maxH = Mathf.Max(60f, _root.rect.height - RetroSkin.TaskbarHeight - 8f);
            var h = Mathf.Min(content.rect.height + 6f, maxH);
            prt.sizeDelta = new Vector2(190, h);

            PositionFlyout(prt, anchorItem);

            // Pivoted at its top edge, so it unrolls downward out of the row
            // that opened it.
            RetroPopup.Play(panel.gameObject, PopupAnimation.Scale, 18f);
            _flyouts.Add(panel.gameObject);
        }

        // Place the fly-out beside its parent row, flipping to stay on screen. Screen<->local conversion, so it works on overlay and world-space alike.
        void PositionFlyout(RectTransform panel, RectTransform item)
        {
            var cam = RetroUi.CanvasCamera(this);
            var corners = new Vector3[4];
            item.GetWorldCorners(corners); // 0 BL, 1 TL, 2 TR, 3 BR

            Vector2 Local(Vector3 world)
            {
                var screen = RectTransformUtility.WorldToScreenPoint(cam, world);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, cam, out var lp);
                return new Vector2(lp.x - _root.rect.xMin, lp.y - _root.rect.yMin);
            }

            var right = Local(corners[2]); // top-right of the row
            var width = panel.sizeDelta.x;
            var height = panel.sizeDelta.y;

            var x = right.x;
            var pivotX = 0f;
            if (x + width > _root.rect.width)        // no room on the right: flip left
            {
                x = Local(corners[1]).x;             // top-left of the row
                pivotX = 1f;
            }

            var y = right.y;                          // align tops
            if (y - height < 0f) y = height;          // don't run past the bottom
            y = Mathf.Min(y, _root.rect.height);

            panel.pivot = new Vector2(pivotX, 1f);
            panel.anchoredPosition = new Vector2(x, y);
        }

        void CloseFlyout(int fromDepth = 0)
        {
            for (var i = _flyouts.Count - 1; i >= fromDepth; i--)
            {
                if (_flyouts[i]) Destroy(_flyouts[i]);
                _flyouts.RemoveAt(i);
            }
        }

        // --- open / close ----------------------------------------------

        public void Toggle() { if (IsOpen) Close(); else Open(); }

        public void Open()
        {
            _open = true;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            CloseFlyout();

            // Anchored at its bottom-left corner on the taskbar, so it grows up
            // out of the Start button.
            if (!_popup) _popup = _panel.gameObject.AddComponent<RetroPopup>();
            _popup.mode = PopupAnimation.Scale;
            _popup.speed = 16f;
            _popup.deactivateOnHide = false;
            _popup.Hidden -= Hide;
            _popup.Hidden += Hide;
            _popup.PlayIn();
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            CloseFlyout();

            // The menu stops taking clicks as it folds, so the click that closed it does not land on it a second time.
            if (_popup) _popup.PlayOut();
            else gameObject.SetActive(false);
        }

        void Hide()
        {
            if (!_open) gameObject.SetActive(false);
        }

        // The menu folds away first, so the question is asked over the
        // desktop rather than over a Start menu still standing open behind it.
        void LogOff()
        {
            Close();
            _os.RequestLogout();
        }

        void Shutdown()
        {
            Close();
            _os.RequestShutdown();
        }
    }

    // Fires a callback whenever the pointer enters the row (menu hover-open).
    public class HoverAction : MonoBehaviour, IPointerEnterHandler
    {
        Action _onEnter;

        public static void Add(GameObject go, Action onEnter) =>
            go.AddComponent<HoverAction>()._onEnter = onEnter;

        public void OnPointerEnter(PointerEventData eventData) => _onEnter?.Invoke();
    }
}
