using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Deepwave.RetroOS.UI.Components
{
    // How a RetroListView lays its items out.
    public enum ListViewMode
    {
        List,      // one icon + label per row
        Details,   // columns with a header, Explorer style
        Icons      // a wrapping grid of large icons
    }

    // One row/tile. Tag carries whatever the app needs (a VfsNode, a mail...).
    public class RetroListEntry
    {
        public string Text;
        public string[] Columns;
        public Sprite Icon;
        public Color? TextColor;
        public bool Bold;
        public object Tag;
    }

    // Explorer-grade list: selection, double-click activation, three view modes,
    // optional sortable columns. Every file/mail/site list in the OS is one.
    public class RetroListView : RetroWidget
    {
        readonly List<RetroListEntry> _entries = new();
        readonly List<GameObject> _rows = new();

        ScrollRect _scroll;
        RectTransform _content;
        RectTransform _header;
        string[] _columns = Array.Empty<string>();
        float[] _columnWidths = Array.Empty<float>();
        ListViewMode _mode = ListViewMode.List;
        ListViewMode _builtLayout = (ListViewMode)(-1);
        int _selected = -1;
        float _lastClickTime;
        int _lastClickIndex = -1;

        public event Action<RetroListEntry> SelectionChanged;
        public event Action<RetroListEntry> Activated;      // double-click / Enter
        public event Action<RetroListEntry, Vector2> ContextRequested;
        public event Action<int> ColumnClicked;

        public IReadOnlyList<RetroListEntry> Entries => _entries;
        public RetroListEntry Selection => _selected >= 0 && _selected < _entries.Count ? _entries[_selected] : null;

        public ListViewMode Mode
        {
            get => _mode;
            set { _mode = value; Rebuild(); }
        }

        public static RetroListView Create(Transform parent, ListViewMode mode = ListViewMode.List)
        {
            var view = Spawn<RetroListView>(parent, "ListView");
            view.Build(mode);
            return view;
        }

        void Build(ListViewMode mode)
        {
            _mode = mode;
            var frame = Background(transform, PaletteSlot.Field, "Frame");
            RetroUi.Bevel(frame.rectTransform, false, RetroSkin.BevelThickness);

            _header = RetroUi.NewRect("Header", transform);
            _header.anchorMin = new Vector2(0f, 1f);
            _header.anchorMax = new Vector2(1f, 1f);
            _header.pivot = new Vector2(0.5f, 1f);
            _header.sizeDelta = new Vector2(-6f, 18f);
            _header.anchoredPosition = new Vector2(0f, -3f);
            RetroUi.HLayout(_header.gameObject, 0, 0);
            _header.gameObject.SetActive(false);

            var body = RetroUi.NewRect("Body", transform);
            RetroUi.Stretch(body, 3f);

            _content = RetroUi.ScrollBody(body, out _scroll);

            ApplyContentLayout();
        }

        // Details mode: define the column headers and their widths (-1 = fill).
        public void SetColumns(string[] captions, float[] widths)
        {
            _columns = captions ?? Array.Empty<string>();
            _columnWidths = widths ?? Array.Empty<float>();

            for (var i = _header.childCount - 1; i >= 0; i--) Destroy(_header.GetChild(i).gameObject);
            _header.gameObject.SetActive(_columns.Length > 0 && _mode == ListViewMode.Details);

            for (var i = 0; i < _columns.Length; i++)
            {
                var index = i;
                var button = RetroPushButton.Create(_header, _columns[i], () => ColumnClicked?.Invoke(index),
                    null, BorderStyle.RaisedThin);
                var label = button.Label;
                if (label) label.alignment = TextAnchor.MiddleLeft;
                if (label) label.rectTransform.offsetMin = new Vector2(4f, 0f);
                if (i < _columnWidths.Length && _columnWidths[i] > 0f) RetroUi.Size(button, w: _columnWidths[i]);
                else RetroUi.Flexible(button);
            }
            LayoutBody();
        }

        void LayoutBody()
        {
            var body = _scroll ? (RectTransform)_scroll.transform : null;
            if (!body) return;
            var top = _header.gameObject.activeSelf ? 22f : 3f;
            body.offsetMax = new Vector2(-3f, -top);
        }

        void ApplyContentLayout()
        {
            // Rebuild only on a real mode change; swap the layout group with
            // DestroyImmediate (Unity refuses a second LayoutGroup on one object).
            if (_builtLayout == _mode && _content.GetComponent<LayoutGroup>()) return;
            _builtLayout = _mode;

            foreach (var old in _content.GetComponents<LayoutGroup>()) DestroyImmediate(old);
            var fitter = _content.GetComponent<ContentSizeFitter>();
            if (!fitter) fitter = _content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (_mode == ListViewMode.Icons)
            {
                var grid = _content.gameObject.AddComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(80f, 74f);
                grid.spacing = new Vector2(6f, 6f);
                grid.padding = new RectOffset(6, 6, 6, 6);
                grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
                grid.startAxis = GridLayoutGroup.Axis.Horizontal;
                grid.constraint = GridLayoutGroup.Constraint.Flexible;
            }
            else
            {
                var layout = RetroUi.VLayout(_content.gameObject, 0, 1);
                layout.childForceExpandHeight = false;
            }
        }

        public void Clear()
        {
            _entries.Clear();
            _selected = -1;
            Rebuild();
        }

        public RetroListEntry Add(RetroListEntry entry)
        {
            _entries.Add(entry);
            AddRow(entry, _entries.Count - 1);
            return entry;
        }

        public RetroListEntry Add(string text, Sprite icon = null, object tag = null) =>
            Add(new RetroListEntry { Text = text, Icon = icon, Tag = tag });

        public void SetItems(IEnumerable<RetroListEntry> items)
        {
            _entries.Clear();
            _entries.AddRange(items);
            Rebuild();
        }

        void Rebuild()
        {
            foreach (var row in _rows) if (row) Destroy(row);
            _rows.Clear();
            ApplyContentLayout();
            _header.gameObject.SetActive(_columns.Length > 0 && _mode == ListViewMode.Details);
            LayoutBody();
            for (var i = 0; i < _entries.Count; i++) AddRow(_entries[i], i);
        }

        void AddRow(RetroListEntry entry, int index)
        {
            var row = RetroUi.Panel(_content, Color.clear, "Row");
            var relay = row.gameObject.AddComponent<RetroPointerRelay>();

            if (_mode == ListViewMode.Icons) BuildTile(row, entry);
            else BuildRow(row, entry);

            relay.Clicked = () => OnRowClicked(index);
            relay.Entered = () =>
            {
                if (RetroSkin.HoverHighlights && index != _selected)
                    RetroPalette.PaintOff(row,
                        Color.Lerp(RetroSkin.Field, RetroSkin.Selection, 0.12f));
            };
            relay.Exited = () =>
            {
                if (index != _selected) RetroPalette.PaintOff(row, Color.clear);
            };

            // 95 selects the row under the pointer before opening its menu.
            row.gameObject.AddComponent<RetroRightClickRelay>().Clicked = pos =>
            {
                Select(index);
                ContextRequested?.Invoke(entry, pos);
            };

            _rows.Add(row.gameObject);
        }

        void BuildRow(Image row, RetroListEntry entry)
        {
            RetroUi.Size(row, h: RetroSkin.ListRowHeight);

            if (_mode == ListViewMode.Details && _columns.Length > 0)
            {
                var layout = RetroUi.HLayout(row.gameObject, 0, 0);
                layout.childForceExpandHeight = true;
                for (var c = 0; c < _columns.Length; c++)
                {
                    var cell = RetroUi.NewRect("Cell", row.transform);
                    if (c < _columnWidths.Length && _columnWidths[c] > 0f) RetroUi.Size(cell, w: _columnWidths[c]);
                    else RetroUi.Flexible(cell);

                    if (c == 0 && entry.Icon)
                    {
                        var icon = RetroUi.Icon(cell, entry.Icon, 16f);
                        var irt = icon.rectTransform;
                        irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
                        irt.pivot = new Vector2(0f, 0.5f);
                        irt.anchoredPosition = new Vector2(2f, 0f);
                    }

                    var value = c == 0 ? entry.Text
                        : entry.Columns != null && c - 1 < entry.Columns.Length ? entry.Columns[c - 1] : "";

                    // Cell contents come from disk, so the cell is a masked rect:
                    // NoWrap alone would let one column's text spill into the next.
                    var clip = RetroUi.Clipped(cell);
                    var label = RetroUi.NoWrap(RetroUi.Label(clip, value, 13, TextAnchor.MiddleLeft));
                    RetroUi.Stretch(label.rectTransform);
                    label.rectTransform.offsetMin = new Vector2(c == 0 && entry.Icon ? 22f : 4f, 0f);
                    Ink(label, entry);
                    if (entry.Bold) label.fontStyle = FontStyle.Bold;
                }
                return;
            }

            if (entry.Icon)
            {
                var icon = RetroUi.Icon(row.transform, entry.Icon, 16f);
                var irt = icon.rectTransform;
                irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
                irt.pivot = new Vector2(0f, 0.5f);
                irt.anchoredPosition = new Vector2(3f, 0f);
            }

            var text = RetroUi.NoWrap(RetroUi.Label(row.transform, entry.Text, 13, TextAnchor.MiddleLeft));
            RetroUi.Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(entry.Icon ? 23f : 5f, 0f);
            Ink(text, entry);
            if (entry.Bold) text.fontStyle = FontStyle.Bold;
        }

        void BuildTile(Image row, RetroListEntry entry)
        {
            var icon = RetroUi.Icon(row.transform, entry.Icon, 32f);
            var irt = icon.rectTransform;
            irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 1f);
            irt.pivot = new Vector2(0.5f, 1f);
            irt.anchoredPosition = new Vector2(0f, -6f);

            var text = RetroUi.Label(row.transform, entry.Text, 12, TextAnchor.UpperCenter);
            var trt = text.rectTransform;
            trt.anchorMin = new Vector2(0f, 0f);
            trt.anchorMax = new Vector2(1f, 0f);
            trt.pivot = new Vector2(0.5f, 0f);
            trt.sizeDelta = new Vector2(-4f, 30f);
            Ink(text, entry);
        }

        // Palette Text unless the caller gave a colour (a greyed contact, a hidden
        // file); only the palette one is repainted on a scheme change.
        static void Ink(Text label, RetroListEntry entry)
        {
            if (entry.TextColor.HasValue) RetroPalette.PaintOff(label, entry.TextColor.Value);
            else RetroPalette.Paint(label, PaletteSlot.Text);
        }

        void OnRowClicked(int index)
        {
            var doubleClick = index == _lastClickIndex &&
                              Time.unscaledTime - _lastClickTime < RetroClicks.Seconds;
            _lastClickIndex = index;
            _lastClickTime = Time.unscaledTime;

            Select(index);
            if (doubleClick) Activated?.Invoke(_entries[index]);
        }

        public void Select(int index)
        {
            if (index < 0 || index >= _entries.Count) return;
            for (var i = 0; i < _rows.Count; i++)
            {
                if (!_rows[i]) continue;
                var img = _rows[i].GetComponent<Image>();
                var on = i == index;

                if (on) RetroPalette.Highlight(img, PaletteSlot.Selection);
                else RetroPalette.PaintOff(img, Color.clear);

                foreach (var label in _rows[i].GetComponentsInChildren<Text>())
                    if (on) RetroPalette.Paint(label, PaletteSlot.SelectionText);
                    else Ink(label, _entries[i]);
            }
            _selected = index;
            Click();
            SelectionChanged?.Invoke(_entries[index]);
        }

        // Selects by tag; returns false when nothing matched.
        public bool SelectTag(object tag)
        {
            var index = _entries.FindIndex(e => Equals(e.Tag, tag));
            if (index < 0) return false;
            Select(index);
            return true;
        }

        public void ScrollToTop()
        {
            if (_scroll) _scroll.verticalNormalizedPosition = 1f;
        }
    }

    // Reports right-clicks with the pointer position (for context menus).
    public class RetroRightClickRelay : MonoBehaviour, IPointerClickHandler
    {
        public Action<Vector2> Clicked;

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Right) Clicked?.Invoke(e.position);
        }
    }
}
