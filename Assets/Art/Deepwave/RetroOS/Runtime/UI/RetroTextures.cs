using System;
using System.Collections.Generic;
using UnityEngine;
using Deepwave.RetroOS.UI.Icons;

namespace Deepwave.RetroOS.UI
{
    /// <summary>Small painted chrome that is not an icon: the scroll trough dither and its arrow glyphs. Follows the theme; cached, dropped on theme change.</summary>
    public static class RetroTextures
    {
        static Sprite s_checker;
        static Sprite s_tick;
        static readonly Dictionary<int, Sprite> s_arrows = new();

        // The palette each cached sprite was painted from. Checked on read, not
        // cleared on RetroSkin.Changed - this class subscribes after the widgets do.
        static Color s_checkerFace, s_checkerLight, s_inkTick;
        static Color s_inkArrows;

        // uGUI's default; a tiled sprite must be built at this to tile 1:1.
        const float ReferencePixelsPerUnit = 100f;

        static RetroTextures() => RetroSkin.Changed += Invalidate;

        /// <summary>The 50% checkerboard filling a scroll trough. Tiled, so it stays two device pixels at any height.</summary>
        public static Sprite Checker
        {
            get
            {
                var face = RetroSkin.Face;
                var light = RetroSkin.FaceLight;
                if (s_checker && s_checkerFace == face && s_checkerLight == light) return s_checker;
                s_checkerFace = face;
                s_checkerLight = light;

                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = "RetroTrough",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Repeat,
                    hideFlags = HideFlags.HideAndDontSave
                };
                var pale = (Color32)light;
                var dark = (Color32)face;
                tex.SetPixels32(new[] { pale, dark, dark, pale });
                tex.Apply();

                // PPU must match the canvas reference or a tiled Image draws one giant tile:
                // Unity sizes a tile as rect / (sprite ppu / reference ppu).
                s_checker = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f),
                    ReferencePixelsPerUnit);
                s_checker.name = "RetroTrough";
                s_checker.hideFlags = HideFlags.HideAndDontSave;
                return s_checker;
            }
        }

        /// <summary>A solid triangle for a scroll arrow or drop-down. Directions match <c>IconCanvas.Triangle</c>: 0 up, 1 down, 2 right, 3 left.</summary>
        public static Sprite Arrow(int direction)
        {
            if (s_inkArrows != RetroSkin.Text)
            {
                s_arrows.Clear();
                s_inkArrows = RetroSkin.Text;
            }

            if (s_arrows.TryGetValue(direction, out var cached) && cached) return cached;

            // Painted at 16 (the button size), triangle inset ~7x4 like a Win95
            // arrow. A sideways arrow wants that box on its side too.
            var canvas = new IconCanvas(16);
            var horizontal = direction >= 2;
            var w = horizontal ? 9f : 14f;
            var h = horizontal ? 14f : 9f;
            canvas.Triangle(16f - w * 0.5f, 16f - h * 0.5f, w, h,
                ToColor(RetroSkin.Text), direction);

            var sprite = canvas.ToSprite("RetroArrow." + direction);
            s_arrows[direction] = sprite;
            return sprite;
        }

        /// <summary>The tick beside an on menu item. Painted, not typed - U+2713 is missing from many fonts.</summary>
        public static Sprite Tick
        {
            get
            {
                if (s_tick && s_inkTick == RetroSkin.Text) return s_tick;
                s_inkTick = RetroSkin.Text;

                var canvas = new IconCanvas(12);
                var ink = ToColor(RetroSkin.Text);
                canvas.Line(7f, 17f, 13f, 23f, ink, 3f);
                canvas.Line(13f, 23f, 25f, 9f, ink, 3f);
                s_tick = canvas.ToSprite("RetroTick");
                return s_tick;
            }
        }

        static Color32 ToColor(Color c) => c;

        /// <summary>Drops the cache so the next request repaints in the new theme. Does not destroy - a graphic may point at the old sprite a frame longer.</summary>
        public static void Invalidate()
        {
            s_checker = null;
            s_tick = null;
            s_arrows.Clear();
        }
    }
}
