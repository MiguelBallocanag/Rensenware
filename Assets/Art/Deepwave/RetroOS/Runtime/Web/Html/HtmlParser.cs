using System;
using System.Collections.Generic;
using System.Text;

namespace Deepwave.RetroOS.Web.Html
{
    // A small, forgiving HTML parser: the amount of HTML a 1995 browser understood -
    // tags, attributes, entities, comments, void elements, implicit closes for p/li/br/tr/td.
    // Unknown markup is kept as a plain block element, so it degrades instead of breaking.
    public static class HtmlParser
    {
        // Nesting cap: the cost is the recursive renderer, not the tree. Content past the
        // cap is kept, flattened into the deepest element still open.
        const int MaxDepth = 256;

        static readonly HashSet<string> Void = new(StringComparer.OrdinalIgnoreCase)
        {
            "br", "hr", "img", "input", "meta", "link", "base", "col", "embed",
            "source", "track", "wbr", "area", "param"
        };

        // Tags that implicitly close an open tag of the same "level".
        static readonly Dictionary<string, string[]> AutoClose = new(StringComparer.OrdinalIgnoreCase)
        {
            ["p"] = new[] { "p" },
            ["li"] = new[] { "li" },
            ["tr"] = new[] { "tr", "td", "th" },
            ["td"] = new[] { "td", "th" },
            ["th"] = new[] { "td", "th" },
            ["dt"] = new[] { "dt", "dd" },
            ["dd"] = new[] { "dt", "dd" },
            ["option"] = new[] { "option" }
        };

        // How far an implicit close may reach: a <tr> in a nested table must close that
        // table's row, not walk out through it (90s pages are built from nested tables).
        static readonly Dictionary<string, string[]> AutoCloseScope = new(StringComparer.OrdinalIgnoreCase)
        {
            ["tr"] = new[] { "table" },
            ["td"] = new[] { "table" },
            ["th"] = new[] { "table" },
            ["li"] = new[] { "ul", "ol", "menu", "dir" },
            ["dt"] = new[] { "dl" },
            ["dd"] = new[] { "dl" },
            ["option"] = new[] { "select", "datalist" },
            ["p"] = new[] { "table", "td", "th", "li", "blockquote" }
        };

        public static HtmlNode Parse(string html)
        {
            var root = new HtmlNode("#document");
            if (string.IsNullOrEmpty(html)) return root;

            var stack = new List<HtmlNode> { root };
            var i = 0;
            var text = new StringBuilder();

            void FlushText()
            {
                if (text.Length == 0) return;
                var value = text.ToString();
                text.Clear();
                if (value.Trim().Length == 0 && !value.Contains('\n'))
                {
                    // Keep a single separating space between inline elements.
                    stack[^1].Add(HtmlNode.TextNode(" "));
                    return;
                }
                if (value.Trim().Length == 0) return;
                stack[^1].Add(HtmlNode.TextNode(Decode(value)));
            }

            while (i < html.Length)
            {
                var c = html[i];
                if (c != '<') { text.Append(c); i++; continue; }

                // Comment / doctype / CDATA.
                if (html.AsSpan(i).StartsWith("<!--"))
                {
                    FlushText();
                    var end = html.IndexOf("-->", i + 4, StringComparison.Ordinal);
                    i = end < 0 ? html.Length : end + 3;
                    continue;
                }
                if (i + 1 < html.Length && html[i + 1] == '!')
                {
                    FlushText();
                    var end = html.IndexOf('>', i);
                    i = end < 0 ? html.Length : end + 1;
                    continue;
                }

                var close = i + 1 < html.Length && html[i + 1] == '/';
                var tagStart = i + (close ? 2 : 1);
                var tagEnd = html.IndexOf('>', tagStart);
                if (tagEnd < 0) { text.Append(c); i++; continue; }

                var raw = html[tagStart..tagEnd].Trim();
                if (raw.Length == 0) { text.Append(c); i++; continue; }

                FlushText();
                i = tagEnd + 1;

                if (close)
                {
                    var name = raw.Split(new[] { ' ', '\t', '\n' })[0].ToLowerInvariant();
                    CloseTag(stack, name);
                    continue;
                }

                var selfClosing = raw.EndsWith("/");
                if (selfClosing) raw = raw[..^1].TrimEnd();

                var (tag, attributes) = ParseTag(raw);
                if (tag.Length == 0) continue;

                if (AutoClose.TryGetValue(tag, out var closes))
                {
                    AutoCloseScope.TryGetValue(tag, out var scope);
                    foreach (var candidate in closes)
                        if (IsOpen(stack, candidate, scope))
                        {
                            CloseTag(stack, candidate, scope);
                            break;
                        }
                }

                var node = new HtmlNode(tag);
                foreach (var pair in attributes) node.Attributes[pair.Key] = pair.Value;
                stack[^1].Add(node);

                if (selfClosing || Void.Contains(tag)) continue;
                if (stack.Count >= MaxDepth) continue;

                // Raw-text elements swallow everything until their close tag.
                if (tag is "script" or "style" or "textarea")
                {
                    var closeTag = "</" + tag;
                    var end = html.IndexOf(closeTag, i, StringComparison.OrdinalIgnoreCase);
                    var body = end < 0 ? html[i..] : html[i..end];
                    node.Add(HtmlNode.TextNode(body));

                    // A truncated close tag ("</style" with no '>') has no bracket to skip; run to the end, not to -1+1 (which loops forever).
                    if (end < 0)
                    {
                        i = html.Length;
                    }
                    else
                    {
                        var bracket = html.IndexOf('>', end);
                        i = bracket < 0 ? html.Length : bracket + 1;
                    }
                    continue;
                }

                stack.Add(node);
            }

            FlushText();
            return root;
        }

