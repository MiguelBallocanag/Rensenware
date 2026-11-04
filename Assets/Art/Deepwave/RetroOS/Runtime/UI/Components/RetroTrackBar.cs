using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Deepwave.RetroOS.UI.Components
{
    // Win95 trackbar: a sunken groove with a raised thumb, plus optional tick
    // marks under it. Used for volume, CCTV scrubbing, media position.
    public class RetroTrackBar : RetroWidget, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public event Action<float> Changed;

        // Fired when the user lets go - handy for seeking without spamming.
        public event Action<float> Committed;

        Image _groove;
        Image _thumb;
        RectTransform _thumbRect;
        RectTransform _canvasRect;
        float _value;
        int _ticks;

        public float Value
        {
            get => _value;
            set => SetValue(value, notify: false);
        }

        public static RetroTrackBar Create(Transform parent, float value, Action<float> onChanged,
            int ticks = 0)
        {
            var bar = Spawn<RetroTrackBar>(parent, "TrackBar");
            bar.Build(value, ticks);
            if (onChanged != null) bar.Changed += onChanged;
            return bar;
        }

        void Build(float value, int ticks)
        {
            _ticks = ticks;
            var hit = gameObject.AddComponent<Image>();
            hit.color = Color.clear;

            _groove = RetroUi.Panel(transform, PaletteSlot.FaceDark, "Groove");
            var grt = _groove.rectTransform;
            grt.anchorMin = new Vector2(0f, 0.5f);
            grt.anchorMax = new Vector2(1f, 0.5f);
            grt.pivot = new Vector2(0.5f, 0.5f);
            grt.sizeDelta = new Vector2(-8f, 4f);
            grt.anchoredPosition = new Vector2(0f, 2f);
            RetroUi.Bevel(grt, false, 1f);

            for (var i = 0; i < ticks; i++)
            {
                var t = ticks > 1 ? i / (float)(ticks - 1) : 0f;
                var tick = RetroUi.Panel(transform, PaletteSlot.FaceDark, "Tick");
                var trt = tick.rectTransform;
                trt.anchorMin = trt.anchorMax = new Vector2(t, 0f);
                trt.pivot = new Vector2(0.5f, 0f);
                trt.sizeDelta = new Vector2(1f, 4f);
                trt.anchoredPosition = new Vector2(Mathf.Lerp(5f, -5f, t), 1f);
                tick.raycastTarget = false;
            }

            _thumb = RetroUi.Panel(transform, PaletteSlot.Face, "Thumb");
            _thumbRect = _thumb.rectTransform;
            _thumbRect.anchorMin = _thumbRect.anchorMax = new Vector2(0f, 0.5f);
            _thumbRect.pivot = new Vector2(0.5f, 0.5f);
            _thumbRect.sizeDelta = new Vector2(11f, 21f);
            RetroUi.Bevel(_thumbRect, true, 1f);

            var canvas = GetComponentInParent<Canvas>();
            if (canvas) _canvasRect = (RectTransform)canvas.transform;

            _value = Mathf.Clamp01(value);
        }

        // Re-place the thumb on resize; the value setter handles the rest. No per-frame work.
        void OnRectTransformDimensionsChange() => PlaceThumb();

        void PlaceThumb()
        {
            if (!_thumbRect) return;
            var width = Rect.rect.width;
            var travel = Mathf.Max(0f, width - _thumbRect.sizeDelta.x);
            _thumbRect.anchoredPosition = new Vector2(
                _thumbRect.sizeDelta.x * 0.5f + travel * _value,
                _ticks > 0 ? 3f : 0f);
        }

        public void SetValue(float value, bool notify)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(_value, value)) return;
            _value = value;
            PlaceThumb();
            if (notify) Changed?.Invoke(value);
        }

        void Track(PointerEventData e)
        {
            if (!Interactable) return;
            var cam = e.pressEventCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, e.position, cam, out var local))
                return;

            var width = Rect.rect.width;
            var thumbW = _thumbRect ? _thumbRect.sizeDelta.x : 11f;
            var travel = Mathf.Max(1f, width - thumbW);
            var x = local.x - Rect.rect.xMin - thumbW * 0.5f;
            SetValue(x / travel, notify: true);
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (!RetroUi.IsPrimary(e)) return;
            Click();
            Track(e);
        }

        public void OnDrag(PointerEventData e) { if (RetroUi.IsPrimary(e)) Track(e); }

        public void OnPointerUp(PointerEventData e)
        {
            if (RetroUi.IsPrimary(e)) Committed?.Invoke(_value);
        }

        public override void ApplyTheme()
        {
            if (_groove) RetroPalette.Paint(_groove, PaletteSlot.FaceDark);
            if (_thumb) RetroPalette.Paint(_thumb, PaletteSlot.Face);
        }
    }

    // Win95 progress bar: a sunken well filled with discrete navy blocks.
    public class RetroProgressBar : RetroWidget
    {
        const float BlockWidth = 9f;
        const float BlockGap = 2f;

        RectTransform _blocks;
        Image _well;
        float _value;

        public float Value
        {
            get => _value;
            set { _value = Mathf.Clamp01(value); Rebuild(); }
        }

        public static RetroProgressBar Create(Transform parent, float value = 0f)
        {
            var bar = Spawn<RetroProgressBar>(parent, "ProgressBar");
            bar.Build(value);
            return bar;
        }

        void Build(float value)
        {
            _well = Background(transform, PaletteSlot.Face, "Well");
            RetroUi.Bevel(_well.rectTransform, false, 1f);
            _blocks = RetroUi.Stretch(RetroUi.NewRect("Blocks", transform), 3f);
            _value = Mathf.Clamp01(value);
        }

        void OnRectTransformDimensionsChange() => Rebuild();

        void Rebuild()
        {
            if (!_blocks) return;
            for (var i = _blocks.childCount - 1; i >= 0; i--) Destroy(_blocks.GetChild(i).gameObject);

            var width = _blocks.rect.width;
            if (width <= 1f) return;

            var total = Mathf.FloorToInt(width / (BlockWidth + BlockGap));
            var filled = Mathf.RoundToInt(total * _value);
            for (var i = 0; i < filled; i++)
            {
                var block = RetroUi.Panel(_blocks, PaletteSlot.TitleActive, "Block");
                var rt = block.rectTransform;
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(BlockWidth, 0f);
                rt.anchoredPosition = new Vector2(i * (BlockWidth + BlockGap), 0f);
                block.raycastTarget = false;
            }
        }

        public override void ApplyTheme()
        {
            if (_well) RetroPalette.Paint(_well, PaletteSlot.Face);
            Rebuild();
        }
    }
}
