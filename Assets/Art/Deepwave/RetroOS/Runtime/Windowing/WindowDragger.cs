using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Windowing
{
    /// <summary>The grab handle of a movable surface: a window title bar, a dialog caption, a floating palette. Drags its target inside a bounding area and keeps it there.</summary>
    [AddComponentMenu("")]
    public class WindowDragger : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler
    {
        /// <summary>The rect that moves. Defaults to this object's own rect.</summary>
        public RectTransform dragObject;

        /// <summary>The rect the target is kept inside. Defaults to the parent chain's root.</summary>
        public RectTransform dragArea;

        /// <summary>Extra room to leave at the bottom of the area - the taskbar strip.</summary>
        public float bottomInset;

        /// <summary>Called on press, before the drag starts.</summary>
        public Action onFocus;

        /// <summary>Called after every move, once the target has been clamped.</summary>
        public Action onMoved;

        /// <summary>Double-clicking the handle runs this - maximize, on a window.</summary>
        public Action onDoubleClick;

        /// <summary>Return false to pin the target in place (a maximized window).</summary>
        public Func<bool> canDrag;

        Vector2 _grabOffset;
        Vector3 _startPosition;
        float _lastClickTime;

        RectTransform Target => dragObject ? dragObject : (RectTransform)transform;

        RectTransform Area
        {
            get
            {
                if (dragArea) return dragArea;

                // Walk to the outermost rect - the canvas - the way a window
                // manager would if nobody told it where the desktop ends.
                var rect = (RectTransform)transform;
                while (rect.parent is RectTransform parent) rect = parent;
                dragArea = rect;
                return dragArea;
            }
        }

        void Awake()
        {
            // A bar made only of labels has nothing to raycast against, and a
            // window with a transparent caption could not be dragged at all.
            if (!GetComponent<Graphic>())
            {
                var hit = gameObject.AddComponent<Image>();
                hit.color = Color.clear;
                hit.raycastTarget = true;
            }
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (!RetroUi.IsPrimary(e)) return;
            onFocus?.Invoke();

            // uGUI only reports clickCount on click, which never fires when the
            // pointer moved at all - so double-click is timed here instead.
            var now = Time.unscaledTime;
            if (now - _lastClickTime < 0.35f)
            {
                _lastClickTime = 0f;
                onDoubleClick?.Invoke();
                return;
            }
            _lastClickTime = now;
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (!RetroUi.IsPrimary(e) || !Movable) return;
            _startPosition = Target.localPosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                Area, e.position, e.pressEventCamera, out _grabOffset);
        }

        public void OnDrag(PointerEventData e)
        {
            if (!RetroUi.IsPrimary(e) || !Movable) return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    Area, e.position, e.pressEventCamera, out var pointer)) return;

            Target.localPosition = _startPosition + (Vector3)(pointer - _grabOffset);
            ClampToArea();
            onMoved?.Invoke();
        }

        bool Movable => Target && (canDrag == null || canDrag());

        /// <summary>Pulls the target back inside the drag area. Public because a resize can push a window past the edge without anything being dragged.</summary>
        public void ClampToArea()
        {
            var target = Target;
            if (!target || !Area) return;

            var area = Area.rect;

            // A drag area with no size yet (first layout not run) has nothing to clamp inside, so leave the target put.
            if (area.width <= 1f || area.height <= 1f) return;

            var self = target.rect;

            // Corner-to-corner, so the whole window stays on the desktop
            // whatever pivot it happens to use.
            var min = (Vector2)area.min - self.min;
            var max = (Vector2)area.max - self.max;
            min.y += bottomInset;

            // A target bigger than the area cannot satisfy both edges; centre
            // it rather than pinning it to one of them.
            var pos = target.localPosition;
            pos.x = min.x <= max.x ? Mathf.Clamp(pos.x, min.x, max.x) : (min.x + max.x) * 0.5f;
            pos.y = min.y <= max.y ? Mathf.Clamp(pos.y, min.y, max.y) : (min.y + max.y) * 0.5f;
            target.localPosition = pos;
        }
    }
}
