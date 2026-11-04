using UnityEngine;
using UnityEngine.UI;

namespace Deepwave.RetroOS.Display
{
    /// <summary>The drawn pointer for the on-screen-cursor interaction mode: a classic arrow on the top-most layer of the OS canvas, following <see cref="RetroScreenPointer"/>.</summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public class RetroVirtualCursor : MonoBehaviour
    {
        RetroScreenHost _host;
        RetroScreenPointer _pointer;
        RectTransform _layer;
        RectTransform _arrow;
        bool _visible;
        bool _tracking = true;

        // Where the arrow rests when not tracking - the canvas centre for an unused machine.
        Vector2 _restPosition;

        public static RetroVirtualCursor Create(RetroScreenHost host, RetroScreenPointer pointer)
        {
            var go = new GameObject("OS Cursor", typeof(RectTransform), typeof(RetroVirtualCursor));
            var cursor = go.GetComponent<RetroVirtualCursor>();
            cursor._host = host;
            cursor._pointer = pointer;

            cursor._layer = (RectTransform)go.transform;
            cursor._layer.SetParent(host.Root, false);
            cursor._layer.anchorMin = Vector2.zero;
            cursor._layer.anchorMax = Vector2.one;
            cursor._layer.offsetMin = cursor._layer.offsetMax = Vector2.zero;
            cursor._layer.SetAsLastSibling();

            var image = new GameObject("Arrow", typeof(RectTransform)).AddComponent<Image>();
            cursor._arrow = image.rectTransform;
            cursor._arrow.SetParent(cursor._layer, false);
            cursor._arrow.anchorMin = cursor._arrow.anchorMax = new Vector2(0.5f, 0.5f);

            var sprite = CursorSprite();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.SetNativeSize();
            // Hot-spot at the tip, a pixel or two in from the top-left corner.
            cursor._arrow.pivot = new Vector2(1.5f / sprite.rect.width, 1f - 1.5f / sprite.rect.height);

            cursor.SetVisible(false);
            return cursor;
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (_layer) _layer.gameObject.SetActive(visible);
        }

        /// <summary>Whether the arrow follows the pointer - the player is sitting at the machine - or holds still where it was last left. An idle computer keeps its pointer where it lies; it does not chase a mouse that is being used to look around the room.</summary>
        public void SetTracking(bool tracking) => _tracking = tracking;

        void LateUpdate()
        {
            if (!_visible || !_host || !_arrow) return;

            // Only reassert the top slot when something (a shutdown screen) has
            // jumped ahead - reparenting every frame would rebuild the canvas.
            if (_layer.parent && _layer.GetSiblingIndex() != _layer.parent.childCount - 1)
                _layer.SetAsLastSibling();

            // Parked: shown, wherever it was last left, whatever the (now
            // stale) pointer says about being over the glass.
            if (!_tracking)
            {
                if (!_arrow.gameObject.activeSelf) _arrow.gameObject.SetActive(true);
                _arrow.anchoredPosition = _restPosition;
                return;
            }

            if (_pointer == null) return;

            var over = _pointer.Over;
            if (_arrow.gameObject.activeSelf != over) _arrow.gameObject.SetActive(over);
            if (!over) return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _layer, _pointer.Position, _host.Projector, out var local))
            {
                _arrow.anchoredPosition = local;
                // Remembered, so the arrow stays here the moment tracking stops.
                _restPosition = local;
            }
        }

        // --- the arrow ---------------------------------------------------------

        static Sprite s_sprite;

        static Sprite CursorSprite()
        {
            if (s_sprite) return s_sprite;

            const int w = 16;
            const int h = 24;

            // Classic pointer outline, authoring coords with y running down.
            var poly = new[]
            {
                new Vector2(1, 1), new Vector2(1, 17), new Vector2(5, 13), new Vector2(8, 20),
                new Vector2(10, 19), new Vector2(7, 12), new Vector2(13, 12)
            };

            bool Inside(float px, float py)
            {
                var hit = false;
                for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                {
                    var a = poly[i];
                    var b = poly[j];
                    if (a.y > py != b.y > py &&
                        px < (b.x - a.x) * (py - a.y) / (b.y - a.y) + a.x)
                        hit = !hit;
                }
                return hit;
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var px = new Color32[w * h];
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    // Sample at the pixel centre; flip Y into texture space.
                    var ax = x + 0.5f;
                    var ay = h - 1 - y + 0.5f;

                    Color32 c;
                    if (Inside(ax, ay)) c = new Color32(255, 255, 255, 255);
                    else if (Inside(ax - 1, ay) || Inside(ax + 1, ay) ||
                             Inside(ax, ay - 1) || Inside(ax, ay + 1) ||
                             Inside(ax - 1, ay - 1) || Inside(ax + 1, ay - 1) ||
                             Inside(ax - 1, ay + 1) || Inside(ax + 1, ay + 1))
                        c = new Color32(0, 0, 0, 255);
                    else c = new Color32(0, 0, 0, 0);

                    px[y * w + x] = c;
                }

            tex.SetPixels32(px);
            tex.Apply();

            return s_sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0f, 1f), 100f);
        }
    }
}
