using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Deepwave.RetroOS.Display
{
    /// <summary>The one thing that puts the OS on a screen.</summary>
    [AddComponentMenu("Deepwave/RetroOS/Screen Host")]
    [DisallowMultipleComponent]
    public class RetroScreenHost : MonoBehaviour
    {
        /// <summary>The canvas the whole OS is built into.</summary>
        public Canvas Canvas { get; private set; }

        /// <summary>Gates clicks without hiding the picture.</summary>
        public CanvasGroup Group { get; private set; }

        /// <summary>The camera that draws the OS into <see cref="Texture"/>.</summary>
        public Camera Projector { get; private set; }

        /// <summary>The OS picture. Never null once the host is built.</summary>
        public RenderTexture Texture { get; private set; }

        /// <summary>The rect the OS fills - the whole canvas.</summary>
        public RectTransform Root { get; private set; }

        /// <summary>Whether the player is at the machine and can click it.</summary>
        public bool Focused { get; private set; }

        /// <summary>Whether the screen is lit. False once the machine has switched it off - a dead monitor, nothing on the glass.</summary>
        public bool ScreenVisible => _visible;

        /// <summary>Keep the OS on its <see cref="Texture"/> even when focused, instead of flipping to a screen-space overlay. Focus then only decides whether it takes clicks and whether the projector runs every frame. Set by the bootstrap for the on-screen-cursor interaction mode.</summary>
        public bool LockToTexture
        {
            get => _lockToTexture;
            set
            {
                if (_lockToTexture == value) return;
                _lockToTexture = value;
                ApplyScale();
            }
        }

        bool _lockToTexture;

        /// <summary>The projector's target size, in pixels.</summary>
        public Vector2Int TextureSize => _textureSize;

        /// <summary>Snap the canvas scale to a whole number, so a 32-pixel icon lands on 32, 64 or 96 device pixels and a one-pixel bevel stays one pixel.</summary>
        public bool PixelPerfect
        {
            get => _pixelPerfect;
            set
            {
                if (_pixelPerfect == value) return;
                _pixelPerfect = value;
                ApplyScale();
            }
        }

        // Far enough apart that no projector frustum reaches the next machine's canvas.
        // Not larger: the raycaster works in world space and the camera in clip space, and
        // at ~10,000 units they disagreed by ~4 interface pixels (a 9px toggle became unhittable).
        // If this or ParkDepth grows again, that offset comes back.
        const float Bay = 250f;

        // How far below the scene the rig is parked - the coordinate that dominates the precision loss (it applies to every machine). See Bay.
        const float ParkDepth = 1000f;

        // Which bay each live screen holds. A list, not a count, so a torn-down machine gives its bay back.
        static readonly System.Collections.Generic.List<RetroScreenHost> Bays = new();

        // Statics outlive a play session when domain reload is off, and every
        // host in that list belongs to the run that has already ended.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Bays.Clear();

        static int TakeBay(RetroScreenHost host)
        {
            for (var i = 0; i < Bays.Count; i++)
                if (!Bays[i])
                {
                    Bays[i] = host;
                    return i;
                }

            Bays.Add(host);
            return Bays.Count - 1;
        }

        int _bay;

        CanvasScaler _scaler;
        Vector2 _reference;
        bool _pixelPerfect = true;
        Vector2Int _textureSize;
        bool _dynamic;
        bool _externalTexture;
        bool _forceEveryFrame;
        int _sortingOrder;

        /// <summary>Builds the canvas, the projector and the picture it draws into.</summary>
        /// <param name="owner">Object the canvas is parented to.</param>
        /// <param name="resolution">Logical UI resolution the OS lays out at.</param>
        /// <param name="dynamicResolution">
        /// Render at the display's own size rather than the logical one, so
        /// the OS is as sharp as the monitor allows.
        /// </param>
        /// <param name="sortingOrder">Canvas sorting order once focused.</param>
        public static RetroScreenHost Create(Transform owner, Vector2 resolution,
            bool dynamicResolution = true, int sortingOrder = 100)
        {
            var go = new GameObject(owner ? $"RetroOS Screen ({owner.name})" : "RetroOS Screen",
                typeof(RetroScreenHost));

            // Each machine's screen gets its own patch of nowhere: a screen-space-camera canvas is real geometry, and the projector sees a whole layer.
            var host = go.GetComponent<RetroScreenHost>();
            host._bay = TakeBay(host);
            go.transform.position = new Vector3(Bay * host._bay, 0f, 0f);
            host._dynamic = dynamicResolution;
            host._sortingOrder = sortingOrder;
            host.Build(resolution);
            return host;
        }

        void Build(Vector2 resolution)
        {
            var canvasGo = new GameObject("Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.layer = gameObject.layer;

            Canvas = canvasGo.GetComponent<Canvas>();
            Canvas.sortingOrder = _sortingOrder;
            Group = canvasGo.GetComponent<CanvasGroup>();

            _scaler = canvasGo.GetComponent<CanvasScaler>();
            _reference = resolution;
            _scaler.referenceResolution = resolution;
            _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            _scaler.matchWidthOrHeight = 0.5f;

            Root = (RectTransform)canvasGo.transform;

            BuildProjector(resolution);
            ApplyScale();
            SetFocused(false);
        }

        // A camera that sees the OS and nothing else, parked below the scene
        // so the game's own cameras can never wander into the picture.
        void BuildProjector(Vector2 resolution)
        {
            var camGo = new GameObject("Projector", typeof(Camera));
            camGo.transform.SetParent(transform, false);
            camGo.transform.localPosition = new Vector3(0f, -ParkDepth, 0f);
            camGo.layer = gameObject.layer;

            Projector = camGo.GetComponent<Camera>();
            // Perspective: Unity sizes a screen-space-camera canvas to the camera's view at planeDistance, for a frustum.
            Projector.orthographic = false;
            Projector.fieldOfView = 60f;
            Projector.clearFlags = CameraClearFlags.SolidColor;
            Projector.backgroundColor = Color.black;
            Projector.cullingMask = 1 << gameObject.layer;
            Projector.nearClipPlane = 0.1f;
            Projector.farClipPlane = 100f;
            Projector.allowHDR = false;
            Projector.allowMSAA = false;
            Projector.depth = -50f;

            // On only for the frames the picture is due - 15fps is enough for a still desktop across a room.
            // Toggling the camera, not Camera.Render(): a manual render is unsupported under an SRP.
            Projector.enabled = false;

            Canvas.renderMode = RenderMode.ScreenSpaceCamera;
            Canvas.worldCamera = Projector;
            Canvas.planeDistance = 10f;

            if (!_externalTexture) EnsureTexture(WantedSize(resolution));
        }

        Vector2Int WantedSize(Vector2 resolution)
        {
            if (!_dynamic) return new Vector2Int((int)resolution.x, (int)resolution.y);
            return new Vector2Int(Mathf.Max(64, Screen.width), Mathf.Max(64, Screen.height));
        }

        /// <summary>Draw into a RenderTexture the game owns - a fixed asset wired into a material - rather than one sized to the display. Turns dynamic resolution off; the host never releases a texture it did not make.</summary>
        public void UseExternalTexture(RenderTexture texture)
        {
            if (!texture || texture == Texture) return;

            // Let go of the display-sized texture the projector was set up with.
            var owned = !_externalTexture ? Texture : null;

            _externalTexture = true;
            _dynamic = false;
            Texture = texture;
            if (!Texture.IsCreated()) Texture.Create();
            _textureSize = new Vector2Int(Texture.width, Texture.height);
            ApplyScale();

            if (Projector) Projector.targetTexture = Texture;

            if (owned)
            {
                owned.Release();
                Destroy(owned);
            }
        }

        // The canvas measures against what it draws into (render texture when projected, display when overlay); both change, so the factor is recomputed.
        void ApplyScale()
        {
            if (!_scaler) return;

            if (!_pixelPerfect)
            {
                _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                return;
            }

            // Before the first texture exists, measure against the requested resolution - a factor of one.
            var into = Focused && !LockToTexture
                ? new Vector2(Screen.width, Screen.height)
                : _textureSize.x > 0 && _textureSize.y > 0
                    ? new Vector2(_textureSize.x, _textureSize.y)
                    : _reference;

            var fit = Mathf.Min(into.x / Mathf.Max(1f, _reference.x),
                into.y / Mathf.Max(1f, _reference.y));

            // Below the logical resolution there is no whole factor to snap to; a too-small screen must shrink the desktop or lose the taskbar.
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            _scaler.scaleFactor = fit >= 1f ? Mathf.Floor(fit) : Mathf.Max(0.1f, fit);
        }

        void EnsureTexture(Vector2Int size)
        {
            if (_externalTexture) return;
            if (Texture && _textureSize == size) return;

            var old = Texture;
            _textureSize = size;
            Texture = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32)
            {
                name = "RetroOS Screen",
                useMipMap = false,
                autoGenerateMips = false,
                filterMode = FilterMode.Bilinear
            };
            Texture.Create();

            Projector.targetTexture = Texture;
            ApplyScale();

            if (old) old.Release();
            if (old) Destroy(old);
        }

        /// <summary>Sits the player down at the machine, or gets them up again. Focused means the OS owns the display and takes clicks; unfocused means it is a picture on a screen somewhere in the world.</summary>
        public void SetFocused(bool focused)
        {
            Focused = focused;

            if (LockToTexture)
            {
                // The OS stays a picture on its surface either way; focus only decides clicks and whether the projector runs flat out.
                if (Canvas.renderMode != RenderMode.ScreenSpaceCamera)
                {
                    Canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    Canvas.worldCamera = Projector;
                    Canvas.planeDistance = 10f;
                }

                _forceEveryFrame = focused;
                _nextFrame = 0f;
                if (!focused && EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
                if (Group) Group.blocksRaycasts = focused;
                ApplyScale();
                return;
            }

            if (focused)
            {
                Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                Canvas.sortingOrder = _sortingOrder;
                if (Projector) Projector.enabled = false;
            }
            else
            {
                Canvas.renderMode = RenderMode.ScreenSpaceCamera;
                Canvas.worldCamera = Projector;
                Canvas.planeDistance = 10f;

                // Draw one frame straight away rather than leaving whatever
                // was on the glass when the player stood up.
                _nextFrame = 0f;
                if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            }

            // Raycasts only: disabling CanvasGroup.interactable would grey every Selectable on a monitor nobody is at.
            if (Group) Group.blocksRaycasts = focused;

            // An overlay is measured against the display and a projected canvas
            // against its texture, so the whole factor is not the same one.
            ApplyScale();
        }

        /// <summary>Switches the whole screen off, picture and all.</summary>
        public void SetVisible(bool visible)
        {
            if (Canvas) Canvas.gameObject.SetActive(visible);
            _visible = visible;

            if (visible)
            {
                // Straight away, rather than up to a fifteenth of a second
                // after the machine comes back on.
                RequestFrame();
                return;
            }

            // A dark monitor, not the last frame frozen: switch the projector off, or it keeps drawing an empty room 15/s.
            if (Projector) Projector.enabled = false;

            if (!Texture) return;

            var previous = RenderTexture.active;
            RenderTexture.active = Texture;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = previous;
        }

        /// <summary>How many times a second the picture is redrawn while the player is not at the machine. Fifteen is indistinguishable from sixty on a desktop that is mostly sitting still, and costs a quarter as much. Zero redraws every frame.</summary>
        public float UnfocusedFps { get; set; } = 15f;

        float _nextFrame;
        bool _visible = true;

        void Update()
        {
            // Overlay + focused: the canvas draws itself. Locked to the texture, the projector runs whether focused or not.
            if (!LockToTexture && Focused) return;
            if (!_visible) return;

            // The picture follows the display: a resized game view, a monitor
            // plugged in, going full screen.
            if (_dynamic)
            {
                var wanted = new Vector2Int(Mathf.Max(64, Screen.width), Mathf.Max(64, Screen.height));
                if (wanted != _textureSize) EnsureTexture(wanted);
            }

            if (!Projector || !Texture) return;

            if (UnfocusedFps <= 0f || _forceEveryFrame) { Projector.enabled = true; return; }

            // This frame is due, or it is not. The camera renders at the end
            // of whichever frames it is left enabled for.
            var now = Time.unscaledTime;
            var due = now >= _nextFrame;
            Projector.enabled = due;
            if (due) _nextFrame = now + 1f / UnfocusedFps;
        }

        /// <summary>Draws the picture on the next frame, whatever the timer says. For something that has to be seen at once - a machine crashing while the player watches from the doorway.</summary>
        public void RequestFrame() => _nextFrame = 0f;

        /// <summary>Tears the screen down. The machine calls this when it goes.</summary>
        public void Dispose()
        {
            if (this) Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (_bay >= 0 && _bay < Bays.Count && Bays[_bay] == this) Bays[_bay] = null;

            if (!Texture) return;
            if (Projector) Projector.targetTexture = null;

            // A texture the game handed us (a wired-in asset) is not ours to free.
            if (_externalTexture) return;
            Texture.Release();
            Destroy(Texture);
        }
    }
}
