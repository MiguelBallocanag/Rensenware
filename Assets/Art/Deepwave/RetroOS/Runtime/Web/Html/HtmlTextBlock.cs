using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Deepwave.RetroOS.Web.Html
{
    // A paragraph of rendered HTML with working inline links. The text is one rich-text
    // label so it wraps like running text; links are character ranges recorded while the
    // run was built. Laying it out turns each range into one rect per line it covers -
    // that rect is both what the underline is drawn on and what a click is tested against.
    public class HtmlTextBlock : MonoBehaviour, IPointerClickHandler,
        IPointerMoveHandler, IPointerExitHandler
    {
        public struct LinkRange
        {
            public int Start;      // first visible character
            public int End;        // one past the last visible character
            public string Href;
            public string Title;
            public bool Underline;
            public Color Color;
        }

        readonly List<LinkRange> _links = new();
        readonly List<RectTransform> _rules = new();

        // Where each link is on screen: one entry per link per wrapped line, in label-local space. Filled by layout, read by click and hover.
        readonly List<(int Link, Rect Area)> _boxes = new();

        // Visible char index -> generator char-array index. They differ: TextGenerator keeps
        // an entry per character of the string it was given, rich-text markup included.
        readonly List<int> _visibleToRaw = new();
        string _mappedText;

        Text _text;
        Action<string> _navigate;
        Action<string> _hover;
        int _hovered = -1;
        int _ruledFor = -1;
        Vector2 _ruledSize = new(-1f, -1f);

        public static HtmlTextBlock Attach(Text text, IEnumerable<LinkRange> links,
            Action<string> navigate, Action<string> hover = null)
        {
            var block = text.gameObject.AddComponent<HtmlTextBlock>();
            block._text = text;
            block._navigate = navigate;
            block._hover = hover;
            block._links.AddRange(links);
            text.raycastTarget = true;
            return block;
        }

        void OnEnable() => Canvas.willRenderCanvases += Measure;
        void OnDisable() => Canvas.willRenderCanvases -= Measure;

        // Places every link once the run is laid out and underlines the ones that asked:
        // a 1995 link is underlined, and legacy uGUI text has no underline tag, so the rules are 1px rects over the run.
        void Measure()
        {
            if (!_text || _links.Count == 0) return;

            var generator = _text.cachedTextGenerator;
            if (generator == null || generator.characterCount == 0) return;

            // Only on a real re-layout. The signature includes line count and first-line top,
            // not just char count - a paragraph is generated once to measure and again at final width, with the same char count.
            var size = _text.rectTransform.rect.size;
            var lineCount = generator.lines.Count;
            var top = lineCount > 0 ? Mathf.RoundToInt(generator.lines[0].topY) : 0;
            var signature = generator.characterCount * 397 + lineCount * 31 + top;
            if (signature == _ruledFor && size == _ruledSize) return;
            _ruledFor = signature;
            _ruledSize = size;

            foreach (var rule in _rules) if (rule) Destroy(rule.gameObject);
            _rules.Clear();
            _boxes.Clear();

            var scale = _text.pixelsPerUnit;
            if (scale <= 0f) scale = 1f;

            var characters = generator.characters;
            var lines = generator.lines;
            var pivot = _text.rectTransform.pivot;

            for (var index = 0; index < _links.Count; index++)
            {
                var link = _links[index];

                // Both ends translated out of visible space and into the
                // generator's own indices before anything is measured.
                var rawStart = RawOf(link.Start);
                var rawEnd = RawOf(link.End - 1);
                if (rawStart < 0 || rawEnd < 0) continue;

                for (var l = 0; l < lines.Count; l++)
                {
                    var first = lines[l].startCharIdx;
                    var last = l + 1 < lines.Count ? lines[l + 1].startCharIdx : characters.Count;

                    var from = Mathf.Max(first, rawStart);
                    var to = Mathf.Min(last - 1, rawEnd);
                    if (to < from || from >= characters.Count) continue;

                    var x0 = characters[from].cursorPos.x / scale;
                    var lastIndex = Mathf.Min(to, characters.Count - 1);
                    var x1 = (characters[lastIndex].cursorPos.x + characters[lastIndex].charWidth) / scale;
                    if (x1 - x0 < 1f) continue;

                    // The whole line box - what a pointer is over; the underline is a decoration on the same span.
                    var lineTop = lines[l].topY / scale;
                    var lineBottom = lineTop - lines[l].height / scale;
                    _boxes.Add((index, new Rect(x0, lineBottom, x1 - x0, lineTop - lineBottom)));

                    if (!link.Underline) continue;

                    // Two pixels under the baseline: on the baseline itself the descenders of g/y/p cross the rule and it reads as a strike-through.
                    var y = lines[l].topY / scale - _text.fontSize - 1f;

                    // Snapped to whole canvas pixels: a table gives rows fractional heights, and a 1px
                    // rule at a fractional offset splits across two pixel rows and comes out at half strength.
                    var origin = _text.rectTransform.position;
                    var snappedX = Mathf.Round(origin.x + x0) - origin.x;
                    var snappedY = Mathf.Round(origin.y + y) - origin.y;

                    var rule = new GameObject("Underline", typeof(RectTransform));
                    rule.transform.SetParent(_text.transform, false);
                    var rt = (RectTransform)rule.transform;
                    rt.anchorMin = rt.anchorMax = pivot;
                    rt.pivot = new Vector2(0f, 0.5f);
                    rt.sizeDelta = new Vector2(Mathf.Round(x1 - x0), 1f);
                    rt.anchoredPosition = new Vector2(snappedX, snappedY);

                    var image = rule.AddComponent<Image>();
                    image.color = link.Color;
                    image.raycastTarget = false;
                    _rules.Add(rt);
                }
            }
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (!UI.RetroUi.IsPrimary(e)) return;
            var link = LinkAt(e);
            if (link.HasValue) _navigate?.Invoke(_links[link.Value].Href);
        }

        public void OnPointerMove(PointerEventData e)
        {
            var link = LinkAt(e);
            var index = link ?? -1;
            if (index == _hovered) return;
            _hovered = index;
            _hover?.Invoke(index >= 0 ? _links[index].Href : null);
        }

        public void OnPointerExit(PointerEventData e)
        {
            if (_hovered < 0) return;
            _hovered = -1;
            _hover?.Invoke(null);
        }

        void EnsureMap()
        {
            if (!_text) return;
            var value = _text.text ?? "";
            if (_mappedText != null && string.Equals(_mappedText, value, StringComparison.Ordinal)) return;

            _mappedText = value;
            _visibleToRaw.Clear();
            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] == '<')
                {
                    var close = value.IndexOf('>', i);
                    if (close >= 0) { i = close; continue; }
                }
                _visibleToRaw.Add(i);
            }
        }

        // The generator index of a visible character, or -1.
        int RawOf(int visible)
        {
            EnsureMap();
            return visible >= 0 && visible < _visibleToRaw.Count ? _visibleToRaw[visible] : -1;
        }

        // Which link the pointer is over, or null. One rectangle test against
        // the boxes the layout pass already worked out.
        int? LinkAt(PointerEventData e)
        {
            if (_boxes.Count == 0 || !_text) return null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _text.rectTransform, e.position, e.pressEventCamera, out var local))
                return null;

            foreach (var (link, area) in _boxes)
                if (area.Contains(local)) return link;

            return null;
        }
    }
}
