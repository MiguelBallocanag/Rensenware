using UnityEngine;
using UnityEngine.InputSystem;

namespace Deepwave.RetroOS.Display
{
    /// <summary>Where the on-screen cursor is, in the OS picture's RenderTexture pixels - the coordinate a camera-mode <see cref="UnityEngine.UI.GraphicRaycaster"/> understands. Used only in the on-screen-cursor interaction mode.</summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public class RetroScreenPointer : MonoBehaviour
    {
        RetroScreenHost _host;

        // Hand-movement pixels to cursor pixels. 1 = a full-display sweep is a full-desktop sweep (sitting right in front of the monitor).
        float _cursorSpeed = 1f;

        // Picture-pixels a second at full stick deflection.
        float _stickSpeed = 1600f;

        Vector2 _position;
        bool _placed;

        // Which device last moved the pointer. The edge pin below is the mouse's
        // rule and only the mouse's: a pad player whose mouse happens to be
        // resting in a corner must not have the pointer dragged there.
        enum Driver { None, Mouse, Stick, Touch }

        Driver _driver;

        /// <summary>Cursor position in RenderTexture pixels, origin bottom-left.</summary>
        public Vector2 Position => _position;

        /// <summary>Screen-pixels of hand to picture-pixels of pointer. Settable so the Control Panel's pointer speed is a live setting rather than a scene one.</summary>
        public float CursorSpeed
        {
            get => _cursorSpeed;
            set => _cursorSpeed = Mathf.Clamp(value, 0.1f, 4f);
        }

        /// <summary>Picture-pixels a second at full gamepad-stick deflection.</summary>
        public float StickSpeed
        {
            get => _stickSpeed;
            set => _stickSpeed = Mathf.Clamp(value, 200f, 6000f);
        }

        /// <summary>Whether the cursor is inside the picture. A confined cursor always is; kept because the drawn cursor asks before it shows itself.</summary>
        public bool Over => isActiveAndEnabled;

        public void Configure(RetroScreenHost host, float cursorSpeed, float stickSpeed)
        {
            _host = host;
            if (cursorSpeed > 0f) _cursorSpeed = cursorSpeed;
            if (stickSpeed > 0f) _stickSpeed = stickSpeed;
        }

        Vector2Int Size => _host ? _host.TextureSize : new Vector2Int(1024, 768);

        void OnEnable()
        {
            // Comes back where it was left; starts in the middle the first time.
            if (_placed) return;
            var size = Size;
            _position = new Vector2(size.x * 0.5f, size.y * 0.5f);
            _placed = true;
        }

        void Update()
        {
            var size = Size;
            _position += FrameDelta(size);
            _position.x = Mathf.Clamp(_position.x, 0f, size.x);
            _position.y = Mathf.Clamp(_position.y, 0f, size.y);
            PinToEdges(size);
        }

        // How close to the display's edge the real pointer has to be to count as
        // against it. A share of the display rather than a fixed pixel, with a
        // floor and a ceiling: a browser only reports what it saw while the
        // pointer was over the canvas, and a fast flick out of it leaves the last
        // sample short of the very last column.
        static float EdgeMargin(float side) => Mathf.Clamp(side * 0.01f, 3f, 12f);

        /// <summary>
        /// Puts the drawn pointer on the picture's edge when the real one is on the
        /// display's.
        ///
        /// The real mouse runs out of desk at the edge of the window and stops
        /// reporting movement. The drawn one is somewhere else by then - the two
        /// clamp against different rectangles, and any pointer speed but exactly 1
        /// pulls them apart on its own - so it stops with the real one and will not
        /// go any further that way: a pointer held against a wall that is not there.
        /// Both are as far left as they go, so they belong in the same place, and
        /// putting them there keeps every edge of the picture reachable however far
        /// the two have drifted.
        ///
        /// It is also the only thing that makes the mode work in a browser, where
        /// <c>CursorLockMode.Confined</c> does nothing at all.
        /// </summary>
        void PinToEdges(Vector2Int size)
        {
            if (_driver != Driver.Mouse) return;

            var mouse = RetroScreenInput.RealMouse;
            if (mouse == null) return;

            var real = mouse.position.ReadValue();
            if (Screen.width <= 1 || Screen.height <= 1) return;

            var marginX = EdgeMargin(Screen.width);
            var marginY = EdgeMargin(Screen.height);

            if (real.x <= marginX) _position.x = 0f;
            else if (real.x >= Screen.width - marginX) _position.x = size.x;

            if (real.y <= marginY) _position.y = 0f;
            else if (real.y >= Screen.height - marginY) _position.y = size.y;
        }

        /// <summary>Drops the cursor at a point in the picture at once, rather than letting it travel there. For a machine that wants to seat the player with the pointer already on something.</summary>
        public void WarpTo(Vector2 texturePosition)
        {
            var size = Size;
            _position = new Vector2(
                Mathf.Clamp(texturePosition.x, 0f, size.x),
                Mathf.Clamp(texturePosition.y, 0f, size.y));
            _placed = true;
        }

        // This frame's movement in picture pixels, across all relative devices. A mouse/finger
        // delta is scaled to the picture; a stick direction is moved at a fixed rate.
        Vector2 FrameDelta(Vector2Int size)
        {
            var move = Vector2.zero;
            var toPicture = new Vector2(
                size.x / Mathf.Max(1f, Screen.width),
                size.y / Mathf.Max(1f, Screen.height));

            // The hardware mouse, not Mouse.current: one mouse in the system is the OS's own device, and reading its delta back would move the cursor by itself.
            var mouse = RetroScreenInput.RealMouse;
            if (mouse != null)
            {
                var d = mouse.delta.ReadValue();
                if (d.sqrMagnitude > 0f) _driver = Driver.Mouse;
                move += new Vector2(d.x * toPicture.x, d.y * toPicture.y) * _cursorSpeed;
            }

            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed)
            {
                var d = touch.primaryTouch.delta.ReadValue();
                if (d.sqrMagnitude > 0f) _driver = Driver.Touch;
                move += new Vector2(d.x * toPicture.x, d.y * toPicture.y) * _cursorSpeed;
            }

            var pad = Gamepad.current;
            if (pad != null)
            {
                var s = pad.rightStick.ReadValue();
                // A small centre band so a resting stick does not creep.
                if (s.sqrMagnitude > 0.02f)
                {
                    _driver = Driver.Stick;
                    move += s * (_stickSpeed * Time.unscaledDeltaTime);
                }
            }

            return move;
        }
    }
}
