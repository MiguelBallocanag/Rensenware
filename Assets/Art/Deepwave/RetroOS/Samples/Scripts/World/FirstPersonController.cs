using UnityEngine;
using UnityEngine.InputSystem;

namespace Deepwave.RetroOS.World
{
    // Minimal first-person walker: WASD + mouse-look via the new Input System (no action asset).
    // Set Active = false to freeze it while the OS screen is open.
    [RequireComponent(typeof(CharacterController))]
    [AddComponentMenu("")]
    public class FirstPersonController : MonoBehaviour
    {
        [Tooltip("Head transform the camera follows; pitch is applied here.")]
        [SerializeField] Transform eye;
        [SerializeField] float moveSpeed = 3.2f;
        [SerializeField] float lookSensitivity = 0.1f;
        [SerializeField] float gravity = 14f;
        [SerializeField] float minPitch = -80f, maxPitch = 80f;
        [SerializeField] bool lookEnabled = true;

        CharacterController _cc;
        float _pitch;
        float _fallSpeed;

        public Transform Eye => eye;
        public bool Active { get; set; } = true;

        void Awake() => _cc = GetComponent<CharacterController>();

        void Update()
        {
            if (!Active) return;
            if (lookEnabled) Look();
            Move();
        }

        void Look()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            var delta = mouse.delta.ReadValue() * lookSensitivity;
            transform.Rotate(0f, delta.x, 0f);
            _pitch = Mathf.Clamp(_pitch - delta.y, minPitch, maxPitch);
            if (eye) eye.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        void Move()
        {
            var keyboard = Keyboard.current;
            var input = Vector2.zero;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
            }

            var move = transform.forward * input.y + transform.right * input.x;
            move = Vector3.ClampMagnitude(move, 1f) * moveSpeed;

            if (_cc.isGrounded && _fallSpeed < 0f) _fallSpeed = -2f;
            _fallSpeed -= gravity * Time.deltaTime;
            move.y = _fallSpeed;

            _cc.Move(move * Time.deltaTime);
        }
    }
}
