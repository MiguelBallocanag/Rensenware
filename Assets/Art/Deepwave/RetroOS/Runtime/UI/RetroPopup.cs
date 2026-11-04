using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Deepwave.RetroOS.UI
{
    /// <summary>How a <see cref="RetroPopup"/> grows into view.</summary>
    public enum PopupAnimation
    {
        /// <summary>Scales up from nothing about the pivot, fading in.</summary>
        Scale,
        /// <summary>Unrolls sideways to its resting width.</summary>
        Horizontal,
        /// <summary>Unrolls downward (or upward, by pivot) to its resting height.</summary>
        Vertical,
        /// <summary>Alpha only - no geometry change.</summary>
        Fade,
        /// <summary>Appears at once. The period-accurate choice.</summary>
        None
    }

    /// <summary>
    /// The transition every transient surface uses (dialogs, menus, fly-outs,
    /// notifications, tool-tips). Drives a <see cref="CanvasGroup"/> and the
    /// rect together, on unscaled time. Sizes are captured on first play-in.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("")]
    public class RetroPopup : MonoBehaviour
    {
        [Tooltip("How the popup grows into view.")]
        public PopupAnimation mode = PopupAnimation.Scale;

        [Tooltip("Shape of the transition over its duration.")]
        public AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Higher is faster. 9 is about a tenth of a second.")]
        [Range(0.5f, 20f)] public float speed = 9f;

        [Tooltip("Skip the outgoing transition and vanish at once.")]
        public bool instantOut;

        [Tooltip("Deactivate the object when the outgoing transition ends.")]
        public bool deactivateOnHide = true;

        /// <summary>Raised once the incoming transition has finished.</summary>
        public event Action Shown;

        /// <summary>Raised once the outgoing transition has finished.</summary>
        public event Action Hidden;

        /// <summary>Whether the popup is showing (or on its way in).</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Whether a transition is running right now. What a screenshot has to wait for: a page caught halfway out is still drawn over the one coming in.</summary>
        public bool Animating => _running != null;

        CanvasGroup _group;
        RectTransform _rect;
        Vector2 _restSize;
        bool _measured;
        Coroutine _running;

        void Awake()
        {
            if (!_group) _group = GetComponent<CanvasGroup>();
            if (!_rect) _rect = (RectTransform)transform;
        }

        /// <summary>Adds a popup to an object and plays it in. The one call app code and factories use.</summary>
        public static RetroPopup Play(GameObject target, PopupAnimation mode = PopupAnimation.Scale,
            float speed = 9f)
        {
            if (!target) return null;
            var popup = target.GetComponent<RetroPopup>();
            if (!popup) popup = target.AddComponent<RetroPopup>();
            popup.mode = mode;
            popup.speed = speed;
            popup.PlayIn();
            return popup;
        }

        /// <summary>Toggles between the incoming and outgoing transitions.</summary>
        public void Toggle()
        {
            if (IsOpen) PlayOut();
            else PlayIn();
        }

        /// <summary>Brings the popup in. Safe to call while it is going out.</summary>
        public void PlayIn()
        {
            IsOpen = true;
            gameObject.SetActive(true);
            Awake();

            if (mode == PopupAnimation.None || !isActiveAndEnabled)
            {
                Settle();
                return;
            }

            // Resting size = the layout's size before the first animation.
            Measure();
            Restart(Grow());
        }

        /// <summary>Takes the popup out. It stops blocking clicks at once, so a menu closing under the cursor never eats the next click.</summary>
        public void PlayOut()
        {
            IsOpen = false;
            Awake();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            if (instantOut || mode == PopupAnimation.None || !isActiveAndEnabled)
            {
                Finish();
                return;
            }

            Measure();
            Restart(Shrink());
        }

        /// <summary>Plays out, then destroys. How a dialog dismisses itself.</summary>
        public void Dismiss()
        {
            Awake();

            if (instantOut || mode == PopupAnimation.None || !isActiveAndEnabled)
            {
                Destroy(gameObject);
                return;
            }

            deactivateOnHide = false;
            Hidden += DestroySelf;
            PlayOut();
        }

        void DestroySelf()
        {
            Hidden -= DestroySelf;
            if (this) Destroy(gameObject);
        }

        /// <summary>Re-reads the resting size. Call after changing a popup's contents.</summary>
        public void Remeasure() => _measured = false;

        void Measure()
        {
            if (_measured) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rect);
            _restSize = _rect.rect.size;
            _measured = true;
        }

        void Restart(IEnumerator routine)
        {
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(routine);
        }

        // Fully visible, at rest.
        void Settle()
        {
            if (_running != null) { StopCoroutine(_running); _running = null; }
            _group.alpha = 1f;
            _group.blocksRaycasts = true;
            _group.interactable = true;
            _rect.localScale = Vector3.one;
            Shown?.Invoke();
        }

        // Fully hidden. Scale stays at one so a transition-less re-show is not stuck at zero.
        void Finish()
        {
            if (_running != null) { StopCoroutine(_running); _running = null; }
            _group.alpha = 0f;
            _rect.localScale = Vector3.one;
            if (deactivateOnHide) gameObject.SetActive(false);
            Hidden?.Invoke();
        }

        IEnumerator Grow()
        {
            _group.blocksRaycasts = true;
            _group.interactable = true;

            for (var t = 0f; t < 1f;)
            {
                t += Time.unscaledDeltaTime * speed;
                Apply(curve.Evaluate(Mathf.Clamp01(t)));
                yield return null;
            }

            _running = null;
            Apply(1f);
            Shown?.Invoke();
        }

        IEnumerator Shrink()
        {
            for (var t = 0f; t < 1f;)
            {
                t += Time.unscaledDeltaTime * speed;
                Apply(1f - curve.Evaluate(Mathf.Clamp01(t)));
                yield return null;
            }

            _running = null;
            Finish();
        }

        // 0 = gone, 1 = at rest.
        void Apply(float k)
        {
            _group.alpha = k;

            switch (mode)
            {
                case PopupAnimation.Scale:
                    // Never quite zero: a zero-area rect cannot lay out its children.
                    var s = Mathf.Lerp(0.85f, 1f, k);
                    _rect.localScale = new Vector3(s, s, 1f);
                    break;
                case PopupAnimation.Horizontal:
                    _rect.sizeDelta = new Vector2(_restSize.x * k, _restSize.y);
                    break;
                case PopupAnimation.Vertical:
                    _rect.sizeDelta = new Vector2(_restSize.x, _restSize.y * k);
                    break;
            }
        }

        void OnDisable()
        {
            // A popup switched off mid-transition must not come back half-drawn.
            if (_running == null) return;
            StopCoroutine(_running);
            _running = null;
            if (_group) _group.alpha = IsOpen ? 1f : 0f;
            if (_rect) _rect.localScale = Vector3.one;
        }
    }
}
