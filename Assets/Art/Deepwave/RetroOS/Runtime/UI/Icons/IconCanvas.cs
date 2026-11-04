using UnityEngine;

namespace Deepwave.RetroOS.UI.Icons
{
    // A tiny pixel-art canvas the built-in icons are painted on. Icons are authored
    // in a 32x32 design grid but rasterised at 32/48/64, so one code path gives
    // chunky pixels at 32 and crisp icons at 64. Primitives only, to look like 1995.
    public class IconCanvas
    {
        public const int Design = 32;

        readonly Color32[] _pixels;

        public int Size { get; }
        public float Scale { get; }

        /// <summary>Whether the canvas is small enough that fine detail turns to mud (&lt;= 20). A painter with something simpler to say at this size should.</summary>
        public bool Small => Size <= 20;

        public IconCanvas(int size)
        {
            Size = Mathf.Max(8, size);
            Scale = Size / (float)Design;
            _pixels = new Color32[Size * Size];
            Clear();
        }

        public void Clear()
        {
            var blank = new Color32(0, 0, 0, 0);
            for (var i = 0; i < _pixels.Length; i++) _pixels[i] = blank;
        }

        // --- raw access (device pixels) ---

        public void SetPixel(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= Size || y >= Size || c.a == 0) return;
            var i = y * Size + x;
            if (c.a == 255) { _pixels[i] = c; return; }

            // Simple source-over so soft shadows and glazes work.
            var dst = _pixels[i];
            var a = c.a / 255f;
            var ia = 1f - a;
            var outA = c.a + dst.a * ia;
            _pixels[i] = new Color32(
                (byte)(c.r * a + dst.r * (dst.a / 255f) * ia),
                (byte)(c.g * a + dst.g * (dst.a / 255f) * ia),
                (byte)(c.b * a + dst.b * (dst.a / 255f) * ia),
                (byte)Mathf.Clamp(outA, 0f, 255f));
        }

        public Color32 GetPixel(int x, int y) =>
            x < 0 || y < 0 || x >= Size || y >= Size ? new Color32(0, 0, 0, 0) : _pixels[y * Size + x];

        // --- design-space drawing ---

        int P(float designUnits) => Mathf.RoundToInt(designUnits * Scale);

        // Filled rectangle, in design units (0..32, origin top-left).
        // A requested shape never rounds away to nothing (a 1-unit line at 16px would).
        public void Fill(float x, float y, float w, float h, Color32 c)
        {
            int x0 = P(x), y0 = P(y), x1 = P(x + w), y1 = P(y + h);
            if (w > 0f && x1 <= x0) x1 = x0 + 1;
            if (h > 0f && y1 <= y0) y1 = y0 + 1;

            for (var py = y0; py < y1; py++)
                for (var px = x0; px < x1; px++)
                    SetPixel(px, Size - 1 - py, c);
        }

        // 1-design-unit outline.
        public void Rect(float x, float y, float w, float h, Color32 c, float thickness = 1f)
        {
            Fill(x, y, w, thickness, c);
            Fill(x, y + h - thickness, w, thickness, c);
            Fill(x, y, thickness, h, c);
            Fill(x + w - thickness, y, thickness, h, c);
        }

        // The signature raised/sunken 3D box: highlight top-left, shadow
        // bottom-right, optional hard black outer frame.
        public void Bevel(float x, float y, float w, float h, Color32 face, bool raised = true,
            bool outline = true)
        {
            var light = raised ? Palette.White : Palette.Gray;
            var dark = raised ? Palette.Gray : Palette.White;

            if (outline) Rect(x, y, w, h, Palette.Black);
            var inset = outline ? 1f : 0f;
            Fill(x + inset, y + inset, w - inset * 2f, h - inset * 2f, face);
            Fill(x + inset, y + inset, w - inset * 2f, 1f, light);
            Fill(x + inset, y + inset, 1f, h - inset * 2f, light);
            Fill(x + inset, y + h - inset - 1f, w - inset * 2f, 1f, dark);
            Fill(x + w - inset - 1f, y + inset, 1f, h - inset * 2f, dark);
        }

