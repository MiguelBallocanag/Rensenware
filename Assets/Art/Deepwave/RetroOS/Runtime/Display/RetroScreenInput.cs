using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;

namespace Deepwave.RetroOS.Display
{
    /// <summary>Drives the OS's uGUI from a virtual pointer instead of the real mouse, for the on-screen-cursor interaction mode.</summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public class RetroScreenInput : MonoBehaviour
    {
        internal const string DeviceName = "RetroScreenMouse";

        /// <summary>The hardware mouse - never the private device this shim drives.</summary>
        internal static Mouse RealMouse
        {
            get
            {
                var current = Mouse.current;
                if (current != null && current.name != DeviceName)
                    return current;

                foreach (var device in InputSystem.devices)
                    if (device is Mouse mouse && mouse.name != DeviceName)
                        return mouse;

                return null;
            }
        }

        RetroScreenPointer _pointer;
        InputSystemUIInputModule _module;

        Mouse _device;
        InputActionAsset _asset;
        InputActionMap _map;
        InputActionReference _point, _left, _right, _middle, _scroll;
        InputActionReference _savedPoint, _savedLeft, _savedRight, _savedMiddle, _savedScroll;
        bool _wired;
        bool _saved;
        bool _holding;
        bool _active;
        Vector2 _lastPosition;

        // Touch tap -> left click: a quick press that does not travel; a press that drags moves the cursor and never clicks.
        double _touchDownAt;
        Vector2 _touchDownPos;
        bool _touchDragged;
        int _tapHold;

        const float TapSeconds = 0.3f;
        const float TapSlopSqr = 20f * 20f;

        public void Bind(RetroScreenPointer pointer, InputSystemUIInputModule module)
        {
            _pointer = pointer;
            _module = module;
            Wire();
        }

        /// <summary>Turns the shim on for <paramref name="owner"/>, or off again.</summary>
        public void SetActive(bool active, RetroScreenPointer owner)
        {
            if (active)
            {
                _pointer = owner;
                _active = true;
                Take();
                return;
            }

            // Not ours to switch off.
            if (owner != null && owner != _pointer) return;

            _active = false;
            _pointer = null;

            // The module reverts to the game's actions when nobody is at an on-screen-cursor machine;
            // held for the whole scene, it left every other canvas reading a device parked at (-1,-1).
            Release();

            if (_device == null) return;

            // Let go of anything held, and park the pointer off the picture so a
            // stale hover does not linger on the screen.
            InputState.Change(_device, new MouseState { position = new Vector2(-1f, -1f) });
        }

        void OnEnable()
        {
            if (_module) Wire();
        }

        void OnDisable() => Unwire();

        void OnDestroy() => Unwire();

        void Update()
        {
            if (!_active || _pointer == null || _device == null) return;

            var position = _pointer.Position;
            var mouse = RealMouse;
            var pad = Gamepad.current;

            var left = mouse != null && mouse.leftButton.isPressed;
            var right = mouse != null && mouse.rightButton.isPressed;
            var middle = mouse != null && mouse.middleButton.isPressed;

            // A gamepad face button clicks too, so the OS is usable from a
            // controller with the right stick walking the cursor.
            if (pad != null)
            {
                left |= pad.buttonSouth.isPressed;
                right |= pad.buttonEast.isPressed;
            }

            if (TapThisFrame()) left = true;

            var scroll = mouse != null ? mouse.scroll.ReadValue() : Vector2.zero;
            if (pad != null && Mathf.Abs(pad.leftStick.y.ReadValue()) > 0.5f)
                scroll += new Vector2(0f, pad.leftStick.y.ReadValue() * 8f);

            var state = new MouseState
            {
                position = position,
                delta = position - _lastPosition,
                scroll = scroll
            };
            state = state.WithButton(MouseButton.Left, left);
            state = state.WithButton(MouseButton.Right, right);
            state = state.WithButton(MouseButton.Middle, middle);

            InputState.Change(_device, state);
            _lastPosition = position;
        }

        // True for the one frame a quick tap reads as a press; the press is held a couple of frames for a clean down-up.
        bool TapThisFrame()
        {
            if (_tapHold > 0)
            {
                _tapHold--;
                return true;
            }

            var touch = Touchscreen.current;
            if (touch == null) return false;

            var t = touch.primaryTouch;
            if (t.press.wasPressedThisFrame)
            {
                _touchDownAt = Time.unscaledTimeAsDouble;
                _touchDownPos = t.position.ReadValue();
                _touchDragged = false;
            }
            else if (t.press.isPressed && !_touchDragged)
            {
                if ((t.position.ReadValue() - _touchDownPos).sqrMagnitude > TapSlopSqr)
                    _touchDragged = true;
            }
            else if (t.press.wasReleasedThisFrame
                     && !_touchDragged
                     && Time.unscaledTimeAsDouble - _touchDownAt <= TapSeconds)
            {
                _tapHold = 2;
                return true;
            }

            return false;
        }

        // --- wiring ----------------------------------------------------------

        void Wire()
        {
            if (_wired || _module == null) return;

            // Nothing here is worth losing the machine over: on failure the OS still boots, module left on its current actions.
            try
            {
                Build();
                _wired = true;
            }
            catch (System.Exception e)
            {
                Debug.LogError("RetroOS: could not install the on-screen pointer, so the OS is " +
                               $"drawn but not clickable. {e.Message}");
                Unwire();
                return;
            }

            // Built, not taken: SetActive points the module at this device, so the shared EventSystem stays the game's until someone sits down.
            if (_active) Take();
        }

        void Build()
        {
            _device = FindDevice() ?? InputSystem.AddDevice<Mouse>(DeviceName);

            // The actions must live in an asset: InputActionReference.Create refuses a map with no asset, and the module takes references.
            _asset = ScriptableObject.CreateInstance<InputActionAsset>();
            _asset.name = "RetroScreenUI";
            _asset.hideFlags = HideFlags.HideAndDontSave;

            _map = _asset.AddActionMap("Pointer");

            // Pass-Through, not Value, for all of them - what the UI module's own defaults use. A Value
            // action latches to one control and runs conflict resolution, which loses track of the pointer between desks.
            _point = Reference(_map.AddAction("point", InputActionType.PassThrough, "<Mouse>/position"));
            _left = Reference(_map.AddAction("leftClick", InputActionType.PassThrough, "<Mouse>/leftButton"));
            _right = Reference(_map.AddAction("rightClick", InputActionType.PassThrough, "<Mouse>/rightButton"));
            _middle = Reference(_map.AddAction("middleClick", InputActionType.PassThrough, "<Mouse>/middleButton"));
            _scroll = Reference(_map.AddAction("scrollWheel", InputActionType.PassThrough, "<Mouse>/scroll"));

            // Bound to our device and nothing else, so the real mouse cannot
            // also drive the OS while it drives the game.
            _asset.devices = new InputDevice[] { _device };
            _asset.Enable();
        }

        // Points the shared UI module at our private device, remembering what
        // it was reading before so it can be handed straight back.
        void Take()
        {
            if (_holding || !_wired || _module == null || _point == null) return;

            _savedPoint = _module.point;
            _savedLeft = _module.leftClick;
            _savedRight = _module.rightClick;
            _savedMiddle = _module.middleClick;
            _savedScroll = _module.scrollWheel;
            _saved = true;
            _holding = true;

            _module.point = _point;
            _module.leftClick = _left;
            _module.rightClick = _right;
            _module.middleClick = _middle;
            _module.scrollWheel = _scroll;
        }

        // Gives the module back whatever it was reading. Safe to call when it
        // was never taken.
        void Release()
        {
            if (!_holding) return;
            _holding = false;

            if (!_saved || _module == null) return;

            _module.point = _savedPoint;
            _module.leftClick = _savedLeft;
            _module.rightClick = _savedRight;
            _module.middleClick = _savedMiddle;
            _module.scrollWheel = _savedScroll;
            _saved = false;
        }

        // Takes down whatever got as far as being built, so a half-finished
        // Build can be cleaned up the same way a working one is.
        void Unwire()
        {
            Release();
            _wired = false;
            _saved = false;

            if (_asset)
            {
                _asset.Disable();
                Destroy(_asset);
            }
            _asset = null;
            _map = null;

            DestroyReference(ref _point);
            DestroyReference(ref _left);
            DestroyReference(ref _right);
            DestroyReference(ref _middle);
            DestroyReference(ref _scroll);

            if (_device != null && _device.added) InputSystem.RemoveDevice(_device);
            _device = null;
        }

        static Mouse FindDevice()
        {
            foreach (var device in InputSystem.devices)
                if (device is Mouse mouse && device.name == DeviceName)
                    return mouse;
            return null;
        }

        static InputActionReference Reference(InputAction action) => InputActionReference.Create(action);

        static void DestroyReference(ref InputActionReference reference)
        {
            if (reference) Destroy(reference);
            reference = null;
        }
    }
}
