using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Deepwave.RetroOS.UI.Components
{
    // One menu entry: command, checkable option, separator or submenu.
    public class RetroMenuItem
    {
        public string Caption;
        public string Shortcut;      // right-aligned hint, e.g. "Ctrl+O"
        public Sprite Icon;
        public Action Invoke;
        public bool Separator;
        public bool Checked;
        public bool Enabled = true;
        public List<RetroMenuItem> Children;
    }

    // A menu as pure data: hand its item list to a bar, button or right-click.
    public class RetroMenu
    {
        public readonly List<RetroMenuItem> Items = new();

        public RetroMenu Command(string caption, Action action, string shortcut = null, Sprite icon = null,
            bool enabled = true)
        {
            Items.Add(new RetroMenuItem
            {
                Caption = caption, Invoke = action, Shortcut = shortcut, Icon = icon, Enabled = enabled
            });
            return this;
        }

        public RetroMenu Check(string caption, bool isChecked, Action action)
        {
            Items.Add(new RetroMenuItem { Caption = caption, Checked = isChecked, Invoke = action });
            return this;
        }

        public RetroMenu Separator()
        {
            Items.Add(new RetroMenuItem { Separator = true });
            return this;
        }

        public RetroMenu Submenu(string caption, RetroMenu menu, Sprite icon = null)
        {
            Items.Add(new RetroMenuItem { Caption = caption, Icon = icon, Children = menu.Items });
            return this;
        }
    }

    // Working Win95 menu bar: rows that drop down, highlight, walk submenus, close on outside click.
    public class RetroMenuStrip : RetroWidget
    {
        readonly List<(RectTransform rect, RetroMenu menu, Image bg, Text label)> _titles = new();
        RetroMenuPopup _open;
        int _openIndex = -1;

        public static RetroMenuStrip Create(Transform parent)
        {
            var strip = Spawn<RetroMenuStrip>(parent, "MenuBar");
            strip.Build();
            return strip;
        }

        void Build()
        {
            Background(transform, PaletteSlot.Face, "Bg");
            RetroUi.HLayout(gameObject, 0, 1);
            RetroUi.Size(this, h: RetroSkin.MenuBarHeight);
        }

        // Adds a top-level title. Returns the menu to fill with commands.
        public RetroMenu AddMenu(string caption)
        {
            var menu = new RetroMenu();
            var index = _titles.Count;

            var holder = RetroUi.NewRect(caption, transform);
            var bg = RetroUi.Panel(holder, Color.clear, "Hi");
            RetroUi.Stretch(bg.rectTransform);
            var label = RetroUi.Label(bg.transform, caption, 13, TextAnchor.MiddleCenter);
            RetroUi.NoWrap(label);
            RetroUi.Stretch(label.rectTransform);

            // Crowd titles rather than run the last one off the side.
            RetroUi.SoftSize(holder, RetroUi.TextWidth(caption) + 20f, 28f);

            var trigger = bg.gameObject.AddComponent<RetroPointerRelay>();
            trigger.Clicked = () => Toggle(index);
            trigger.Entered = () => { if (_openIndex >= 0 && _openIndex != index) Toggle(index); };

            _titles.Add((holder, menu, bg, label));
            return menu;
        }

        void Toggle(int index)
        {
            if (_openIndex == index) { CloseAll(); return; }
            CloseAll();

            var (rect, menu, bg, label) = _titles[index];
            RetroPalette.Highlight(bg, PaletteSlot.Selection);
            RetroPalette.Paint(label, PaletteSlot.SelectionText);
            _openIndex = index;
            Click();

            _open = RetroMenuPopup.Show(menu.Items, rect, RetroMenuPopup.Placement.Below, CloseAll);
        }

        public void CloseAll()
        {
            if (_open) _open.Dismiss();
            _open = null;
            if (_openIndex >= 0 && _openIndex < _titles.Count)
            {
                var (_, _, bg, label) = _titles[_openIndex];
                RetroPalette.PaintOff(bg, Color.clear);
                RetroPalette.Paint(label, PaletteSlot.Text);
            }
            _openIndex = -1;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            CloseAll();
        }
    }

    // The floating panel a menu drops into. Shared by context menus and combo lists.
    public class RetroMenuPopup : MonoBehaviour
    {
        public enum Placement { Below, Beside, AtPointer }

        RectTransform _root;      // full-screen blocker layer
        RectTransform _panel;
        RetroMenuPopup _child;
        RetroMenuItem _childOwner; // the row the open submenu belongs to
        Action _clearChildRow;     // puts that row's highlight out again
        RetroMenuPopup _parent;    // null on the menu the player opened
        RetroPopup _popup;
        Action _onDismiss;
        bool _dismissed;

        /// <summary>Shows a popup anchored to <paramref name="anchor"/> in the nearest OS overlay.</summary>
        /// <param name="parent">The popup this drops out of (submenu), or <c>null</c> for the menu the player opened.</param>
        public static RetroMenuPopup Show(List<RetroMenuItem> items, RectTransform anchor,
            Placement placement, Action onDismiss, Vector2 pointer = default,
            RetroMenuPopup parent = null)
        {
            var layer = FindLayer(anchor);
            if (!layer) return null;

            var go = new GameObject("MenuPopup", typeof(RectTransform));
            go.transform.SetParent(layer, false);
            var popup = go.AddComponent<RetroMenuPopup>();
            popup._parent = parent;

            // Only the menu the player opened reports its dismissal upward.
            popup._onDismiss = parent ? null : onDismiss;
            popup.Build(items, anchor, placement, pointer);
            return popup;
        }

        // On the OS work area, so popups escape window clipping and share its CRT warp.
        static RectTransform FindLayer(RectTransform anchor) => RetroUi.FloatingLayer(anchor);

        void Build(List<RetroMenuItem> items, RectTransform anchor, Placement placement, Vector2 pointer)
        {
            _root = RetroUi.Stretch((RectTransform)transform);
            transform.SetAsLastSibling();

            // Only the root menu lays a click-catching sheet; a submenu's own sheet
            // would cover its parent's rows. The parent's sheet still catches outside clicks.
            if (!_parent)
            {
                var blocker = RetroUi.Panel(transform, new Color(0f, 0f, 0f, 0f), "Blocker");
                RetroUi.Stretch(blocker.rectTransform);
                var dismiss = blocker.gameObject.AddComponent<RetroPointerRelay>();
                dismiss.PrimaryOnly = false;  // any button outside closes the menu
                dismiss.Clicked = Dismiss;
            }

            var panel = RetroUi.Panel(transform, PaletteSlot.Face, "Panel");
            _panel = panel.rectTransform;
            RetroUi.Bevel(_panel, true, RetroSkin.BevelThickness);
            _panel.anchorMin = _panel.anchorMax = Vector2.zero;
            _panel.pivot = new Vector2(0f, 1f);

            var list = RetroUi.NewRect("Items", _panel);
            RetroUi.Stretch(list, 3f);
            var layout = RetroUi.VLayout(list.gameObject, 0, 0);
            layout.childForceExpandHeight = false;

            var width = 100f;
            foreach (var item in items)
            {
                width = Mathf.Max(width, MeasureWidth(item));
                AddRow(list, item);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(list);
            var height = 0f;
            foreach (RectTransform child in list) height += child.rect.height;
            _panel.sizeDelta = new Vector2(width, height + 6f);

            Position(anchor, placement, pointer);

            // Pivoted top-left, so scaling up unfolds the menu out of what opened it.
            _popup = RetroPopup.Play(_panel.gameObject, PopupAnimation.Scale, 16f);
        }

        static float MeasureWidth(RetroMenuItem item)
        {
            if (item.Separator) return 0f;
            var w = 40f + RetroUi.TextWidth(item.Caption);
            if (!string.IsNullOrEmpty(item.Shortcut)) w += RetroUi.TextWidth(item.Shortcut) + 24f;
            if (item.Children != null) w += 18f;
            return w;
        }

        void AddRow(Transform parent, RetroMenuItem item)
        {
            if (item.Separator)
            {
                var sep = RetroUi.NewRect("Sep", parent);
                RetroUi.Size(sep, h: 7f);
                var line = RetroUi.Separator(sep);
                line.anchorMin = new Vector2(0f, 0.5f);
                line.anchorMax = new Vector2(1f, 0.5f);
                line.sizeDelta = new Vector2(-8f, 2f);
                return;
            }

            var row = RetroUi.Panel(parent, Color.clear, "Item");
            RetroUi.Size(row, h: 20f);

            var label = RetroUi.NoWrap(RetroUi.Label(row.transform, item.Caption, 13, TextAnchor.MiddleLeft));
            RetroUi.Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(24f, 0f);
            RetroPalette.Paint(label,
                item.Enabled ? PaletteSlot.Text : PaletteSlot.TextDisabled);

            if (item.Icon)
            {
                var icon = RetroUi.Icon(row.transform, item.Icon, 16f);
                var irt = icon.rectTransform;
                irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
                irt.pivot = new Vector2(0f, 0.5f);
                irt.anchoredPosition = new Vector2(4f, 0f);
            }
            else if (item.Checked)
            {
                // A painted tick (U+2713 is missing from many fonts).
                var tick = RetroUi.Icon(row.transform, RetroTextures.Tick, 12f);
                var trt = tick.rectTransform;
                trt.anchorMin = trt.anchorMax = new Vector2(0f, 0.5f);
                trt.pivot = new Vector2(0f, 0.5f);
                trt.anchoredPosition = new Vector2(6f, 0f);
            }

            // Submenu arrow is painted too; a shortcut stays text.
            if (item.Children != null)
            {
                var more = RetroUi.Icon(row.transform, RetroTextures.Arrow(2), 16f);
                var mrt = more.rectTransform;
                mrt.anchorMin = mrt.anchorMax = new Vector2(1f, 0.5f);
                mrt.pivot = new Vector2(1f, 0.5f);
                mrt.anchoredPosition = new Vector2(-8f, 0f);
            }

            Text hint = null;
            if (!string.IsNullOrEmpty(item.Shortcut) && item.Children == null)
            {
                hint = RetroUi.NoWrap(
                    RetroUi.Label(row.transform, item.Shortcut, 13, TextAnchor.MiddleRight));
                RetroUi.Stretch(hint.rectTransform);
                hint.rectTransform.offsetMax = new Vector2(-8f, 0f);
                // Grey either way - a shortcut is secondary, like 95 drew it.
                RetroPalette.Paint(hint, PaletteSlot.TextDisabled);
            }

            void Light()
            {
                RetroPalette.Highlight(row, PaletteSlot.Selection);
                RetroPalette.Paint(label, PaletteSlot.SelectionText);
                if (hint) RetroPalette.Paint(hint, PaletteSlot.SelectionText);
            }

            void Unlight()
            {
                if (!row) return;
                RetroPalette.PaintOff(row, Color.clear);
                RetroPalette.Paint(label,
                    item.Enabled ? PaletteSlot.Text : PaletteSlot.TextDisabled);
                if (hint) RetroPalette.Paint(hint, PaletteSlot.TextDisabled);
            }

            // Opens the submenu, unless already open (reopening flickered on row crossing).
            void OpenChild()
            {
                if (item.Children == null || (_child && _childOwner == item)) return;
                CloseChild();
                _child = Show(item.Children, row.rectTransform, Placement.Beside, null,
                    parent: this);
                _childOwner = item;
                _clearChildRow = Unlight;
            }

            var relay = row.gameObject.AddComponent<RetroPointerRelay>();
            relay.Entered = () =>
            {
                if (!item.Enabled) return;
                if (item.Children == null || _childOwner != item) CloseChild();
                Light();
                OpenChild();
            };
            relay.Exited = () =>
            {
                // The row a submenu came from stays lit while it is up; CloseChild clears it.
                if (_child && _childOwner == item) return;
                Unlight();
            };
            relay.Clicked = () =>
            {
                if (!item.Enabled) return;

                // A parent item has nothing to run: a click opens its submenu (a mouse only hovers).
                if (item.Children != null) { OpenChild(); return; }

                if (RetroSkin.WidgetSounds) Audio.RetroSfx.Click();
                var action = item.Invoke;
                DismissAll();
                action?.Invoke();
            };
        }

        void Position(RectTransform anchor, Placement placement, Vector2 pointer)
        {
            var cam = RetroUi.CanvasCamera(this);
            Vector2 Local(Vector3 world)
            {
                var screen = RectTransformUtility.WorldToScreenPoint(cam, world);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, cam, out var lp);
                return new Vector2(lp.x - _root.rect.xMin, lp.y - _root.rect.yMin);
            }

            Vector2 target;
            if (placement == Placement.AtPointer)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, pointer, cam, out var lp);
                target = new Vector2(lp.x - _root.rect.xMin, lp.y - _root.rect.yMin);
            }
            else
            {
                var corners = new Vector3[4];
                anchor.GetWorldCorners(corners); // 0 BL, 1 TL, 2 TR, 3 BR
                target = placement == Placement.Below ? Local(corners[0]) : Local(corners[2]);
            }

            var size = _panel.sizeDelta;
            var x = target.x;
            var y = target.y;

            // Flip rather than overflow: a menu off the right opens left, off the
            // bottom opens up. Clamped after, for a menu bigger than the screen.
            if (x + size.x > _root.rect.width) x = Mathf.Max(0f, x - size.x);
            if (y - size.y < 0f) y = Mathf.Min(_root.rect.height, y + size.y);

            _panel.anchoredPosition = new Vector2(x, y);
        }

        void CloseChild()
        {
            if (_child) _child.Dismiss();
            _child = null;
            _childOwner = null;

            var clear = _clearChildRow;
            _clearChildRow = null;
            clear?.Invoke();
        }

        /// <summary>Closes this popup and every one it dropped out of.</summary>
        public void DismissAll()
        {
            var root = this;
            while (root._parent) root = root._parent;
            root.Dismiss();
        }

        public void Dismiss()
        {
            if (_dismissed) return;
            _dismissed = true;

            CloseChild();
            var callback = _onDismiss;
            _onDismiss = null;

            // Stop taking input at once: while it fades, a live blocker would eat the next click.
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;

            if (_popup)
            {
                _popup.Hidden += () => { if (this) Destroy(gameObject); };
                _popup.PlayOut();
            }
            else if (this)
            {
                Destroy(gameObject);
            }

            callback?.Invoke();
        }
    }

    // Small pointer plumbing shared by menu rows, list rows and popups.
    public class RetroPointerRelay : MonoBehaviour,
        IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public Action Clicked;
        public Action Entered;
        public Action Exited;

        // Every OS activation is a primary-button action; firing on any button let a
        // right-click do the thing AND open the context menu. A dismiss-blocker sets this false.
        public bool PrimaryOnly = true;

        public void OnPointerClick(PointerEventData e)
        {
            if (PrimaryOnly && e.button != PointerEventData.InputButton.Left) return;
            Clicked?.Invoke();
        }
        public void OnPointerEnter(PointerEventData e) => Entered?.Invoke();
        public void OnPointerExit(PointerEventData e) => Exited?.Invoke();
    }

    // Fires on a double primary click - the era's "open"/"fill window" gesture.
    public class RetroDoubleClick : MonoBehaviour, IPointerClickHandler
    {
        public Action Clicked;

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            if (RetroClicks.IsDouble(this)) Clicked?.Invoke();
        }
    }

    // Right-click menus for any element.
    public class RetroContextMenu : MonoBehaviour, IPointerClickHandler
    {
        Func<RetroMenu> _build;

        public static RetroContextMenu Attach(GameObject target, Func<RetroMenu> build)
        {
            var menu = target.GetComponent<RetroContextMenu>() ?? target.AddComponent<RetroContextMenu>();
            menu._build = build;
            return menu;
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Right || _build == null) return;
            var menu = _build();
            if (menu == null || menu.Items.Count == 0) return;
            RetroMenuPopup.Show(menu.Items, (RectTransform)transform,
                RetroMenuPopup.Placement.AtPointer, null, e.position);
        }
    }
}
