using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Deepwave.RetroOS.Web.Html
{
    // One node of a parsed page. Text nodes have a null Tag.
    public class HtmlNode
    {
        public string Tag;                       // lower case, null for text
        public string Text;                      // text nodes only
        public readonly Dictionary<string, string> Attributes =
            new(StringComparer.OrdinalIgnoreCase);
        public readonly List<HtmlNode> Children = new();
        public HtmlNode Parent;

        public bool IsText => Tag == null;

        public string Id => Attr("id");
        public string[] Classes => (Attr("class") ?? "")
            .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        public HtmlNode(string tag) => Tag = tag;

        public static HtmlNode TextNode(string text) => new(null) { Text = text };

        public HtmlNode Add(HtmlNode child)
        {
            child.Parent = this;
            Children.Add(child);
            return child;
        }

        public string Attr(string name) =>
            Attributes.TryGetValue(name, out var value) ? value : null;

        public bool Has(string name) => Attributes.ContainsKey(name);

        // Depth-first walk including this node.
        public IEnumerable<HtmlNode> Descendants()
        {
            yield return this;
            foreach (var child in Children)
                foreach (var node in child.Descendants())
                    yield return node;
        }

        public HtmlNode Find(string tag) => Descendants()
            .FirstOrDefault(n => string.Equals(n.Tag, tag, StringComparison.OrdinalIgnoreCase));

        public IEnumerable<HtmlNode> FindAll(string tag) => Descendants()
            .Where(n => string.Equals(n.Tag, tag, StringComparison.OrdinalIgnoreCase));

        // All text under this node, whitespace-collapsed.
        public string InnerText
        {
            get
            {
                var sb = new StringBuilder();
                Collect(this, sb);
                return sb.ToString().Trim();
            }
        }

        static void Collect(HtmlNode node, StringBuilder sb)
        {
            if (node.IsText) { sb.Append(node.Text); return; }
            if (node.Tag is "script" or "style") return;
            foreach (var child in node.Children) Collect(child, sb);
        }

        public override string ToString() => IsText ? $"\"{Text}\"" : $"<{Tag}>";
    }
}