        // Whether `tag` is open. A scope stops the search at its first element, so a nested table's row is not matched to the outer table.
        static bool IsOpen(List<HtmlNode> stack, string tag, string[] scope = null)
        {
            for (var i = stack.Count - 1; i > 0; i--)
            {
                var open = stack[i].Tag;
                if (string.Equals(open, tag, StringComparison.OrdinalIgnoreCase)) return true;
                if (InScope(scope, open)) return false;
            }
            return false;
        }

        static void CloseTag(List<HtmlNode> stack, string tag, string[] scope = null)
        {
            for (var i = stack.Count - 1; i > 0; i--)
            {
                var open = stack[i].Tag;
                if (string.Equals(open, tag, StringComparison.OrdinalIgnoreCase))
                {
                    stack.RemoveRange(i, stack.Count - i);
                    return;
                }
                if (InScope(scope, open)) return;
            }
            // Unmatched close tag: ignore, the way every browser does.
        }

        static bool InScope(string[] scope, string tag)
        {
            if (scope == null) return false;
            foreach (var boundary in scope)
                if (string.Equals(boundary, tag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static (string tag, List<KeyValuePair<string, string>> attributes) ParseTag(string raw)
        {
            var attributes = new List<KeyValuePair<string, string>>();
            var i = 0;

            while (i < raw.Length && !char.IsWhiteSpace(raw[i])) i++;
            var tag = raw[..i].ToLowerInvariant();

            while (i < raw.Length)
            {
                while (i < raw.Length && char.IsWhiteSpace(raw[i])) i++;
                if (i >= raw.Length) break;

                var nameStart = i;
                while (i < raw.Length && raw[i] != '=' && !char.IsWhiteSpace(raw[i])) i++;
                var name = raw[nameStart..i];
                if (name.Length == 0) { i++; continue; }

                while (i < raw.Length && char.IsWhiteSpace(raw[i])) i++;
                if (i >= raw.Length || raw[i] != '=')
                {
                    attributes.Add(new KeyValuePair<string, string>(name, ""));
                    continue;
                }

                i++; // '='
                while (i < raw.Length && char.IsWhiteSpace(raw[i])) i++;
                if (i >= raw.Length) break;

                string value;
                if (raw[i] is '"' or '\'')
                {
                    var quote = raw[i++];
                    var start = i;
                    while (i < raw.Length && raw[i] != quote) i++;
                    value = raw[start..Math.Min(i, raw.Length)];
                    if (i < raw.Length) i++;
                }
                else
                {
                    var start = i;
                    while (i < raw.Length && !char.IsWhiteSpace(raw[i])) i++;
                    value = raw[start..i];
                }
                attributes.Add(new KeyValuePair<string, string>(name, Decode(value)));
            }

            return (tag, attributes);
        }

        static readonly Dictionary<string, string> Entities = new(StringComparer.Ordinal)
        {
            ["amp"] = "&", ["lt"] = "<", ["gt"] = ">", ["quot"] = "\"", ["apos"] = "'",
            ["nbsp"] = " ", ["copy"] = "©", ["reg"] = "®", ["trade"] = "™",
            ["hellip"] = "…", ["mdash"] = "—", ["ndash"] = "–", ["bull"] = "•",
            ["laquo"] = "«", ["raquo"] = "»", ["deg"] = "°", ["middot"] = "·",
            ["larr"] = "←", ["rarr"] = "→", ["uarr"] = "↑", ["darr"] = "↓",
            ["times"] = "×", ["divide"] = "÷", ["sect"] = "§", ["para"] = "¶"
        };

        public static string Decode(string value)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOf('&') < 0) return value;

            var sb = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] != '&') { sb.Append(value[i]); continue; }

                var end = value.IndexOf(';', i + 1);
                if (end < 0 || end - i > 10) { sb.Append('&'); continue; }

                var name = value[(i + 1)..end];
                if (name.StartsWith("#"))
                {
                    var digits = name[1..];
                    var parsed = digits.StartsWith("x", StringComparison.OrdinalIgnoreCase)
                        ? int.TryParse(digits[1..], System.Globalization.NumberStyles.HexNumber,
                            null, out var hex) ? hex : -1
                        : int.TryParse(digits, out var dec) ? dec : -1;
                    // ConvertFromUtf32 throws on bad code points, and a page may contain nonsense - keep the raw text.
                    if (IsCodePoint(parsed))
                    {
                        sb.Append(char.ConvertFromUtf32(parsed));
                        i = end;
                        continue;
                    }
                }
                else if (Entities.TryGetValue(name, out var replacement))
                {
                    sb.Append(replacement);
                    i = end;
                    continue;
                }

                sb.Append('&');
            }
            return sb.ToString();
        }

        // A value char.ConvertFromUtf32 will accept.
        static bool IsCodePoint(int value) =>
            value > 0 && value <= 0x10FFFF && (value < 0xD800 || value > 0xDFFF);
    }
}
