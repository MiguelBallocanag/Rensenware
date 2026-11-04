using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Deepwave.RetroOS.Core.Storage;
using Deepwave.RetroOS.UI;
using Deepwave.RetroOS.UI.Components;

namespace Deepwave.RetroOS.Desktop
{
    // Win95 desktop icon: select on click, open on double-click, drag snaps to grid.
    public class DesktopIcon : MonoBehaviour, IPointerDownHandler, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        const float CellW = 76, CellH = 78, Margin = 8;

        RectTransform _rt, _area, _canvasRect;
        Image _labelBg;
        Action _open;

        // The owning machine's settings, held per icon so two computers do not rearrange each other's desktop.
        MachinePrefs _prefs;

        // Caption under the icon; also what "Arrange Icons by Name" sorts on.
        public string Title { get; private set; }

        // The app id (position is filed under it, not the caption - a renamed caption would move every tidied icon).
        public string Id { get; private set; }

        // Whether this icon is the one the player picked.
        public bool IsSelected { get; private set; }

        /// <summary>The selected icon on one desktop, or <c>null</c>.</summary>
        public static DesktopIcon SelectedOn(RectTransform area)
        {
            if (!area) return null;

            foreach (var icon in area.GetComponentsInChildren<DesktopIcon>(true))
                if (icon.IsSelected) return icon;

            return null;
        }

        /// <summary>Builds one shortcut in the cell <see cref="DesktopLayout.Plan"/> gave it. The cell is decided for the desktop as a whole, not here: an icon on its own cannot see who else wants the spot.</summary>
        public static DesktopIcon Create(RectTransform area, Sprite sprite, string id, string title,
            Action open, int col, int row, Func<DesktopIcon, RetroMenu> contextMenu = null,
            MachinePrefs prefs = null)
        {
            // Icon positions are one machine's: the store comes from the OS that built this desktop.
            prefs ??= RetroOs.Current?.Prefs;

            var rt = RetroUi.NewRect(title, area);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(CellW - 8, CellH - 8);
            rt.anchoredPosition = CellCenter(col, row);

            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = Color.clear;

            var icon = RetroUi.Icon(rt, sprite, 32);
            var irt = icon.rectTransform;
            irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 1);
            irt.pivot = new Vector2(0.5f, 1);
            irt.anchoredPosition = new Vector2(0, -4);

            var labelBg = RetroUi.Panel(rt, Color.clear, "LabelBg");
            var brt = labelBg.rectTransform;
            brt.anchorMin = new Vector2(0, 0);
            brt.anchorMax = new Vector2(1, 0);
            brt.pivot = new Vector2(0.5f, 0);
            brt.sizeDelta = new Vector2(0, 28);
            labelBg.raycastTarget = false;

            // White with a shadow in every scheme, like a 95 caption over an unknown wallpaper. Painted off-palette, so a repaint leaves it alone.
            var label = RetroPalette.PaintOff(
                RetroUi.Label(labelBg.transform, title, 12, TextAnchor.UpperCenter), Color.white);
            RetroUi.Stretch(label.rectTransform);
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = Color.black;
            shadow.effectDistance = new Vector2(1, -1);

            var di = rt.gameObject.AddComponent<DesktopIcon>();
            di._rt = rt;
            di._area = area;
            di._labelBg = labelBg;
            di._open = open;
            di._prefs = prefs;
            di.Title = title;
            di.Id = id;
            var canvas = area.GetComponentInParent<Canvas>();
            if (canvas) di._canvasRect = (RectTransform)canvas.transform;

