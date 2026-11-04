using System.Collections;
using System.Collections.Generic;
using Deepwave.RetroOS.Display;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Deepwave.RetroOS.World
{
    // A desk with a computer. Press the interact key: the camera glides to the screen and
    // RetroOS takes over; the exit key pulls back to first-person. Put this on the desk with
    // the screen; the computer can sit anywhere. It owns the camera during the session, so
    // the view camera must NOT be permanently parented under the player.
    [AddComponentMenu("")]
    public class RetroWorkstation : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The computer at this desk. Empty falls back to looking for " +
                 "one parented under the desk - either the machine itself or " +
                 "the screen it draws on.")]
        [SerializeField] RetroOsBootstrap computer;
        [SerializeField] FirstPersonController player;
        [SerializeField] Camera viewCamera;
        [Tooltip("Pose the camera glides to - place it facing the screen.")]
        [SerializeField] Transform screenFocus;

        [Header("Interaction")]
        [SerializeField] float interactRange = 2.4f;
        [Tooltip("How directly the player must face the monitor (1 = dead on).")]
        [SerializeField] float facingDot = 0.35f;
        [SerializeField] float transitionTime = 0.9f;

        [Header("Keys")]
        [SerializeField] Key interactKey = Key.E;
        [SerializeField] Key exitKey = Key.Space;

        [Header("Prompt")]
        [Tooltip("Font for the walk-up hint. Empty uses Unity's built-in one.")]
        [SerializeField] Font promptFont;

        [Tooltip("How long the way back out is shown after sitting down. " +
                 "0 leaves it up for as long as the player is at the machine.")]
        [SerializeField] float exitHintSeconds = 4f;

        enum Mode { World, Entering, Using, Exiting }
        Mode _mode = Mode.World;

        // One desk at a time: overlapping interact ranges would both answer one key press,
        // gliding the shared camera between two screens and focusing both machines.
        static RetroWorkstation s_inUse;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_inUse = null;

        RetroOsBootstrap _os;

        /// <summary>The computer at this desk.</summary>
        RetroOsBootstrap Os
        {
            get
            {
                if (_os)
                    return _os;

                if (computer)
                    return _os = computer;

                var screen = GetComponentInChildren<RetroMonitor>(true);
                if (screen && screen.Computer)
                    return _os = screen.Computer;

                return _os = GetComponentInChildren<RetroOsBootstrap>(true);
            }
        }

        Text _prompt;
        Transform _eye;
        Canvas _promptCanvas;
        RectTransform _plate;
        GameObject _promptRoot;
        Coroutine _exitHint;

        bool _warnedUnwired;

        // Everything this desk needs before it can seat anybody, reported the first time one is missing.
        // A half-wired desk used to look like a working one the player stood too far from: the prompt
        // appeared, the camera glided, and every session call was skipped by a silent `if (Os)`.
        bool Wired()
        {
            var missing = new List<string>();
            if (!player) missing.Add("Player");
            if (!viewCamera) missing.Add("View Camera");
            if (!screenFocus) missing.Add("Screen Focus");
            if (!Os) missing.Add("Computer");

            if (missing.Count == 0)
                return true;

            if (!_warnedUnwired)
            {
                _warnedUnwired = true;
                Debug.LogError(
                    $"RetroOS: the desk '{name}' has no " + string.Join(", ", missing) +
                    " on its Retro Workstation, so walking up to it does nothing. Computer takes " +
                    "the machine's RetroOsBootstrap - leave it empty only when the machine, or " +
                    "the screen it draws on, is parented under this desk.", this);
            }
            return false;
        }

        void Start()
        {
            _eye = player ? player.Eye : null;
            BuildPrompt();
            SetCursor(false);
            ShowPrompt(false);
        }

        void Update()
        {
            switch (_mode)
            {
                case Mode.World:
                    var can = CanInteract();
                    if (can)
                    {
                        SetHint(WalkUpText(), atTop: false);
                        PromptOverOs(false);
                    }

                    ShowPrompt(can);
                    if (can && Pressed(interactKey)) Interact();
                    break;

                case Mode.Using:
                    // Exit on the exit key, but not while typing into the OS.
                    if ((Pressed(exitKey) && !TypingInOs()) || Pressed(Key.Escape))
                        StartCoroutine(Exit());
                    break;
            }
        }

        /// <summary>A machine sitting there switched off. The OS is built either way - what is missing is the power, so the interact key is a switch before it is a chair.</summary>
        bool MachineOff => Os && !Os.PoweredOn;

        string WalkUpText() => MachineOff
            ? $"Press [{interactKey}] to turn on computer"
            : $"Press [{interactKey}] to use computer";

        void Interact()
        {
            // Turning it on leaves the player watching it boot; the prompt reads as the chair from the next frame.
            if (MachineOff)
            {
                Os.PowerOn();
                return;
            }

            // Two desks that both cleared CanInteract on the same frame race
            // to here; the first to claim the seat wins and the other backs off.
            if (s_inUse && s_inUse != this)
                return;

            StartCoroutine(Enter());
        }

        bool CanInteract()
        {
            if (_mode != Mode.World || !Wired()) return false;

            // Another desk has the player: stay out of reach so a key press cannot start a second session.
            if (s_inUse && s_inUse != this) return false;

            if (Vector3.Distance(player.transform.position, transform.position) > interactRange) return false;

            // Compare horizontal headings so looking down at a low desk monitor
            // still counts as facing it.
            var toStation = transform.position - viewCamera.transform.position;
            toStation.y = 0f;
            var forward = viewCamera.transform.forward;
            forward.y = 0f;
            if (toStation.sqrMagnitude < 1e-4f || forward.sqrMagnitude < 1e-4f) return true;
            return Vector3.Dot(forward.normalized, toStation.normalized) > facingDot;
        }

        IEnumerator Enter()
        {
            // Claimed before the first yield, so the other desk's Update this
            // same frame already sees the seat taken.
            s_inUse = this;
            _mode = Mode.Entering;
            ShowPrompt(false);
            if (player) player.Active = false;

            var cam = viewCamera.transform;
            cam.SetParent(null, true);
            yield return Glide(cam, screenFocus.position, screenFocus.rotation);

            if (Os) Os.SetFocused(true);
            SetCursor(true);
            _mode = Mode.Using;
            ShowExitHint();
        }

        IEnumerator Exit()
        {
            _mode = Mode.Exiting;
            ShowPrompt(false);
            PromptOverOs(false);
            if (Os) Os.SetFocused(false);
            SetCursor(false);

            var cam = viewCamera.transform;
            var target = _eye ? _eye : cam;
            yield return Glide(cam, target.position, target.rotation);

            if (_eye)
            {
                cam.SetParent(_eye, true);
                cam.localPosition = Vector3.zero;
                cam.localRotation = Quaternion.identity;
            }
            if (player) player.Active = true;
            _mode = Mode.World;

            // Seat's free. Held until here on purpose, so a jab at the key
            // mid-glide cannot drop the player straight into another desk.
            if (s_inUse == this) s_inUse = null;
        }

        void OnDisable()
        {
            // Torn down mid-session: do not leave every other desk locked out behind a workstation that is gone.
            if (s_inUse == this) s_inUse = null;
        }

        // Smooth ease-in-out move of the camera to a pose.
        IEnumerator Glide(Transform cam, Vector3 pos, Quaternion rot)
        {
            var fromPos = cam.position;
            var fromRot = cam.rotation;
            for (var t = 0f; t < transitionTime; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / transitionTime);
                cam.SetPositionAndRotation(Vector3.Lerp(fromPos, pos, k), Quaternion.Slerp(fromRot, rot, k));
                yield return null;
            }
            cam.SetPositionAndRotation(pos, rot);
        }

        static bool Pressed(Key key)
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard[key].wasPressedThisFrame;
        }

        // The OS ships this test, so the sample does not keep its own copy.
        static bool TypingInOs() => Deepwave.RetroOS.UI.RetroUi.IsTyping();

        void SetCursor(bool visible)
        {
            // In on-screen-cursor mode the OS owns the hardware cursor while focused - leave it alone;
            // back in the world the first-person controller wants it locked again.
            if (Os && Os.UsesOnScreenCursor && Os.Focused) return;

            Cursor.visible = visible;
            Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
        }

        // A small screen-space hint, plain uGUI on purpose: it is the game telling the player which
        // key to press, not part of the computer's interface, so it is built without the RetroUi kit
        // (which would take its font and colour from whichever machine claimed the palette last).
        void BuildPrompt()
        {
            var go = new GameObject("Workstation Prompt", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            _promptCanvas = go.GetComponent<Canvas>();
            _promptCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _promptCanvas.sortingOrder = UnderTheOs;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1024, 768);

            var plate = new GameObject("Plate", typeof(RectTransform), typeof(Image));
            plate.transform.SetParent(go.transform, false);
            var backing = plate.GetComponent<Image>();
            backing.color = new Color(0f, 0f, 0f, 0.55f);
            // The exit hint is drawn over the desktop, and a hint that ate the
            // click meant for the taskbar under it would be worse than none.
            backing.raycastTarget = false;
            _plate = (RectTransform)plate.transform;
            _plate.sizeDelta = new Vector2(320, 34);

            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(plate.transform, false);
            _prompt = label.GetComponent<Text>();
            _prompt.font = promptFont ? promptFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _prompt.fontSize = 14;
            _prompt.color = Color.white;
            _prompt.alignment = TextAnchor.MiddleCenter;
            // Never clipped: the key name is whatever the game bound, and a
            // long one wrapping to a second line should still be readable.
            _prompt.verticalOverflow = VerticalWrapMode.Overflow;
            _prompt.supportRichText = false;
            _prompt.raycastTarget = false;
            var lrt = _prompt.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            _promptRoot = plate;

            // Placed, not worded: the wording depends on whether the machine is on, and Update writes it as it shows it.
            PlaceHint(atTop: false);
        }

        // The walk-up hint sits behind the machine's picture; the way-back-out sits in front, since the OS covers the display.
        const int UnderTheOs = 10;

        void ShowPrompt(bool show)
        {
            if (_promptRoot && _promptRoot.activeSelf != show) _promptRoot.SetActive(show);
        }

        // Low on screen in the room, high at the machine - low there it would sit over the taskbar.
        void SetHint(string text, bool atTop)
        {
            if (!_prompt) return;

            if (_prompt.text != text) _prompt.text = text;
            PlaceHint(atTop);
        }

        void PlaceHint(bool atTop)
        {
            if (!_plate) return;

            var anchor = new Vector2(0.5f, atTop ? 1f : 0f);
            if (_plate.anchorMin == anchor) return;

            _plate.anchorMin = _plate.anchorMax = _plate.pivot = anchor;
            _plate.anchoredPosition = new Vector2(0f, atTop ? -60f : 90f);
        }

        // Ask the OS for its sorting order rather than guessing a number a game could get behind.
        void PromptOverOs(bool over)
        {
            if (!_promptCanvas) return;

            var screen = Os ? Os.ScreenHost : null;
            var order = over && screen && screen.Canvas ? screen.Canvas.sortingOrder + 1 : UnderTheOs;
            if (_promptCanvas.sortingOrder != order) _promptCanvas.sortingOrder = order;
        }

        // The way back out, shown on sit-down and dismissed on its own - worth reading once.
        void ShowExitHint()
        {
            SetHint($"Press [{exitKey}] to stand up", atTop: true);
            PromptOverOs(true);
            ShowPrompt(true);

            if (_exitHint != null) StopCoroutine(_exitHint);
            _exitHint = exitHintSeconds > 0f ? StartCoroutine(HideExitHint()) : null;
        }

        IEnumerator HideExitHint()
        {
            yield return new WaitForSeconds(exitHintSeconds);
            if (_mode == Mode.Using) ShowPrompt(false);
            _exitHint = null;
        }
    }
}