        // Vertical two-colour ramp, quantised to a few bands and dithered between
        // them - a 16-colour machine checkerboarded the joins rather than lerping per row.
        public void Gradient(float x, float y, float w, float h, Color32 top, Color32 bottom,
            bool dither = true, int bands = 4)
        {
            int x0 = P(x), y0 = P(y), x1 = P(x + w), y1 = P(y + h);
            bands = Mathf.Max(2, bands);
            var span = Mathf.Max(1, y1 - y0);

            var steps = new Color32[bands];
            for (var i = 0; i < bands; i++)
                steps[i] = Color32.Lerp(top, bottom, i / (float)(bands - 1));

            for (var py = y0; py < y1; py++)
            {
                var t = (py - y0) / (float)span * (bands - 1);
                var lo = Mathf.Clamp(Mathf.FloorToInt(t), 0, bands - 1);
                var hi = Mathf.Min(lo + 1, bands - 1);
                var frac = t - lo;
                for (var px = x0; px < x1; px++)
                {
                    // Ordered 2x2 dither so the band joint breaks up instead of stepping hard.
                    var checker = ((px + py) & 1) == 0 ? 0.35f : 0.75f;
                    var c = dither && frac > checker ? steps[hi] : steps[lo];
                    SetPixel(px, Size - 1 - py, c);
                }
            }
        }

        // 50% checkerboard blend - the classic way to fake a third colour.
        public void Dither(float x, float y, float w, float h, Color32 a, Color32 b)
        {
            int x0 = P(x), y0 = P(y), x1 = P(x + w), y1 = P(y + h);
            for (var py = y0; py < y1; py++)
                for (var px = x0; px < x1; px++)
                    SetPixel(px, Size - 1 - py, ((px + py) & 1) == 0 ? a : b);
        }

        public void Line(float x0, float y0, float x1, float y1, Color32 c, float thickness = 1f)
        {
            int px0 = P(x0), py0 = P(y0), px1 = P(x1), py1 = P(y1);
            var t = Mathf.Max(1, P(thickness));
            var dx = Mathf.Abs(px1 - px0);
            var dy = -Mathf.Abs(py1 - py0);
            var sx = px0 < px1 ? 1 : -1;
            var sy = py0 < py1 ? 1 : -1;
            var err = dx + dy;

            while (true)
            {
                for (var oy = 0; oy < t; oy++)
                    for (var ox = 0; ox < t; ox++)
                        SetPixel(px0 + ox, Size - 1 - (py0 + oy), c);
                if (px0 == px1 && py0 == py1) break;
                var e2 = err * 2;
                if (e2 >= dy) { err += dy; px0 += sx; }
                if (e2 <= dx) { err += dx; py0 += sy; }
            }
        }

        // Radius rounded to whole device pixels first, so two radii a unit apart
        // are a whole pixel apart (a 0.75px rim otherwise draws as a dotted outline).
        public void Disc(float cx, float cy, float radius, Color32 c)
        {
            var ri = Mathf.Max(0, Mathf.RoundToInt(radius * Scale));
            int pcx = P(cx), pcy = P(cy);
            var rr = ri * ri;
            for (var dy = -ri; dy <= ri; dy++)
                for (var dx = -ri; dx <= ri; dx++)
                    if (dx * dx + dy * dy <= rr)
                        SetPixel(pcx + dx, Size - 1 - (pcy + dy), c);
        }

        // Filled ellipse - a drum seen in three-quarter view, a disc lying
        // flat, a mouth. Round subjects that are not circles.
        public void Ellipse(float cx, float cy, float rx, float ry, Color32 c)
        {
            float ax = rx * Scale, ay = ry * Scale;
            int pcx = P(cx), pcy = P(cy);
            for (var dy = -Mathf.CeilToInt(ay); dy <= Mathf.CeilToInt(ay); dy++)
                for (var dx = -Mathf.CeilToInt(ax); dx <= Mathf.CeilToInt(ax); dx++)
                    if (dx * dx / (ax * ax) + dy * dy / (ay * ay) <= 1f)
                        SetPixel(pcx + dx, Size - 1 - (pcy + dy), c);
        }

