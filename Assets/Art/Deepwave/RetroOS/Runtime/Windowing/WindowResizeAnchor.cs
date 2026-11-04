using System;
using UnityEngine;
using UnityEngine.EventSystems;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Windowing
{
    /// <summary>Which corner a window is resized from.</summary>
    public enum ResizeCorner
    {
        /// <summary>Not resizable.</summary>
        None,
        BottomRight,
        BottomLeft
    }

    /// <summary>The corner grip that resizes a window. It pins the opposite corner by pivoting the frame there, so a window grows away from the handle and its title bar never walks across the desktop while you drag.</summary>
    [AddComponentMenu("")]
    public class WindowResizeAnchor : MonoBehaviour, IPointerDownHandler, IDragHandler, IEndDragHandler
    {
        /// <summary>The frame being resized.</summary>
        public RectTransform targetRect;

        /// <summary>Smallest size the frame may take.</summary>
        public Vector2 minSize = new(320f, 220f);

        /// <summary>Largest size the frame may take, asked for as the drag happens.</summary>
        public Func<Vector2> maxSize;

        /// <summary>Called on press - brings the window forward.</summary>
        public Action onFocus;

        /// <summary>Called after every resize step.</summary>
        public Action onResized;

        Vector2 _lastPointer;
        bool _mirrorX;

        /// <summary>Moves a rect's pivot without moving the rect: the offset the pivot change would have caused is taken back out of the position.</summary>
        public static void SetPivot(RectTransform rect, Vector2 pivot)
        {
            if (!rect) return;
            var offset = pivot - rect.pivot;
            offset.Scale(rect.rect.size);
            var world = rect.position + rect.TransformVector(offset);
            rect.pivot = pivot;
            rect.position = world;
        }

        /// <summary>Parks the grip in a corner and pivots the frame at the opposite one. Re-runnable: a window may change which corner it grows from.</summary>
        public void SetCorner(ResizeCorner corner)
        {
            var rect = (RectTransform)transform;

            if (corner == ResizeCorner.BottomLeft)
            {
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                SetPivot(rect, Vector2.zero);
                SetPivot(targetRect, Vector2.one);          // pin the top-right
                _mirrorX = true;
            }
            else
            {
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
                SetPivot(rect, new Vector2(1f, 0f));
                SetPivot(targetRect, new Vector2(0f, 1f));  // pin the top-left
                _mirrorX = false;
            }

            rect.anchoredPosition = new Vector2(_mirrorX ? 2f : -2f, 2f);
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (!targetRect || !RetroUi.IsPrimary(e)) return;
            onFocus?.Invoke();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                targetRect, e.position, e.pressEventCamera, out _lastPointer);
        }

        public void OnDrag(PointerEventData e)
        {
            if (!targetRect || !RetroUi.IsPrimary(e)) return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    targetRect, e.position, e.pressEventCamera, out var pointer)) return;

            var moved = pointer - _lastPointer;
            var size = targetRect.sizeDelta;

            // Dragging down always means taller, whichever corner is held; only
            // the horizontal sense flips.
            size += new Vector2(_mirrorX ? -moved.x : moved.x, -moved.y);

            var limit = maxSize != null ? maxSize() : new Vector2(4096f, 4096f);
            targetRect.sizeDelta = new Vector2(
                Mathf.Clamp(size.x, minSize.x, Mathf.Max(minSize.x, limit.x)),
                Mathf.Clamp(size.y, minSize.y, Mathf.Max(minSize.y, limit.y)));

            _lastPointer = pointer;
            onResized?.Invoke();
        }

        public void OnEndDrag(PointerEventData e) => onResized?.Invoke();
    }
}