            if (contextMenu != null) RetroUi.ContextMenu(di, () => contextMenu(di));
            return di;
        }

        /// <summary>Clears the selection on one desktop.</summary>
        public static void DeselectAll(RectTransform area)
        {
            if (!area) return;

            foreach (var icon in area.GetComponentsInChildren<DesktopIcon>(true))
                icon.Deselect();
        }

        void Deselect()
        {
            if (!IsSelected) return;
            IsSelected = false;
            RetroPalette.PaintOff(_labelBg, Color.clear);
        }

        public void OnPointerDown(PointerEventData e)
        {
            DeselectAll(_area);
            IsSelected = true;
            RetroPalette.Highlight(_labelBg, PaletteSlot.TitleActive);
        }

        public void OnPointerClick(PointerEventData e)
        {
            // Left button only: a double right-click opens the context menu
            // twice, it does not launch the program.
            if (e.button != PointerEventData.InputButton.Left) return;

            // Counted here, not read off the event: uGUI times its own clickCount
            // by a constant nothing can reach, and the Control Panel's double-click
            // speed has to mean the same thing on an icon as it does in a list.
            if (RetroClicks.IsDouble(this)) _open?.Invoke();
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            transform.SetAsLastSibling();
        }

        // Follow the cursor but stay inside the desktop while dragging.
        public void OnDrag(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            _rt.anchoredPosition += RetroUi.LocalPointerDelta(e, _canvasRect);
            ClampWithinArea();
        }

        // On release, snap to the grid; if that cell is taken, take the nearest
        // free one so two icons never stack.
        public void OnEndDrag(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            var (cols, rows) = GridSize();
            var cell = ClampCell(CellOf(_rt.anchoredPosition), cols, rows);
            cell = NearestFreeCell(cell, cols, rows);
            _rt.anchoredPosition = CellCenter(cell.x, cell.y);
            Remember();
        }

        // --- where the player left them ---
        // A tidied desktop stays tidied. The cell is written down, not the position, so icons return to the same places on a different screen size. Reading them back is DesktopLayout's job.

        // Arranging a whole desktop writes every icon down; save once at the
        // end of that rather than once per icon.
        void Remember(bool save = true)
        {
            if (_prefs == null) return;

            var cell = CellOf(_rt.anchoredPosition);
            _prefs.Set(DesktopLayout.KeyFor(Id), cell.x + "," + cell.y);
            if (save) _prefs.Save();
        }

        // Runs the icon's action, as a double-click or a menu "Open" does.
        public void Open() => _open?.Invoke();

        // Snaps every icon on the desktop back onto the grid, keeping the
        // column each one already sits nearest to ("Line up Icons").
        public static void LineUp(RectTransform area)
        {
            if (!area) return;

            MachinePrefs store = null;
            foreach (Transform child in area)
            {
                var icon = child.GetComponent<DesktopIcon>();
                if (!icon) continue;
                var (cols, rows) = icon.GridSize();
                var cell = ClampCell(CellOf(icon._rt.anchoredPosition), cols, rows);
                icon._rt.anchoredPosition = CellCenter(cell.x, cell.y);
                icon.Remember(save: false);
                store ??= icon._prefs;
            }

            store?.Save();
        }

        // Refills the grid from the top-left in the given order, filling each
        // column top to bottom the way 95 does ("Arrange Icons").
        public static void Arrange(RectTransform area, Comparison<DesktopIcon> order = null)
        {
            if (!area) return;

            var icons = new List<DesktopIcon>();
            foreach (Transform child in area)
            {
                var icon = child.GetComponent<DesktopIcon>();
                if (icon) icons.Add(icon);
            }
            if (icons.Count == 0) return;

            if (order != null) icons.Sort(order);

            var (_, rows) = GridOf(area.rect.width, area.rect.height);
            for (var i = 0; i < icons.Count; i++)
            {
                icons[i]._rt.anchoredPosition = CellCenter(i / rows, i % rows);
                icons[i].Remember(save: false);
            }

            icons[0]._prefs?.Save();
        }

        /// <summary>Pulls every icon back onto a desktop that has changed size.</summary>
        public static void KeepOnScreen(RectTransform area)
        {
            if (!area || area.rect.width <= 1f || area.rect.height <= 1f) return;

            foreach (Transform child in area)
                if (child.GetComponent<DesktopIcon>() is { } icon) icon.ClampWithinArea();
        }

        void ClampWithinArea()
        {
            var halfW = _rt.rect.width * 0.5f;
            var iconH = _rt.rect.height;
            var maxX = Mathf.Max(halfW, _area.rect.width - halfW);
            var minY = Mathf.Min(0f, iconH - _area.rect.height);

            var pos = _rt.anchoredPosition;
            pos.x = Mathf.Clamp(pos.x, halfW, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, 0f);
            _rt.anchoredPosition = pos;
        }

        /// <summary>How many columns and rows of icons an area that size holds. The one definition of the grid: a desktop that lays icons out on a different one puts them on rows nothing can be dropped onto.</summary>
        public static (int Cols, int Rows) GridOf(float width, float height) => (
            Mathf.Max(1, Mathf.FloorToInt((width - Margin) / CellW)),
            Mathf.Max(1, Mathf.FloorToInt((height - Margin) / CellH)));

        (int cols, int rows) GridSize() => GridOf(_area.rect.width, _area.rect.height);

        // Searches out in rings from the wanted cell for the first empty one.
        Vector2Int NearestFreeCell(Vector2Int wanted, int cols, int rows)
        {
            var taken = OccupiedCells();
            if (!taken.Contains(wanted)) return wanted;

            for (var r = 1; r <= cols + rows; r++)
                for (var dx = -r; dx <= r; dx++)
                    for (var dy = -r; dy <= r; dy++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        var c = new Vector2Int(wanted.x + dx, wanted.y + dy);
                        if (c.x < 0 || c.x >= cols || c.y < 0 || c.y >= rows) continue;
                        if (!taken.Contains(c)) return c;
                    }
            return wanted; // desktop full: give up and overlap
        }

        HashSet<Vector2Int> OccupiedCells()
        {
            var set = new HashSet<Vector2Int>();
            foreach (Transform child in _area)
            {
                if (child == transform) continue;
                var icon = child.GetComponent<DesktopIcon>();
                if (icon) set.Add(CellOf(icon._rt.anchoredPosition));
            }
            return set;
        }

        static Vector2Int CellOf(Vector2 pos) => new(
            Mathf.RoundToInt((pos.x - Margin - CellW / 2f) / CellW),
            Mathf.RoundToInt((-pos.y - Margin) / CellH));

        static Vector2Int ClampCell(Vector2Int c, int cols, int rows) =>
            new(Mathf.Clamp(c.x, 0, cols - 1), Mathf.Clamp(c.y, 0, rows - 1));

        static Vector2 CellCenter(int col, int row) =>
            new(Margin + col * CellW + CellW / 2, -(Margin + row * CellH));
    }
}
