using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Deepwave.RetroOS.Web.Html
{
    // The properties the renderer understands. Anything else is parsed and ignored,
    // so a real-browser stylesheet still works in the 1995 subset.
    public class CssStyle
    {
        public Color? Color;
        public Color? Background;
        public float? FontSize;
        public bool? Bold;
        public bool? Italic;
        public bool? Underline;
        public bool? Monospace;
        public TextAnchor? Align;
        public float? MarginTop, MarginBottom, MarginLeft, MarginRight;
        public float? PaddingTop, PaddingBottom, PaddingLeft, PaddingRight;
        public float? Width, Height, MaxWidth;
        public float? BorderWidth;
        public Color? BorderColor;
        public string Display;         // block | inline | none | list-item
        public string ListStyle;
        public float? LineSpacing;
        public string TextTransform;

        public CssStyle Clone() => (CssStyle)MemberwiseClone();

        // Later declarations win, the way the cascade works.
        public void Merge(CssStyle other)
        {
            if (other == null) return;
            Color = other.Color ?? Color;
            Background = other.Background ?? Background;
            FontSize = other.FontSize ?? FontSize;
            Bold = other.Bold ?? Bold;
            Italic = other.Italic ?? Italic;
            Underline = other.Underline ?? Underline;
            Monospace = other.Monospace ?? Monospace;
            Align = other.Align ?? Align;
            MarginTop = other.MarginTop ?? MarginTop;
            MarginBottom = other.MarginBottom ?? MarginBottom;
            MarginLeft = other.MarginLeft ?? MarginLeft;
            MarginRight = other.MarginRight ?? MarginRight;
            PaddingTop = other.PaddingTop ?? PaddingTop;
            PaddingBottom = other.PaddingBottom ?? PaddingBottom;
            PaddingLeft = other.PaddingLeft ?? PaddingLeft;
            PaddingRight = other.PaddingRight ?? PaddingRight;
            Width = other.Width ?? Width;
            Height = other.Height ?? Height;
            MaxWidth = other.MaxWidth ?? MaxWidth;
            BorderWidth = other.BorderWidth ?? BorderWidth;
            BorderColor = other.BorderColor ?? BorderColor;
            Display = other.Display ?? Display;
            ListStyle = other.ListStyle ?? ListStyle;
            LineSpacing = other.LineSpacing ?? LineSpacing;
            TextTransform = other.TextTransform ?? TextTransform;
        }

        // What a child inherits from its parent (text properties only, as in
        // real CSS - boxes and backgrounds do not inherit).
        public CssStyle Inherited() => new()
        {
            Color = Color,
            FontSize = FontSize,
            Bold = Bold,
            Italic = Italic,
            Underline = Underline,
            Monospace = Monospace,
            Align = Align,
            LineSpacing = LineSpacing,
            TextTransform = TextTransform
        };
    }

    // One "selector { declarations }" rule.
    public class CssRule
    {
        public CssSelector Selector;
        public CssStyle Style;
        public int Order;
    }

    // A selector we can evaluate: an optional ancestor chain plus a tag/class/id target
    // ("p", ".note", "#main", "td.head", "nav a").
    public class CssSelector
    {
        public string Tag;
        public string Class;
        public string Id;
        public CssSelector Ancestor;

        public int Specificity =>
            (Id != null ? 100 : 0) + (Class != null ? 10 : 0) + (Tag != null ? 1 : 0) +
            (Ancestor?.Specificity ?? 0);

        public bool Matches(HtmlNode node)
        {
            if (!MatchesSelf(node)) return false;
            if (Ancestor == null) return true;

            for (var parent = node.Parent; parent != null; parent = parent.Parent)
                if (Ancestor.Matches(parent)) return true;
            return false;
        }

        bool MatchesSelf(HtmlNode node)
        {
            if (node.IsText) return false;
            if (Tag != null && !string.Equals(Tag, node.Tag, StringComparison.OrdinalIgnoreCase))
                return false;
            if (Id != null && !string.Equals(Id, node.Id, StringComparison.OrdinalIgnoreCase))
                return false;
            if (Class != null &&
                !node.Classes.Any(c => string.Equals(c, Class, StringComparison.OrdinalIgnoreCase)))
                return false;
            return true;
        }

        public static CssSelector Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            var parts = text.Trim().Split(new[] { ' ', '>', '\t', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return null;

            CssSelector current = null;
            foreach (var part in parts)
            {
                var selector = ParseSimple(part);
                if (selector == null) return null;
                selector.Ancestor = current;
                current = selector;
            }
            return current;
        }

        static CssSelector ParseSimple(string part)
        {
            var selector = new CssSelector();
            var buffer = "";
            var mode = 't';

            void Commit()
            {
                if (buffer.Length == 0) return;
                if (mode == 't') selector.Tag = buffer == "*" ? null : buffer.ToLowerInvariant();
                else if (mode == '.') selector.Class = buffer;
                else selector.Id = buffer;
                buffer = "";
            }

            foreach (var c in part)
            {
                if (c is '.' or '#') { Commit(); mode = c; continue; }
                // Pseudo-classes and attribute selectors are out of scope.
                if (c is ':' or '[') { Commit(); return selector; }
                buffer += c;
            }
            Commit();
            return selector;
        }
    }

    // Collects rules from <style> blocks and .css files and resolves the
    // style for any node.
    public class CssStylesheet
    {
        readonly List<CssRule> _rules = new();
        int _order;

        public int RuleCount => _rules.Count;

        public void Parse(string css)
        {
            if (string.IsNullOrWhiteSpace(css)) return;
            css = StripComments(css);

            var i = 0;
            while (i < css.Length)
            {
                var open = css.IndexOf('{', i);
                if (open < 0) break;
                var close = css.IndexOf('}', open);
                if (close < 0) break;

                var selectorText = css[i..open].Trim();
                var body = css[(open + 1)..close];
                i = close + 1;

                // At-rules. Screen queries are flattened (rules read as top-level); everything
                // else is stepped over brace by brace - a print-only block was styling the screen.
                if (selectorText.StartsWith("@"))
                {
                    // Resume inside the brace for a screen query so its rules are read as ordinary rules; else step over the block.
                    i = AppliesToScreen(selectorText) ? open + 1 : SkipBlock(css, open);
                    continue;
                }

                var style = ParseDeclarations(body);
                foreach (var single in selectorText.Split(','))
                {
                    var selector = CssSelector.Parse(single);
                    if (selector == null) continue;
                    _rules.Add(new CssRule { Selector = selector, Style = style, Order = _order++ });
                }
            }
        }

        // "@media screen", "@media all" and a bare "@media" are for us; a
        // print or speech query is not, and neither is @font-face.
        static bool AppliesToScreen(string atRule)
        {
            if (!atRule.StartsWith("@media", StringComparison.OrdinalIgnoreCase)) return false;
            var query = atRule[6..].Trim();
            return query.Length == 0 ||
                   query.IndexOf("screen", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   query.IndexOf("all", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Index just past the block that opens at `open`, counting nested
        // braces. Runs to the end when the block is never closed.
        static int SkipBlock(string css, int open)
        {
            var depth = 0;
            for (var i = open; i < css.Length; i++)
            {
                if (css[i] == '{') depth++;
                else if (css[i] == '}' && --depth == 0) return i + 1;
            }
            return css.Length;
        }

        static string StripComments(string css)
        {
            while (true)
            {
                var start = css.IndexOf("/*", StringComparison.Ordinal);
                if (start < 0) return css;
                var end = css.IndexOf("*/", start + 2, StringComparison.Ordinal);
                css = end < 0 ? css[..start] : css[..start] + css[(end + 2)..];
            }
        }

        // Every rule that matches, in cascade order (specificity, then source).
        public CssStyle StyleFor(HtmlNode node)
        {
            var result = new CssStyle();
            foreach (var rule in _rules
                         .Where(r => r.Selector.Matches(node))
                         .OrderBy(r => r.Selector.Specificity)
                         .ThenBy(r => r.Order))
                result.Merge(rule.Style);
            return result;
        }

        public static CssStyle ParseDeclarations(string body)
        {
            var style = new CssStyle();
            foreach (var declaration in body.Split(';'))
            {
                var colon = declaration.IndexOf(':');
                if (colon < 0) continue;
                var name = declaration[..colon].Trim().ToLowerInvariant();
                var value = declaration[(colon + 1)..].Trim();
                if (value.Length == 0) continue;
                Apply(style, name, value);
            }
            return style;
        }

        static void Apply(CssStyle style, string name, string value)
        {
            var lower = value.ToLowerInvariant();
            switch (name)
            {
                case "color": style.Color = ParseColor(value); break;
                case "background":
                case "background-color": style.Background = ParseColor(value); break;

                case "font-size": style.FontSize = ParseLength(value, 14f); break;
                case "font-weight":
                    style.Bold = lower is "bold" or "bolder" ||
                                 (int.TryParse(lower, out var weight) && weight >= 600);
                    break;
                case "font-style": style.Italic = lower is "italic" or "oblique"; break;
                case "font-family":
                    style.Monospace = lower.Contains("mono") || lower.Contains("courier") ||
                                      lower.Contains("consol") || lower.Contains("terminal");
                    break;
                case "text-decoration":
                case "text-decoration-line": style.Underline = lower.Contains("underline"); break;
                case "text-transform": style.TextTransform = lower; break;
                case "line-height": style.LineSpacing = ParseLineHeight(value); break;

                case "text-align":
                    style.Align = lower switch
                    {
                        "center" => TextAnchor.UpperCenter,
                        "right" => TextAnchor.UpperRight,
                        _ => TextAnchor.UpperLeft
                    };
                    break;

                case "margin": Box(value, v => { style.MarginTop = v; }, v => { style.MarginRight = v; },
                    v => { style.MarginBottom = v; }, v => { style.MarginLeft = v; }); break;
                case "margin-top": style.MarginTop = ParseLength(value, 0f); break;
                case "margin-bottom": style.MarginBottom = ParseLength(value, 0f); break;
                case "margin-left": style.MarginLeft = ParseLength(value, 0f); break;
                case "margin-right": style.MarginRight = ParseLength(value, 0f); break;

                case "padding": Box(value, v => { style.PaddingTop = v; }, v => { style.PaddingRight = v; },
                    v => { style.PaddingBottom = v; }, v => { style.PaddingLeft = v; }); break;
                case "padding-top": style.PaddingTop = ParseLength(value, 0f); break;
                case "padding-bottom": style.PaddingBottom = ParseLength(value, 0f); break;
                case "padding-left": style.PaddingLeft = ParseLength(value, 0f); break;
                case "padding-right": style.PaddingRight = ParseLength(value, 0f); break;

                case "width": style.Width = ParseLength(value, 0f); break;
                case "height": style.Height = ParseLength(value, 0f); break;
                case "max-width": style.MaxWidth = ParseLength(value, 0f); break;

                case "border":
                    style.BorderWidth = ParseLength(value, 1f) ?? 1f;
                    style.BorderColor = ParseColor(value) ?? style.BorderColor;
                    break;
                case "border-width": style.BorderWidth = ParseLength(value, 1f); break;
                case "border-color": style.BorderColor = ParseColor(value); break;

                case "display": style.Display = lower; break;
                case "list-style":
                case "list-style-type": style.ListStyle = lower; break;
            }
        }

        static void Box(string value, Action<float> top, Action<float> right,
            Action<float> bottom, Action<float> left)
        {
            var parts = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => ParseLength(p, 0f) ?? 0f).ToArray();
            switch (parts.Length)
            {
                case 1: top(parts[0]); right(parts[0]); bottom(parts[0]); left(parts[0]); break;
                case 2: top(parts[0]); bottom(parts[0]); right(parts[1]); left(parts[1]); break;
                case 3: top(parts[0]); right(parts[1]); left(parts[1]); bottom(parts[2]); break;
                case >= 4: top(parts[0]); right(parts[1]); bottom(parts[2]); left(parts[3]); break;
            }
        }

        static float? ParseLineHeight(string value)
        {
            var raw = value.Trim();
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var factor))
                return factor;
            var px = ParseLength(raw, 0f);
            return px.HasValue ? px.Value / 14f : null;
        }

        // px / pt / em / % / bare numbers. Everything lands in canvas pixels.
        public static float? ParseLength(string value, float basis)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            value = value.Trim().ToLowerInvariant();

            // "1px solid #000" -> take the first token that parses.
            if (value.Contains(' '))
                foreach (var token in value.Split(' '))
                {
                    var parsed = ParseLength(token, basis);
                    if (parsed.HasValue) return parsed;
                }

            var named = value switch
            {
                "xx-small" => 9f, "x-small" => 10f, "small" => 12f,
                "medium" => 14f, "large" => 17f, "x-large" => 21f, "xx-large" => 26f,
                "thin" => 1f, "thick" => 3f, "auto" => (float?)null, "0" => 0f,
                _ => (float?)null
            };
            if (named.HasValue) return named;

            var number = "";
            var i = 0;
            if (i < value.Length && (value[i] == '-' || value[i] == '+')) number += value[i++];
            while (i < value.Length && (char.IsDigit(value[i]) || value[i] == '.')) number += value[i++];
            if (number.Length == 0 || !float.TryParse(number, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var amount)) return null;

            var unit = value[i..].Trim();
            return unit switch
            {
                "pt" => amount * 1.333f,
                "em" or "rem" => amount * (basis > 0f ? basis : 14f),
                "%" => basis > 0f ? amount / 100f * basis : amount,
                _ => amount
            };
        }

        static readonly Dictionary<string, string> Named = new(StringComparer.OrdinalIgnoreCase)
        {
            ["black"] = "#000000", ["white"] = "#FFFFFF", ["silver"] = "#C0C0C0",
            ["gray"] = "#808080", ["grey"] = "#808080", ["maroon"] = "#800000",
            ["red"] = "#FF0000", ["purple"] = "#800080", ["fuchsia"] = "#FF00FF",
            ["green"] = "#008000", ["lime"] = "#00FF00", ["olive"] = "#808000",
            ["yellow"] = "#FFFF00", ["navy"] = "#000080", ["blue"] = "#0000FF",
            ["teal"] = "#008080", ["aqua"] = "#00FFFF", ["cyan"] = "#00FFFF",
            ["orange"] = "#FFA500", ["pink"] = "#FFC0CB", ["gold"] = "#FFD700",
            ["beige"] = "#F5F5DC", ["ivory"] = "#FFFFF0", ["khaki"] = "#F0E68C",
            ["magenta"] = "#FF00FF", ["darkgreen"] = "#006400", ["darkred"] = "#8B0000",
            ["lightgray"] = "#D3D3D3", ["lightgrey"] = "#D3D3D3", ["transparent"] = "#00000000"
        };

        public static Color? ParseColor(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            value = value.Trim();

            // Pull the colour out of shorthand like "1px solid #808080".
            if (value.Contains(' '))
                foreach (var token in value.Split(' '))
                {
                    var parsed = ParseColor(token);
                    if (parsed.HasValue) return parsed;
                }

            if (value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            {
                var open = value.IndexOf('(');
                var close = value.IndexOf(')');
                if (open < 0 || close < 0) return null;
                var parts = value[(open + 1)..close].Split(',');
                if (parts.Length < 3) return null;
                float Channel(int index) =>
                    float.TryParse(parts[index].Trim().TrimEnd('%'), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var v) ? v / 255f : 0f;
                var alpha = parts.Length > 3 && float.TryParse(parts[3].Trim(), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var a) ? a : 1f;
                return new Color(Channel(0), Channel(1), Channel(2), alpha);
            }

            if (Named.TryGetValue(value, out var hex)) value = hex;
            if (!value.StartsWith("#")) return null;
            return ColorUtility.TryParseHtmlString(value, out var color) ? color : null;
        }
    }
}
