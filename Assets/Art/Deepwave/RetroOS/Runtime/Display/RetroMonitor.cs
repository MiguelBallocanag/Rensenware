using UnityEngine;

namespace Deepwave.RetroOS.Display
{
    // How the player points at the OS on this screen.
    public enum ScreenInteraction
    {
        // Sitting down flips the OS to a pixel-exact screen-space overlay.
        FullscreenOverlay,

        // The OS never leaves its texture; a drawn cursor, aimed at the glass,
        // carries every click, drag, scroll and keystroke.
        OnScreenCursor,
    }

    /// <summary>The screen a computer is plugged into: how big its picture is and how the player points at it.</summary>
    [AddComponentMenu("Deepwave/RetroOS/Monitor")]
    [DisallowMultipleComponent]
    public class RetroMonitor : MonoBehaviour
    {
        [Tooltip(
            "Logical UI resolution the OS lays out at. 1024x768 is the "
                + "period-accurate 4:3 desktop."
        )]
        [SerializeField]
        Vector2 resolution = new(1024, 768);

        [Tooltip(
            "Render at the display's own size instead of the logical one, so "
                + "the picture is as sharp as the monitor allows. Turn off for "
                + "the chunky, honestly-low-resolution look."
        )]
        [SerializeField]
        bool dynamicResolution = true;

        [Tooltip(
            "Scale the interface by whole numbers only, so a 32-pixel icon "
                + "lands on 32, 64 or 96 pixels and a one-pixel border stays "
                + "one pixel. A larger display then shows more desktop rather "
                + "than a bigger one, the way a real machine did. Turn off to "
                + "have the interface stretch to fill the display exactly, "
                + "smoothly and slightly softly."
        )]
        [SerializeField]
        bool pixelPerfect = true;

        [Tooltip(
            "How often the picture is redrawn while nobody is at the machine. "
                + "15 is indistinguishable from 60 on a desktop that is mostly "
                + "sitting still. 0 redraws every frame."
        )]
        [Range(0f, 60f)]
        [SerializeField]
        float unfocusedFps = 15f;

        [Tooltip(
            "The picture, as a RenderTexture asset you own. Wire the same "
                + "asset into your screen material or RawImage to show the OS - "
                + "in edit mode too. Empty renders into a texture sized to the "
                + "display, which only a fullscreen machine can see; Dynamic "
                + "Resolution is ignored when this is set."
        )]
        [SerializeField]
        RenderTexture textureAsset;

        [Tooltip(
            "Region of the picture the OS fills, as fractions of it "
                + "(x,y = bottom-left corner, w,h = size). The whole picture = "
                + "0,0,1,1. Everything - desktop, windows, taskbar, dialogs - "
                + "lives inside it, so a bezel that eats the edges of the glass "
                + "is something the desktop can be held clear of."
        )]
        [SerializeField]
        Rect workArea = new(0f, 0f, 1f, 1f);

        [Tooltip(
            "Fullscreen Overlay: sitting down flips the OS to a pixel-exact "
                + "screen-space overlay. On Screen Cursor: the OS stays on its "
                + "picture and a drawn pointer, walked by the mouse, a gamepad "
                + "stick or a dragging finger, carries every click."
        )]
        [SerializeField]
        ScreenInteraction interaction = ScreenInteraction.FullscreenOverlay;

        [Tooltip(
            "On Screen Cursor only. How far the drawn pointer travels for a "
                + "given hand movement. 1 means a sweep across the display is a "
                + "sweep across the whole desktop."
        )]
        [Range(0.1f, 4f)]
        [SerializeField]
        float cursorSpeed = 1f;

        [Tooltip(
            "On Screen Cursor only. How fast the drawn pointer moves under a "
                + "fully pushed gamepad stick, in desktop pixels a second."
        )]
        [Range(200f, 6000f)]
        [SerializeField]
        float stickSpeed = 1600f;

        [Tooltip(
            "Where the OS sits among the game's own canvases while the player "
                + "is at the machine. Raise it to draw over a HUD, lower it to "
                + "let one draw over the OS."
        )]
        [SerializeField]
        int sortingOrder = 100;

        /// <summary>The logical resolution the OS lays out at.</summary>
        public Vector2 Resolution =>
            resolution.x < 64f || resolution.y < 64f ? new Vector2(1024f, 768f) : resolution;

        public bool DynamicResolution => dynamicResolution;
        public bool PixelPerfect => pixelPerfect;
        public float UnfocusedFps => unfocusedFps;
        public RenderTexture TextureAsset => textureAsset;
        public int SortingOrder => sortingOrder;

        /// <summary>The part of the picture the OS fills, in fractions of it. A region with no width or height is a screen nothing could be read on, so it reads as the whole picture instead.</summary>
        public Rect WorkArea
        {
            get
            {
                var x = Mathf.Clamp01(workArea.xMin);
                var y = Mathf.Clamp01(workArea.yMin);
                var w = Mathf.Clamp01(workArea.xMax) - x;
                var h = Mathf.Clamp01(workArea.yMax) - y;
                return w <= 0f || h <= 0f ? new Rect(0f, 0f, 1f, 1f) : new Rect(x, y, w, h);
            }
        }

        public ScreenInteraction Interaction => interaction;

        /// <summary>On-screen cursor: screen-pixels of hand to picture-pixels of pointer.</summary>
        public float CursorSpeed => cursorSpeed;

        /// <summary>On-screen cursor: picture-pixels a second at full gamepad-stick deflection.</summary>
        public float StickSpeed => stickSpeed;

        /// <summary>Whether this screen is driven by a cursor drawn on itself rather than by flipping the OS to a fullscreen overlay.</summary>
        public bool UsesOnScreenCursor => interaction == ScreenInteraction.OnScreenCursor;

        /// <summary>The computer drawing on this screen, once one has taken it. Null until a machine boots, and null for a screen nothing is plugged into.</summary>
        public RetroOsBootstrap Computer { get; private set; }

        /// <summary>Taken by the computer that draws on this screen.</summary>
        internal void PlugIn(RetroOsBootstrap computer) => Computer = computer;

        /// <summary>Given up when that computer goes.</summary>
        internal void Unplug(RetroOsBootstrap computer)
        {
            if (Computer == computer)
                Computer = null;
        }
    }
}