        public void EllipseRing(float cx, float cy, float rx, float ry, Color32 c)
        {
            float ax = rx * Scale, ay = ry * Scale;
            // Band at least one whole pixel wide, or it draws as a dotted line.
            var band = Mathf.Max(0.62f, 1f - 2f / Mathf.Max(1f, Mathf.Min(ax, ay)));
            int pcx = P(cx), pcy = P(cy);
            for (var dy = -Mathf.CeilToInt(ay); dy <= Mathf.CeilToInt(ay); dy++)
                for (var dx = -Mathf.CeilToInt(ax); dx <= Mathf.CeilToInt(ax); dx++)
                {
                    var d = dx * dx / (ax * ax) + dy * dy / (ay * ay);
                    if (d <= 1f && d >= band * band)
                        SetPixel(pcx + dx, Size - 1 - (pcy + dy), c);
                }
        }

        public void Ring(float cx, float cy, float radius, Color32 c, float thickness = 1f)
        {
            // Whole device pixels, never thinner than one: a half-pixel annulus dots.
            var outer = Mathf.Max(1, Mathf.RoundToInt(radius * Scale));
            var inner = Mathf.Clamp(Mathf.RoundToInt((radius - thickness) * Scale), 0, outer - 1);
            int pcx = P(cx), pcy = P(cy);
            for (var dy = -outer; dy <= outer; dy++)
                for (var dx = -outer; dx <= outer; dx++)
                {
                    var d = dx * dx + dy * dy;
                    if (d <= outer * outer && d >= inner * inner)
                        SetPixel(pcx + dx, Size - 1 - (pcy + dy), c);
                }
        }

        // A ring in two checkerboarded colours - the period-correct way to sheen
        // something round (a translucent ring would be depth from opacity).
        public void DitherRing(float cx, float cy, float radius, float thickness, Color32 a, Color32 b)
        {
            var outer = Mathf.Max(1, Mathf.RoundToInt(radius * Scale));
            var inner = Mathf.Clamp(Mathf.RoundToInt((radius - thickness) * Scale), 0, outer - 1);
            int pcx = P(cx), pcy = P(cy);
            for (var dy = -outer; dy <= outer; dy++)
                for (var dx = -outer; dx <= outer; dx++)
                {
                    var d = dx * dx + dy * dy;
                    if (d > outer * outer || d < inner * inner) continue;
                    var px = pcx + dx;
                    var py = pcy + dy;
                    SetPixel(px, Size - 1 - py, ((px + py) & 1) == 0 ? a : b);
                }
        }

        // A box seen three-quarters on: front face, lid sheared up-right at 45deg,
        // right cheek. The single thing that makes the Win9x shell set look the way
        // it does - a drive, printer, chip and tower are all this box. (x,y,w,h) is
        // the FRONT face; a black silhouette a unit proud is laid down first.
        public void Box(float x, float y, float w, float h, float depth,
            Color32 face, Color32 top, Color32 side, bool outline = true)
        {
            if (outline) BoxFaces(x - 1f, y - 1f, w + 2f, h + 2f, depth, Palette.Black,
                Palette.Black, Palette.Black);
            BoxFaces(x, y, w, h, depth, face, top, side);
        }

        // The three faces alone - for a box built up in pieces or inside another outline.
        public void BoxFaces(float x, float y, float w, float h, float depth,
            Color32 face, Color32 top, Color32 side)
        {
            var steps = Mathf.Max(0, Mathf.RoundToInt(depth));
            for (var i = 1; i <= steps; i++)
            {
                Fill(x + i, y - i, w, 1f, top);
                Fill(x + w + i - 1f, y - i, 1f, h, side);
            }
            Fill(x, y, w, h, face);
        }

        // Axis-aligned triangle: pointing Up/Down/Left/Right inside a box.
        public void Triangle(float x, float y, float w, float h, Color32 c, int direction)
        {
            int x0 = P(x), y0 = P(y), x1 = P(x + w), y1 = P(y + h);
            var pw = Mathf.Max(1, x1 - x0);
            var ph = Mathf.Max(1, y1 - y0);

            for (var py = 0; py < ph; py++)
                for (var px = 0; px < pw; px++)
                {
                    var u = px / (float)pw;
                    var v = py / (float)ph;
                    var inside = direction switch
                    {
                        0 => Mathf.Abs(u - 0.5f) <= v * 0.5f,        // up
                        1 => Mathf.Abs(u - 0.5f) <= (1f - v) * 0.5f, // down
                        // Apex is the edge the arrow points at: a right wedge is widest on the left.
                        2 => Mathf.Abs(v - 0.5f) <= (1f - u) * 0.5f, // right
                        _ => Mathf.Abs(v - 0.5f) <= u * 0.5f         // left
                    };
                    if (inside) SetPixel(x0 + px, Size - 1 - (y0 + py), c);
                }
        }

