using System;
using System.Collections;
using UnityEngine;

namespace Deepwave.RetroOS.Windowing
{
    /// <summary>The transitions a window plays.</summary>
    public enum WindowState
    {
        /// <summary>Opening.</summary>
        In,
        /// <summary>Closing. Deactivates the window when it ends.</summary>
        Out,
        /// <summary>Dropping to the taskbar. Deactivates the window when it ends.</summary>
        Minimize,
        /// <summary>Filling the work area.</summary>
        Fullscreen,
        /// <summary>Returning to its floating size.</summary>
        Normalize
    }

    /// <summary>Plays a window's open, close, minimize, maximize and restore transitions. Every window has one; it is the only thing that switches a window object on or off, so state and visuals cannot disagree.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("")]
    public class WindowTransition : MonoBehaviour
    {
        [Tooltip("Shape of every window transition over its duration.")]
        public AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Higher is faster. 10 lands about a tenth of a second.")]
        [Range(0.5f, 25f)] public float speed = 11f;

        [Tooltip("Scale a window opens from and closes to. 1 = no zoom.")]
        [Range(0.5f, 1f)] public float openScale = 0.92f;

        [Tooltip("Turn every transition off for the flat, instant 95 feel.")]
        public bool instant;

        /// <summary>Raised when a transition finishes, with the state it played.</summary>
        public event Action<WindowState> Finished;

        /// <summary>The transition currently playing, or <c>null</c> when idle.</summary>
        public WindowState? Playing { get; private set; }

        /// <summary>Where a minimize should fly to, in the target's own space.</summary>
        public Vector2 MinimizeTarget { get; set; }

        /// <summary>The rect that moves and scales - a window's chrome, not its host. Scaling the host would zoom the window about the middle of the desktop, so one in a corner would fly in from the centre. Defaults to this object's own rect.</summary>
        public RectTransform Target
        {
            get => _target ? _target : _target = (RectTransform)transform;
            set => _target = value;
        }

        CanvasGroup _group;
        RectTransform _target;
        Coroutine _running;
        Vector2 _restPosition;

        void Awake()
        {
            if (!_group) _group = GetComponent<CanvasGroup>();
        }

        /// <summary>Plays a transition. Starting a new one interrupts whatever was running, so a window mashed between minimize and restore never ends up stuck at half alpha.</summary>
        public void Play(WindowState state)
        {
            Awake();

            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;

                // An interrupted minimize has already flown the frame toward the taskbar; put it back before anything else plays.
                if (Playing == WindowState.Minimize) Target.anchoredPosition = _restPosition;
                Playing = null;
            }

            var opening = state != WindowState.Out && state != WindowState.Minimize;
            if (opening) gameObject.SetActive(true);

            // A closing window stops taking clicks at once, so the click that closed it lands behind it and Close cannot run twice.
            else if (_group) _group.blocksRaycasts = false;

            // Recorded before the instant path: landing a Minimize records where the window was, the only chance with transitions off.
            _restPosition = Target.anchoredPosition;

            if (instant || !isActiveAndEnabled)
            {
                Land(state);
                return;
            }

            Playing = state;
            _running = StartCoroutine(Run(state));
        }

        IEnumerator Run(WindowState state)
        {
            for (var t = 0f; t < 1f;)
            {
                t += Time.unscaledDeltaTime * speed;
                Sample(state, curve.Evaluate(Mathf.Clamp01(t)));
                yield return null;
            }

            _running = null;
            Land(state);
        }

        // t runs 0 -> 1 through the transition, whichever way it is going.
        void Sample(WindowState state, float t)
        {
            switch (state)
            {
                case WindowState.In:
                    _group.alpha = t;
                    Scale(Mathf.Lerp(openScale, 1f, t));
                    break;

                case WindowState.Out:
                    _group.alpha = 1f - t;
                    Scale(Mathf.Lerp(1f, openScale, t));
                    break;

                case WindowState.Minimize:
                    _group.alpha = 1f - t;
                    Scale(Mathf.Lerp(1f, 0.6f, t));
                    Target.anchoredPosition = Vector2.Lerp(_restPosition, MinimizeTarget, t);
                    break;

                // The frame itself is re-laid-out by the window; the transition
                // only sells the change with a short settle.
                case WindowState.Fullscreen:
                case WindowState.Normalize:
                    _group.alpha = Mathf.Lerp(0.55f, 1f, t);
                    Scale(Mathf.Lerp(0.98f, 1f, t));
                    break;
            }
        }

        // The finished pose, applied whether or not the transition animated.
        void Land(WindowState state)
        {
            Playing = null;
            Scale(1f);

            switch (state)
            {
                case WindowState.Out:
                case WindowState.Minimize:
                    _group.alpha = 1f;                    // ready for the next open
                    _group.blocksRaycasts = true;
                    if (state == WindowState.Minimize) Target.anchoredPosition = _restPosition;
                    gameObject.SetActive(false);
                    break;

                default:
                    _group.alpha = 1f;
                    _group.blocksRaycasts = true;
                    break;
            }

            Finished?.Invoke(state);
        }

        void Scale(float s) => Target.localScale = new Vector3(s, s, 1f);

        void OnDisable()
        {
            if (_running == null) return;
            StopCoroutine(_running);
            _running = null;
            Playing = null;
            if (_group) _group.alpha = 1f;
            if (_target) _target.localScale = Vector3.one;
        }
    }
}
