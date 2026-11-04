using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Web.Html
{
    // What the renderer needs from the browser around it.
    public class HtmlRenderContext
    {
        // Follow a link (already resolved to an absolute address by the browser).
        public Action<string> Navigate;
        // Turn a src attribute into a picture, or null when it cannot load.
        public Func<string, Sprite> LoadImage;
        // Frames of an animated image, when the source has more than one.
        public Func<string, List<Sprite>> LoadFrames;
        // Resolve a relative href/src against the page being shown.
        public Func<string, string> Resolve;
        // Called with the href under the cursor (status bar), or null.
        public Action<string> Hover;
        // Whether a resolved address has been visited, so the link is drawn
        // purple instead of blue.
        public Func<string, bool> Visited;
        // What the page asked its links be painted, from <body link vlink>.
        // Null means the browser's own blue and purple.
        public Color? LinkColor;
        public Color? VisitedColor;
        // Width available for the page body, in canvas pixels.
        public float ContentWidth = 560f;
    }

    // Lays a parsed page out as real uGUI, 1995-style: block boxes stacked vertically,
    // inline runs as wrapped rich text, images as their own boxes, tables as simple grids.
    public static class HtmlRenderer
    {
        // A picture with no size given is drawn at its own pixels, capped so
        // one large photo cannot push the whole column off screen.
        const float MaxNaturalWidth = 480f;
        const float BrokenSize = 34f;

        static readonly HashSet<string> InlineTags = new(StringComparer.OrdinalIgnoreCase)
        {
            "a", "b", "strong", "i", "em", "u", "ins", "span", "font", "code", "kbd", "samp",
            "small", "big", "sub", "sup", "abbr", "cite", "q", "mark", "s", "strike", "del", "tt", "var"
        };

        static readonly HashSet<string> Skipped = new(StringComparer.OrdinalIgnoreCase)
        {
            "script", "style", "head", "meta", "link", "title", "noscript", "iframe", "object"
        };

        public static void Render(RectTransform parent, HtmlNode document, CssStylesheet css,
            HtmlRenderContext ctx)
        {
            var body = document.Find("body") ?? document;
            var root = new CssStyle
            {
                Color = RetroSkin.Text,
                FontSize = 14f,
                Align = TextAnchor.UpperLeft
            };

            var bodyStyle = root.Clone();
            bodyStyle.Merge(UserAgent(body));
            bodyStyle.Merge(css.StyleFor(body));
            bodyStyle.Merge(InlineStyle(body));

            // <body link vlink> - the only way to set link colours here: no :visited in a stylesheet, and a colour on "a" would not tell followed from not.
            ctx.LinkColor = CssStylesheet.ParseColor(body.Attr("link"));
            ctx.VisitedColor = CssStylesheet.ParseColor(body.Attr("vlink"));

            if (bodyStyle.Background.HasValue) PaintBackground(parent, bodyStyle.Background.Value);

            // <body background="tile.gif"> - the era's wallpaper.
            var tile = body.Attr("background");
            if (!string.IsNullOrEmpty(tile) && ctx.LoadImage != null)
            {
                var wallpaper = ctx.LoadImage(ctx.Resolve != null ? ctx.Resolve(tile) : tile);
                if (wallpaper) PaintWallpaper(parent, wallpaper);
            }

            RenderChildren(parent, body, bodyStyle, css, ctx);
        }

        // Tiled wallpaper behind the page, under everything it renders.
        static void PaintWallpaper(RectTransform parent, Sprite sprite)
        {
            var target = RetroUi.ScrollBackdrop(parent.GetComponentInParent<ScrollRect>());
            if (!target) return;

            target.sprite = sprite;
            target.type = Image.Type.Tiled;
            target.color = Color.white;
        }

        static void PaintBackground(RectTransform parent, Color color)
        {
            // The page background sits behind everything the body renders.
            var target = RetroUi.ScrollBackdrop(parent.GetComponentInParent<ScrollRect>());
            if (target) target.color = color;
        }

        // Walks a block's children, gathering runs of inline content into
        // paragraphs and emitting block children as their own boxes.
        static void RenderChildren(RectTransform parent, HtmlNode node, CssStyle inherited,
            CssStylesheet css, HtmlRenderContext ctx)
        {
            var run = new List<HtmlNode>();

            void FlushRun()
            {
                if (run.Count == 0) return;
                EmitInlineRun(parent, run, inherited, css, ctx);
                run.Clear();
            }

            foreach (var child in node.Children)
            {
                if (!child.IsText && Skipped.Contains(child.Tag)) continue;

                if (IsInline(child)) { run.Add(child); continue; }

                FlushRun();
                RenderBlock(parent, child, inherited, css, ctx);
            }
            FlushRun();
        }

        static bool IsInline(HtmlNode node)
        {
            if (node.IsText) return true;
            if (node.Tag == "br") return true;
            if (node.Tag == "img") return false;   // pictures get their own box

            // An inline element wrapping a picture goes down the block path, not the text run:
            // an <img> contributes no characters, so <a href><img></a> would otherwise render nothing.
            // Picture already looks up the enclosing <a> for href and border.
            if (!InlineTags.Contains(node.Tag)) return false;
            return !ContainsImage(node);
        }

        static bool ContainsImage(HtmlNode node)
        {
            foreach (var child in node.Children)
            {
                if (child.IsText) continue;
                if (string.Equals(child.Tag, "img", StringComparison.OrdinalIgnoreCase)) return true;
                if (ContainsImage(child)) return true;
            }
            return false;
        }

        // --- blocks --------------------------------------------------------

        static void RenderBlock(RectTransform parent, HtmlNode node, CssStyle inherited,
            CssStylesheet css, HtmlRenderContext ctx)
        {
            var style = inherited.Inherited();
            style.Merge(UserAgent(node));
            style.Merge(css.StyleFor(node));
            style.Merge(InlineStyle(node));

            if (style.Display == "none") return;

            switch (node.Tag)
            {
                case "hr": Rule(parent, style); return;
                case "img": Picture(parent, node, style, ctx); return;
                case "br": Gap(parent, 8f); return;
                case "table": Table(parent, node, style, css, ctx); return;
                case "ul":
                case "ol": List(parent, node, style, css, ctx); return;
                case "pre": Preformatted(parent, node, style); return;
            }

            var box = Box(parent, style, out var body);
            RenderChildren(body, node, style, css, ctx);

            // An empty box would collapse to nothing and eat its margins.
            if (body.childCount == 0 && !style.Height.HasValue && !style.Background.HasValue)
                UnityEngine.Object.Destroy(box.gameObject);
        }

        // Creates a block container honouring margins, padding, background and
        // border, and returns the rect children go into.
        static RectTransform Box(RectTransform parent, CssStyle style, out RectTransform body)
        {
            var outer = RetroUi.NewRect("Block", parent);
            var layout = RetroUi.VLayout(outer.gameObject, 0,
                0);
            layout.padding = new RectOffset(
                Mathf.RoundToInt(style.MarginLeft ?? 0f),
                Mathf.RoundToInt(style.MarginRight ?? 0f),
                Mathf.RoundToInt(style.MarginTop ?? 0f),
                Mathf.RoundToInt(style.MarginBottom ?? 0f));

            var inner = RetroUi.NewRect("Content", outer);
            if (style.Background.HasValue || style.BorderWidth.HasValue)
            {
                var bg = inner.gameObject.AddComponent<Image>();
                bg.color = style.Background ?? Color.clear;
                bg.raycastTarget = false;
                if (style.BorderWidth.HasValue && style.BorderWidth.Value > 0f)
                    Border(inner, style.BorderColor ?? RetroSkin.FaceDark, style.BorderWidth.Value);
            }

            var innerLayout = RetroUi.VLayout(inner.gameObject, 0, 0);
            innerLayout.padding = new RectOffset(
                Mathf.RoundToInt(style.PaddingLeft ?? 0f),
                Mathf.RoundToInt(style.PaddingRight ?? 0f),
                Mathf.RoundToInt(style.PaddingTop ?? 0f),
                Mathf.RoundToInt(style.PaddingBottom ?? 0f));

            if (style.Height.HasValue) RetroUi.Size(inner, h: style.Height.Value);
            if (style.Width.HasValue) RetroUi.Size(inner, w: style.Width.Value);

            body = inner;
            return outer;
        }

        static void Border(RectTransform rt, Color color, float width)
        {
            void Edge(Vector2 min, Vector2 max, Vector2 size)
            {
                var img = RetroUi.NewRect("Border", rt).gameObject.AddComponent<Image>();
                img.color = color;
                img.raycastTarget = false;
                img.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                var r = img.rectTransform;
                r.anchorMin = min;
                r.anchorMax = max;
                r.sizeDelta = size;
                r.anchoredPosition = Vector2.zero;
            }

            Edge(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, width));
            Edge(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, width));
            Edge(new Vector2(0, 0), new Vector2(0, 1), new Vector2(width, 0));
            Edge(new Vector2(1, 0), new Vector2(1, 1), new Vector2(width, 0));
        }

        static void Gap(RectTransform parent, float height) =>
            RetroUi.Size(RetroUi.NewRect("Gap", parent), h: height);

        static void Rule(RectTransform parent, CssStyle style)
        {
            Gap(parent, style.MarginTop ?? 6f);
            RetroUi.Separator(parent);
            Gap(parent, style.MarginBottom ?? 6f);
        }

        static void Preformatted(RectTransform parent, HtmlNode node, CssStyle style)
        {
            var frame = RetroUi.Panel(parent, style.Background ?? new Color(0.94f, 0.94f, 0.94f), "Pre");
            RetroUi.Bevel(frame.rectTransform, false, 1f);

            // The frame lays the text out itself: a stretched inner rect reports no height, so a <pre> block came out zero pixels tall.
            var layout = RetroUi.VLayout(frame.gameObject, 0, 6);
            layout.childForceExpandHeight = false;

            var label = RetroUi.Label(frame.transform, node.InnerText,
                Mathf.RoundToInt(style.FontSize ?? 13f), TextAnchor.UpperLeft, RetroSkin.Mono);
            label.color = style.Color ?? RetroSkin.Text;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        static void Picture(RectTransform parent, HtmlNode node, CssStyle style, HtmlRenderContext ctx)
        {
            var src = node.Attr("src");
            if (string.IsNullOrEmpty(src)) return;

            var resolved = ctx.Resolve != null ? ctx.Resolve(src) : src;
            var frames = ctx.LoadFrames?.Invoke(resolved);
            var sprite = frames is { Count: > 0 } ? frames[0] : ctx.LoadImage?.Invoke(resolved);

            // A picture sits on its own row so `align` can place it the way a
            // 90s page expects: left, centre or right of the column.
            var row = RetroUi.NewRect("ImageRow", parent);
            var layout = RetroUi.HLayout(row.gameObject, 0, 0);
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            layout.childAlignment = Align(node, style);

            var width = ParseAttrLength(node.Attr("width")) ?? style.Width;
            var height = ParseAttrLength(node.Attr("height")) ?? style.Height;
            var alt = node.Attr("alt");

            var content = sprite
                ? Loaded(row, sprite, frames, width, height)
                : Broken(row, alt, width, height);

            RetroUi.Size(row, h: content.sizeDelta.y > 0f ? content.sizeDelta.y : BrokenSize);

            // An image wrapped in a link is clickable, as in every 90s site.
            var href = Ancestor(node, "a")?.Attr("href");
            if (string.IsNullOrEmpty(href)) return;

            var target = ctx.Resolve != null ? ctx.Resolve(href) : href;
            foreach (var graphic in content.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = true;

            var relay = content.gameObject.AddComponent<UI.Components.RetroPointerRelay>();
            relay.Clicked = () => ctx.Navigate?.Invoke(target);
            relay.Entered = () => ctx.Hover?.Invoke(target);
            relay.Exited = () => ctx.Hover?.Invoke(null);

            // Linked images got a blue border - the era's clearest signal that
            // a picture is clickable.
            var border = ParseAttrLength(node.Attr("border")) ?? 2f;
            if (border > 0f) Border(content, RetroSkin.Link, border);
        }

        static TextAnchor Align(HtmlNode node, CssStyle style)
        {
            switch ((node.Attr("align") ?? "").ToLowerInvariant())
            {
                case "right": return TextAnchor.UpperRight;
                case "center":
                case "middle": return TextAnchor.UpperCenter;
                case "left": return TextAnchor.UpperLeft;
            }

            return style.Align switch
            {
                TextAnchor.UpperCenter => TextAnchor.UpperCenter,
                TextAnchor.UpperRight => TextAnchor.UpperRight,
                _ => TextAnchor.UpperLeft
            };
        }

        // The picture itself, sized from the attributes or from its own pixels.
        static RectTransform Loaded(RectTransform row, Sprite sprite, List<Sprite> frames,
            float? width, float? height)
        {
            var aspect = sprite.rect.height > 0f ? sprite.rect.width / sprite.rect.height : 1f;

            // Honour whichever dimension the page gave and derive the other, so
            // <img width="64"> is 64 wide instead of stretching to the column.
            if (width.HasValue && !height.HasValue) height = width.Value / Mathf.Max(0.01f, aspect);
            else if (height.HasValue && !width.HasValue) width = height.Value * aspect;
            else if (!width.HasValue)
            {
                width = Mathf.Min(sprite.rect.width, MaxNaturalWidth);
                height = width.Value / Mathf.Max(0.01f, aspect);
            }

            var holder = RetroUi.NewRect("Image", row);
            holder.sizeDelta = new Vector2(width.Value, height.Value);
            RetroUi.Size(holder, w: width.Value, h: height.Value);

            var image = RetroUi.Picture(holder, sprite);
            RetroUi.Stretch(image.rectTransform);

            if (frames is { Count: > 1 }) FlipbookImage.Attach(image, frames, 8f);
            return holder;
        }

        // What a browser draws when the picture will not load: the torn-page
        // placeholder, with the alt text beside it.
        static RectTransform Broken(RectTransform row, string alt, float? width, float? height)
        {
            var holder = RetroUi.NewRect("BrokenImage", row);
            var w = width ?? Mathf.Max(BrokenSize, RetroUi.TextWidth(alt, 12f) + BrokenSize + 12f);
            var h = height ?? BrokenSize;
            holder.sizeDelta = new Vector2(w, h);
            RetroUi.Size(holder, w: w, h: h);

            var frame = RetroUi.Panel(holder, PaletteSlot.Field, "Frame");
            RetroUi.Stretch(frame.rectTransform);
            RetroUi.Bevel(frame.rectTransform, false, 1f);

            var glyph = RetroUi.Icon(holder, RetroIcons.BrokenImage, Mathf.Min(h - 6f, 24f));
            var grt = glyph.rectTransform;
            grt.anchorMin = grt.anchorMax = new Vector2(0f, 0.5f);
            grt.pivot = new Vector2(0f, 0.5f);
            grt.anchoredPosition = new Vector2(4f, 0f);

            if (string.IsNullOrEmpty(alt)) return holder;

            var label = RetroUi.NoWrap(RetroUi.Label(holder, alt, 12, TextAnchor.MiddleLeft));
            RetroPalette.Paint(label, PaletteSlot.TextDisabled);
            RetroUi.Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(Mathf.Min(h, 30f), 0f);
            return holder;
        }

        static void List(RectTransform parent, HtmlNode node, CssStyle style, CssStylesheet css,
            HtmlRenderContext ctx)
        {
            var ordered = string.Equals(node.Tag, "ol", StringComparison.OrdinalIgnoreCase);
            var index = 1;

            Gap(parent, style.MarginTop ?? 4f);
            foreach (var item in node.Children)
            {
                if (item.IsText || !string.Equals(item.Tag, "li", StringComparison.OrdinalIgnoreCase))
                    continue;

                var row = RetroUi.NewRect("ListItem", parent);
                var layout = RetroUi.HLayout(row.gameObject, 4, 0);
                layout.childForceExpandHeight = false;
                layout.childAlignment = TextAnchor.UpperLeft;

                var bullet = RetroUi.Label(row, ordered ? index + "." : "•",
                    Mathf.RoundToInt(style.FontSize ?? 14f), TextAnchor.UpperRight);
                bullet.color = style.Color ?? RetroSkin.Text;
                RetroUi.Size(bullet, w: ordered ? 26f : 16f);

                var body = RetroUi.NewRect("Body", row);
                RetroUi.Flexible(body, w: 1f, h: 0f);
                RetroUi.VLayout(body.gameObject, 0, 0);

                var itemStyle = style.Inherited();
                itemStyle.Merge(css.StyleFor(item));
                itemStyle.Merge(InlineStyle(item));
                RenderChildren(body, item, itemStyle, css, ctx);

                index++;
            }
            Gap(parent, style.MarginBottom ?? 4f);
        }

        static void Table(RectTransform parent, HtmlNode node, CssStyle style, CssStylesheet css,
            HtmlRenderContext ctx)
        {
            var border = ParseAttrLength(node.Attr("border")) ?? style.BorderWidth ?? 0f;
            var frame = RetroUi.NewRect("Table", parent);
            var frameLayout = RetroUi.VLayout(frame.gameObject, border > 0f ? 1 : 2, 0);
            frameLayout.childForceExpandHeight = false;

            // Descendants, not Children: <tbody>/<thead> sit between a table and its rows.
            // Rows are matched to their owning table, so a table in a cell does not draw its rows twice.
            var rows = new List<List<HtmlNode>>();
            foreach (var row in node.Descendants())
            {
                if (row.IsText || !string.Equals(row.Tag, "tr", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (Ancestor(row, "table") != node) continue;

                var cells = new List<HtmlNode>();
                foreach (var cell in row.Children)
                    if (!cell.IsText &&
                        (string.Equals(cell.Tag, "td", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(cell.Tag, "th", StringComparison.OrdinalIgnoreCase)))
                        cells.Add(cell);
                if (cells.Count > 0) rows.Add(cells);
            }

            var columns = ColumnWidths(rows);

            for (var r = 0; r < rows.Count; r++)
            {
                var rowRect = RetroUi.NewRect("Row", frame);
                var rowLayout = RetroUi.HLayout(rowRect.gameObject, border > 0f ? 1 : 6, 0);
                rowLayout.childForceExpandHeight = true;

                for (var i = 0; i < rows[r].Count; i++)
                {
                    var cell = rows[r][i];
                    var isHeader = string.Equals(cell.Tag, "th", StringComparison.OrdinalIgnoreCase);

                    var cellStyle = style.Inherited();
                    cellStyle.Merge(UserAgent(cell));
                    cellStyle.Merge(css.StyleFor(cell));
                    cellStyle.Merge(InlineStyle(cell));
                    if (isHeader) cellStyle.Bold = true;

                    var cellRect = RetroUi.NewRect(cell.Tag, rowRect);
                    ApplyColumn(cellRect, i < columns.Count ? columns[i] : default);

                    // A colour the page asked for is the page's; the fallback
                    // behind a bordered cell is the palette's, and follows it.
                    var bg = cellRect.gameObject.AddComponent<Image>();
                    if (cellStyle.Background.HasValue)
                        RetroPalette.PaintOff(bg, cellStyle.Background.Value);
                    else if (border > 0f) RetroPalette.Paint(bg, PaletteSlot.Face);
                    else RetroPalette.PaintOff(bg, Color.clear);
                    bg.raycastTarget = false;
                    if (border > 0f) Border(cellRect, cellStyle.BorderColor ?? RetroSkin.FaceDark, border);

                    // The cell lays out its own contents: a stretched Pad rect reports no height, so
                    // every table row drew at the top of the table on top of the last one.
                    var cellLayout = RetroUi.VLayout(cellRect.gameObject, 0, 4);
                    cellLayout.childForceExpandHeight = false;
                    RenderChildren(cellRect, cell, cellStyle, css, ctx);
                }
            }
        }

        // How wide a column is: pinned to a number of pixels, sharing the row
        // out in proportion, or taking an equal part of what is left.
        readonly struct Column
        {
            public readonly float Pixels;
            public readonly float Share;

            public Column(float pixels, float share)
            {
                Pixels = pixels;
                Share = share;
            }
        }

        // A width on a cell is a column property: a browser takes the first one any row
        // states and applies it down the whole column, so header-row widths are not ragged underneath.
        static List<Column> ColumnWidths(List<List<HtmlNode>> rows)
        {
            var widest = 0;
            foreach (var row in rows) widest = Mathf.Max(widest, row.Count);

            var columns = new List<Column>();
            for (var i = 0; i < widest; i++)
            {
                var found = default(Column);
                foreach (var row in rows)
                {
                    if (i >= row.Count) continue;
                    var attr = (row[i].Attr("width") ?? "").Trim();
                    if (attr.Length == 0) continue;

                    if (attr.EndsWith("%") && float.TryParse(attr[..^1], NumberStyles.Float,
                            CultureInfo.InvariantCulture, out var share) && share > 0f)
                    {
                        found = new Column(0f, share);
                        break;
                    }

                    var pixels = ParseAttrLength(attr);
                    if (pixels is > 0f) { found = new Column(pixels.Value, 0f); break; }
                }
                columns.Add(found);
            }
            return columns;
        }

        static void ApplyColumn(RectTransform cellRect, Column column)
        {
            var element = cellRect.gameObject.AddComponent<LayoutElement>();
            element.flexibleHeight = 1f;

            if (column.Pixels > 0f)
            {
                // A cell WIDTH is a minimum as well as a preference: a HorizontalLayoutGroup short
                // of room slides children back to minWidth, so a pinned sidebar column lost every time at minWidth 0.
                element.preferredWidth = column.Pixels;
                element.minWidth = column.Pixels;
                element.flexibleWidth = 0f;
                return;
            }

            element.flexibleWidth = column.Share > 0f ? column.Share : 1f;
        }

        // --- inline runs ---------------------------------------------------

        static void EmitInlineRun(RectTransform parent, List<HtmlNode> run, CssStyle inherited,
            CssStylesheet css, HtmlRenderContext ctx)
        {
            var builder = new StringBuilder();
            var links = new List<HtmlTextBlock.LinkRange>();
            var visible = 0;

            foreach (var node in run)
                AppendInline(node, inherited, css, ctx, builder, links, ref visible);

            var text = builder.ToString();
            if (text.Trim().Length == 0) return;

            var label = RetroUi.Label(parent, text, Mathf.RoundToInt(inherited.FontSize ?? 14f),
                inherited.Align ?? TextAnchor.UpperLeft,
                inherited.Monospace == true ? RetroSkin.Mono : null);
            label.color = inherited.Color ?? RetroSkin.Text;
            label.supportRichText = true;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            if (inherited.LineSpacing.HasValue) label.lineSpacing = inherited.LineSpacing.Value;

            var fitter = label.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (links.Count > 0)
                HtmlTextBlock.Attach(label, links, ctx.Navigate, ctx.Hover);
        }

        // Turns one inline node into rich text, recording link ranges. Only visible characters advance the counter (what Unity's text generator reports on a click).
        static void AppendInline(HtmlNode node, CssStyle inherited, CssStylesheet css,
            HtmlRenderContext ctx, StringBuilder sb, List<HtmlTextBlock.LinkRange> links,
            ref int visible)
        {
            if (node.IsText)
            {
                var value = Collapse(node.Text);
                if (inherited.TextTransform == "uppercase") value = value.ToUpperInvariant();
                else if (inherited.TextTransform == "lowercase") value = value.ToLowerInvariant();
                sb.Append(Escape(value));
                visible += value.Length;
                return;
            }

            if (node.Tag == "br")
            {
                sb.Append('\n');
                visible += 1;
                return;
            }

            var style = inherited.Inherited();

            var isLink = string.Equals(node.Tag, "a", StringComparison.OrdinalIgnoreCase) &&
                         !string.IsNullOrEmpty(node.Attr("href"));
            if (isLink)
            {
                // Blue, or purple once followed - the user agent's own a:link rule, applied before
                // the page's styles so it beats inheritance and loses to the author (a colour on body would otherwise win).
                var target = ctx.Resolve != null
                    ? ctx.Resolve(node.Attr("href"))
                    : node.Attr("href");
                style.Color = ctx.Visited != null && ctx.Visited(target)
                    ? ctx.VisitedColor ?? RetroSkin.LinkVisited
                    : ctx.LinkColor ?? RetroSkin.Link;
                style.Underline = true;
            }

            style.Merge(UserAgent(node));
            style.Merge(css.StyleFor(node));
            style.Merge(InlineStyle(node));

            // <font color size> is period-correct and still common in retro sites.
            if (node.Tag == "font")
            {
                var color = CssStylesheet.ParseColor(node.Attr("color"));
                if (color.HasValue) style.Color = color;
                if (int.TryParse(node.Attr("size"), out var size))
                    style.FontSize = 10f + Mathf.Clamp(size, 1, 7) * 2f;
            }

            var open = new StringBuilder();
            var close = new StringBuilder();

            if (style.Color.HasValue && style.Color != inherited.Color)
            {
                open.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(style.Color.Value)).Append('>');
                close.Insert(0, "</color>");
            }
            if (style.Bold == true && inherited.Bold != true) { open.Append("<b>"); close.Insert(0, "</b>"); }
            if (style.Italic == true && inherited.Italic != true) { open.Append("<i>"); close.Insert(0, "</i>"); }
            if (style.FontSize.HasValue && !Mathf.Approximately(style.FontSize.Value, inherited.FontSize ?? 14f))
            {
                open.Append("<size=").Append(Mathf.RoundToInt(style.FontSize.Value)).Append('>');
                close.Insert(0, "</size>");
            }

            sb.Append(open);
            var start = visible;

            foreach (var child in node.Children)
                AppendInline(child, style, css, ctx, sb, links, ref visible);

            sb.Append(close);

            if (!isLink) return;
            var href = node.Attr("href");
            links.Add(new HtmlTextBlock.LinkRange
            {
                Start = start,
                End = visible,
                Href = ctx.Resolve != null ? ctx.Resolve(href) : href,
                Title = node.Attr("title"),
                // Legacy uGUI text has no underline tag, so the block draws
                // the rule itself. A page may still turn it off.
                Underline = style.Underline == true,
                Color = style.Color ?? RetroSkin.Link
            });
        }

        // Runs of whitespace collapse to one space, exactly as in HTML.
        static string Collapse(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            var sb = new StringBuilder(value.Length);
            var space = false;
            foreach (var c in value)
            {
                if (char.IsWhiteSpace(c))
                {
                    if (!space) sb.Append(' ');
                    space = true;
                    continue;
                }
                space = false;
                sb.Append(c);
            }
            return sb.ToString();
        }

        // Left as-is: uGUI prints an unknown tag literally, and an escape char would shift the visible-character counter the link ranges use.
        static string Escape(string value) => value;

        static HtmlNode Ancestor(HtmlNode node, string tag)
        {
            for (var p = node.Parent; p != null; p = p.Parent)
                if (string.Equals(p.Tag, tag, StringComparison.OrdinalIgnoreCase)) return p;
            return null;
        }

        static float? ParseAttrLength(string value) =>
            string.IsNullOrWhiteSpace(value) ? null : CssStylesheet.ParseLength(value, 0f);

        static CssStyle InlineStyle(HtmlNode node)
        {
            var inline = node.Attr("style");
            var style = string.IsNullOrWhiteSpace(inline)
                ? new CssStyle()
                : CssStylesheet.ParseDeclarations(inline);

            // Presentational attributes the era relied on.
            var align = node.Attr("align");
            if (!string.IsNullOrEmpty(align))
                style.Align = align.ToLowerInvariant() switch
                {
                    "center" => TextAnchor.UpperCenter,
                    "right" => TextAnchor.UpperRight,
                    _ => TextAnchor.UpperLeft
                };

            var bg = CssStylesheet.ParseColor(node.Attr("bgcolor"));
            if (bg.HasValue) style.Background = bg;

            var color = CssStylesheet.ParseColor(node.Attr("text"));
            if (color.HasValue) style.Color = color;

            return style;
        }

        // The browser's own default stylesheet - what makes an unstyled page
        // still look like a page.
        static CssStyle UserAgent(HtmlNode node) => node.Tag switch
        {
            "body" => new CssStyle { MarginTop = 6f, MarginBottom = 6f, FontSize = 14f },
            "h1" => new CssStyle { FontSize = 26f, Bold = true, MarginTop = 12f, MarginBottom = 8f },
            "h2" => new CssStyle { FontSize = 22f, Bold = true, MarginTop = 10f, MarginBottom = 6f },
            "h3" => new CssStyle { FontSize = 19f, Bold = true, MarginTop = 8f, MarginBottom = 5f },
            "h4" => new CssStyle { FontSize = 16f, Bold = true, MarginTop = 8f, MarginBottom = 4f },
            "h5" => new CssStyle { FontSize = 15f, Bold = true, MarginTop = 6f, MarginBottom = 4f },
            "h6" => new CssStyle { FontSize = 14f, Bold = true, MarginTop = 6f, MarginBottom = 4f },
            "p" => new CssStyle { MarginTop = 4f, MarginBottom = 8f },
            "blockquote" => new CssStyle { MarginLeft = 24f, MarginRight = 12f, Italic = true },
            "center" => new CssStyle { Align = TextAnchor.UpperCenter },
            "b" or "strong" or "th" => new CssStyle { Bold = true },
            "i" or "em" or "cite" or "var" => new CssStyle { Italic = true },
            "u" or "ins" => new CssStyle { Underline = true },
            "small" => new CssStyle { FontSize = 12f },
            "big" => new CssStyle { FontSize = 17f },
            "code" or "kbd" or "samp" or "tt" => new CssStyle { Monospace = true },
            "ul" or "ol" => new CssStyle { MarginLeft = 16f },
            "dd" => new CssStyle { MarginLeft = 24f },
            "dt" => new CssStyle { Bold = true },
            "marquee" => new CssStyle { MarginTop = 4f, MarginBottom = 4f },
            _ => new CssStyle()
        };
    }
}