        // --- erasing ---
        // Drawing transparent does nothing (alpha 0 = no ink), so cutting a hole needs its own call.

        public void Erase(float x, float y, float w, float h)
        {
            int x0 = P(x), y0 = P(y), x1 = P(x + w), y1 = P(y + h);
            for (var py = y0; py < y1; py++)
                for (var px = x0; px < x1; px++)
                    Clear(px, Size - 1 - py);
        }

        public void EraseDisc(float cx, float cy, float radius)
        {
            var r = radius * Scale;
            int pcx = P(cx), pcy = P(cy);
            var ri = Mathf.CeilToInt(r);
            for (var dy = -ri; dy <= ri; dy++)
                for (var dx = -ri; dx <= ri; dx++)
                    if (dx * dx + dy * dy <= r * r)
                        Clear(pcx + dx, Size - 1 - (pcy + dy));
        }

        void Clear(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Size || y >= Size) return;
            _pixels[y * Size + x] = new Color32(0, 0, 0, 0);
        }

        // Hard drop shadow - offset, not blurred. A blur would be a 2010s shadow.
        public void Shadow(float x, float y, float w, float h, float offset = 1.5f)
        {
            Fill(x + offset, y + offset, w, h, new Color32(0, 0, 0, 70));
        }

        // The same for a round subject (a rect offset behind a disc shows four corners).
        public void DiscShadow(float cx, float cy, float radius, float offset = 1.5f)
        {
            Disc(cx + offset, cy + offset, radius, new Color32(0, 0, 0, 70));
        }

        // Specular sheen along the top-left of a filled shape.
        public void Sheen(float x, float y, float w, float h, byte alpha = 90)
        {
            Fill(x, y, w, Mathf.Max(1f, h * 0.28f), new Color32(255, 255, 255, alpha));
        }

        public Sprite ToSprite(string name)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            tex.SetPixels32(_pixels);
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        public Texture2D ToTexture(string name)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.SetPixels32(_pixels);
            tex.Apply();
            return tex;
        }
    }

    // The Windows 95 VGA palette plus a few extra shades for the vivid icon set.
    public static class Palette
    {
        public static readonly Color32 Clear = new(0, 0, 0, 0);
        public static readonly Color32 Black = new(0, 0, 0, 255);
        public static readonly Color32 White = new(255, 255, 255, 255);
        public static readonly Color32 Silver = new(192, 192, 192, 255);
        public static readonly Color32 Gray = new(128, 128, 128, 255);
        public static readonly Color32 DarkGray = new(64, 64, 64, 255);
        public static readonly Color32 LightSilver = new(223, 223, 223, 255);

        public static readonly Color32 Navy = new(0, 0, 128, 255);
        public static readonly Color32 Blue = new(0, 0, 255, 255);
        public static readonly Color32 SkyBlue = new(96, 160, 255, 255);
        public static readonly Color32 Teal = new(0, 128, 128, 255);
        public static readonly Color32 Cyan = new(0, 255, 255, 255);

        public static readonly Color32 Green = new(0, 255, 0, 255);
        public static readonly Color32 DarkGreen = new(0, 128, 0, 255);
        public static readonly Color32 Lime = new(128, 255, 128, 255);

        public static readonly Color32 Maroon = new(128, 0, 0, 255);
        public static readonly Color32 Red = new(255, 0, 0, 255);
        public static readonly Color32 Salmon = new(255, 128, 128, 255);

        public static readonly Color32 Olive = new(128, 128, 0, 255);
        public static readonly Color32 Yellow = new(255, 255, 0, 255);
        public static readonly Color32 Gold = new(255, 208, 64, 255);
        public static readonly Color32 Amber = new(200, 150, 32, 255);

        public static readonly Color32 Purple = new(128, 0, 128, 255);
        public static readonly Color32 Magenta = new(255, 0, 255, 255);
        public static readonly Color32 Lavender = new(200, 160, 255, 255);

        public static readonly Color32 Manila = new(255, 220, 130, 255);
        public static readonly Color32 ManilaDark = new(196, 152, 60, 255);
        public static readonly Color32 Paper = new(252, 252, 248, 255);
        public static readonly Color32 Skin = new(255, 214, 170, 255);
    }
}
