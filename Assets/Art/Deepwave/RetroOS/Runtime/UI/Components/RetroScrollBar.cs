using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Deepwave.RetroOS.UI.Components
{
    /// <summary>
    /// The three-part scroll bar: an arrow button at each end, a dithered trough,
    /// and a raised thumb sized to what is on screen. The middle is a uGUI
    /// <see cref="Scrollbar"/> the <see cref="ScrollRect"/> drives; the arrows are ours.
    /// </summary>
    [AddComponentMenu("")]
    public class RetroScrollBar : RetroWidget
    {
        /// <summary>Width of a vertical bar, and height of a horizontal one.</summary>
        public const float Thickness = 16f;

        // What one arrow press moves, as a fraction of the content; holding repeats it.
        const float LineStep = 0.08f;
        const float RepeatDelay = 0.35f, RepeatEvery = 0.05f;

        ScrollRect _scroll;
        Scrollbar _bar;
        Image _trough;
        Image _handle;
        RetroBevel _handleBevel;
        RetroArrowButton _up, _down;
        RectTransform _slide;
        bool _horizontal;

        public Scrollbar Bar => _bar;

        /// <summary>Builds a bar on a scroll area's edge and hands it to the ScrollRect, which keeps the thumb in step.</summary>
        public static RetroScrollBar Create(ScrollRect scroll, bool horizontal = false)
        {
            if (!scroll) return null;

            var widget = Spawn<RetroScrollBar>(scroll.transform, horizontal ? "HScroll" : "VScroll");
            widget._scroll = scroll;
            widget._horizontal = horizontal;

            var rt = widget.Rect;
            if (horizontal)
            {
                rt.anchorMin = new Vector2(0, 0);
                rt.anchorMax = new Vector2(1, 0);
                rt.pivot = new Vector2(0.5f, 0);
                rt.sizeDelta = new Vector2(-4, Thickness);
                rt.anchoredPosition = new Vector2(0, 2);
            }
            else
            {
                rt.anchorMin = new Vector2(1, 0);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(1, 0.5f);
                rt.sizeDelta = new Vector2(Thickness, -4);
                rt.anchoredPosition = new Vector2(-2, 0);
            }

            widget.Build();
            return widget;
        }

        void Build()
        {
            // The trough is a white/silver checkerboard - reads as a texture, not a colour.
            _trough = Background(transform, Color.white, "Trough");
            _trough.sprite = RetroTextures.Checker;
            _trough.type = Image.Type.Tiled;
            _trough.color = Color.white;

            var slide = _slide = RetroUi.NewRect("Sliding Area", transform);
            if (_horizontal)
            {
                slide.anchorMin = new Vector2(0, 0);
                slide.anchorMax = new Vector2(1, 1);
                slide.offsetMin = new Vector2(Thickness, 0);
                slide.offsetMax = new Vector2(-Thickness, 0);
            }
            else
            {
                slide.anchorMin = new Vector2(0, 0);
                slide.anchorMax = new Vector2(1, 1);
                slide.offsetMin = new Vector2(0, Thickness);
                slide.offsetMax = new Vector2(0, -Thickness);
            }

            var handleRect = RetroUi.NewRect("Handle", slide);
            // The Scrollbar drives only anchors, so thumb size = span + sizeDelta.
            // Zero it, or the default 100x100 makes a slab hanging outside the bar.
            handleRect.sizeDelta = Vector2.zero;
            handleRect.anchoredPosition = Vector2.zero;
            _handle = handleRect.gameObject.AddComponent<Image>();
            RetroPalette.Paint(_handle, PaletteSlot.Face);
            _handleBevel = Border(handleRect, BorderStyle.Raised);

            _bar = gameObject.AddComponent<Scrollbar>();
            _bar.transition = Selectable.Transition.None;
            _bar.handleRect = handleRect;
            _bar.direction = _horizontal
                ? Scrollbar.Direction.LeftToRight
                : Scrollbar.Direction.BottomToTop;
            _bar.targetGraphic = _handle;

            // A thumb thinner than this cannot be grabbed.
            _bar.size = 0.2f;

            _up = RetroArrowButton.Create(transform, _horizontal ? 3 : 0, () => Step(-1));
            _down = RetroArrowButton.Create(transform, _horizontal ? 2 : 1, () => Step(1));
            Place(_up.Rect, atStart: true);
            Place(_down.Rect, atStart: false);

            if (_horizontal)
            {
                _scroll.horizontalScrollbar = _bar;
                _scroll.horizontalScrollbarVisibility =
                    ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            }
            else
            {
                _scroll.verticalScrollbar = _bar;
                _scroll.verticalScrollbarVisibility =
                    ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            }
        }

        void Place(RectTransform rt, bool atStart)
        {
            if (_horizontal)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(atStart ? 0 : 1, 0.5f);
                rt.pivot = new Vector2(atStart ? 0 : 1, 0.5f);
            }
            else
            {
                // Up is at the top, down at the bottom.
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, atStart ? 1 : 0);
                rt.pivot = new Vector2(0.5f, atStart ? 1 : 0);
            }
            rt.sizeDelta = new Vector2(Thickness, Thickness);
            rt.anchoredPosition = Vector2.zero;
        }

        // One line, clamped so holding the arrow at the end does nothing.
        void Step(int direction)
        {
            if (!_scroll) return;
            if (_horizontal)
                _scroll.horizontalNormalizedPosition =
                    Mathf.Clamp01(_scroll.horizontalNormalizedPosition + direction * LineStep);
            else
                _scroll.verticalNormalizedPosition =
                    Mathf.Clamp01(_scroll.verticalNormalizedPosition - direction * LineStep);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Canvas.willRenderCanvases += ClampThumb;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Canvas.willRenderCanvases -= ClampThumb;
        }

        // The ScrollRect puts no floor under the thumb size; Win95 stopped shrinking
        // it at the bar's width. Runs after the ScrollRect has had its say.
        void ClampThumb()
        {
            if (!_bar || !_slide) return;
            var length = _horizontal ? _slide.rect.width : _slide.rect.height;
            if (length <= 1f) return;

            var minimum = Mathf.Clamp01(Thickness / length);
            if (_bar.size < minimum) _bar.size = minimum;
        }

        public override void ApplyTheme()
        {
            // The trough is a sprite, so re-fetch it - the held one is the old scheme's.
            if (_trough) _trough.sprite = RetroTextures.Checker;
            if (_handle) RetroPalette.Paint(_handle, PaletteSlot.Face);
            if (_handleBevel) _handleBevel.SetRaised(true);
            if (_up) _up.ApplyTheme();
            if (_down) _down.ApplyTheme();
        }

        // --- the arrow at each end ---

        /// <summary>A square button with a solid triangle that keeps firing while held.</summary>
        public class RetroArrowButton : RetroWidget, IPointerDownHandler, IPointerUpHandler
        {
            System.Action _step;
            Image _face;
            RetroBevel _bevel;
            Image _glyph;
            float _nextRepeat;
            bool _held;

            public static RetroArrowButton Create(Transform parent, int direction,
                System.Action step)
            {
                var widget = Spawn<RetroArrowButton>(parent, "Arrow");
                widget._step = step;
                widget._face = Background(widget.transform, PaletteSlot.Face, "Face");
                widget._bevel = Border(widget.Rect, BorderStyle.Raised);

                widget._glyph = RetroUi.NewRect("Glyph", widget.transform)
                    .gameObject.AddComponent<Image>();
                widget._glyph.sprite = RetroTextures.Arrow(direction);
                widget._glyph.raycastTarget = false;
                RetroUi.Stretch(widget._glyph.rectTransform);
                return widget;
            }

            public void OnPointerDown(PointerEventData eventData)
            {
                if (!Interactable) return;
                _held = true;
                _bevel?.SetRaised(false);
                _step?.Invoke();
                _nextRepeat = Time.unscaledTime + RepeatDelay;
                Click();
            }

            public void OnPointerUp(PointerEventData eventData)
            {
                _held = false;
                _bevel?.SetRaised(true);
            }

            void Update()
            {
                if (!_held || Time.unscaledTime < _nextRepeat) return;
                _step?.Invoke();
                _nextRepeat = Time.unscaledTime + RepeatEvery;
            }

            public override void ApplyTheme()
            {
                if (_face) RetroPalette.Paint(_face, PaletteSlot.Face);
            }
        }
    }
}
