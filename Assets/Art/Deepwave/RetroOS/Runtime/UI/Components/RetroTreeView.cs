using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Deepwave.RetroOS.UI.Components
{
    // A node in a RetroTreeView. Children may be given up front or built lazily
    // on first expand (so the file explorer never walks a huge disk at once).
    public class RetroTreeNode
    {
        public string Text;
        public Sprite Icon;
        public Sprite OpenIcon;
        public object Tag;
        public bool Expanded;
        public List<RetroTreeNode> Children;
        public Func<List<RetroTreeNode>> LoadChildren;

        public bool HasChildren =>
            (Children != null && Children.Count > 0) || LoadChildren != null;

        public List<RetroTreeNode> Resolve()
        {
            if (Children == null && LoadChildren != null) Children = LoadChildren();
            return Children ??= new List<RetroTreeNode>();
        }
    }

    // Explorer's left-hand pane: indented rows with [+]/[-] boxes and dotted
    // connector lines.
    public class RetroTreeView : RetroWidget
    {
        const float Indent = 16f;

        readonly List<RetroTreeNode> _roots = new();
        readonly Dictionary<RetroTreeNode, Image> _rows = new();

        ScrollRect _scroll;
        RectTransform _content;
        RetroTreeNode _selected;

        public event Action<RetroTreeNode> SelectionChanged;
        public event Action<RetroTreeNode> Activated;

        public RetroTreeNode Selection => _selected;

        // The top-level nodes, so a caller rebuilding the tree can read the
        // state off the old one first.
        public IReadOnlyList<RetroTreeNode> Roots => _roots;

        public static RetroTreeView Create(Transform parent)
        {
            var tree = Spawn<RetroTreeView>(parent, "TreeView");
            tree.Build();
            return tree;
        }

        void Build()
        {
            var frame = Background(transform, PaletteSlot.Field, "Frame");
            RetroUi.Bevel(frame.rectTransform, false, RetroSkin.BevelThickness);

            var body = RetroUi.Stretch(RetroUi.NewRect("Body", transform), 3f);
            _content = RetroUi.ScrollBody(body, out _scroll);

            var layout = RetroUi.VLayout(_content.gameObject, 0, 2);
            layout.childForceExpandHeight = false;
            _content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
        }

        public void SetRoots(IEnumerable<RetroTreeNode> roots)
        {
            _roots.Clear();
            _roots.AddRange(roots);
            Refresh();
        }

        public void Refresh()
        {
            for (var i = _content.childCount - 1; i >= 0; i--) Destroy(_content.GetChild(i).gameObject);
            _rows.Clear();
            foreach (var node in _roots) AddNode(node, 0);
        }

        void AddNode(RetroTreeNode node, int depth)
        {
            var row = RetroUi.Panel(_content, Color.clear, "Node");
            if (node == _selected) RetroPalette.Highlight(row, PaletteSlot.Selection);
            RetroUi.Size(row, h: 18f);
            _rows[node] = row;

            var x = 2f + depth * Indent;

            if (node.HasChildren)
            {
                var toggle = RetroUi.Panel(row.transform, PaletteSlot.Field, "Toggle");
                var trt = toggle.rectTransform;
                trt.anchorMin = trt.anchorMax = new Vector2(0f, 0.5f);
                trt.pivot = new Vector2(0f, 0.5f);
                trt.sizeDelta = new Vector2(9f, 9f);
                trt.anchoredPosition = new Vector2(x, 0f);
                RetroUi.Bevel(trt, false, 1f);

                var glyph = RetroUi.Label(toggle.transform, node.Expanded ? "-" : "+", 11,
                    TextAnchor.MiddleCenter);
                RetroUi.Stretch(glyph.rectTransform);

                toggle.gameObject.AddComponent<RetroPointerRelay>().Clicked = () =>
                {
                    node.Expanded = !node.Expanded;
                    if (node.Expanded) node.Resolve();
                    Click();
                    Refresh();
                };
            }

            var iconX = x + 13f;
            var icon = node.Expanded && node.OpenIcon ? node.OpenIcon : node.Icon;
            if (icon)
            {
                var img = RetroUi.Icon(row.transform, icon, 16f);
                var irt = img.rectTransform;
                irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
                irt.pivot = new Vector2(0f, 0.5f);
                irt.anchoredPosition = new Vector2(iconX, 0f);
            }

            var label = RetroUi.NoWrap(RetroUi.Label(row.transform, node.Text, 13, TextAnchor.MiddleLeft));
            RetroUi.Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(iconX + (icon ? 19f : 2f), 0f);
            RetroPalette.Paint(label,
                node == _selected ? PaletteSlot.SelectionText : PaletteSlot.Text);

            var relay = row.gameObject.AddComponent<RetroPointerRelay>();
            relay.Clicked = () => Select(node);

            if (node.Expanded)
                foreach (var child in node.Resolve()) AddNode(child, depth + 1);
        }

        public void Select(RetroTreeNode node)
        {
            _selected = node;
            foreach (var pair in _rows)
            {
                var on = pair.Key == node;
                if (on) RetroPalette.Highlight(pair.Value, PaletteSlot.Selection);
                else RetroPalette.PaintOff(pair.Value, Color.clear);
                foreach (var label in pair.Value.GetComponentsInChildren<Text>())
                    if (label.text != "+" && label.text != "-")
                        RetroPalette.Paint(label,
                            on ? PaletteSlot.SelectionText : PaletteSlot.Text);
            }
            Click();
            SelectionChanged?.Invoke(node);
            Activated?.Invoke(node);
        }

        // Opens every ancestor of a node so it becomes visible, then selects it.
        public void Reveal(RetroTreeNode node, IEnumerable<RetroTreeNode> ancestors)
        {
            foreach (var parent in ancestors) { parent.Expanded = true; parent.Resolve(); }
            _selected = node;
            Refresh();
        }
    }
}
