using System.Collections.Generic;
using UnityEngine;

namespace Deepwave.RetroOS.UI
{
    /// <summary>A named repaint of the interface - the Control Panel's Appearance list. Sits on the <see cref="RetroTheme"/> for the session only, never written back.</summary>
    public class RetroColorScheme
    {
        public string Name = "Windows Standard";

        public Color Face = Hex("C0C0C0");
        public Color FaceDark = Hex("808080");
        public Color FaceLight = Hex("FFFFFF");
        public Color Text = Hex("000000");

        // The paper lists, edit boxes and address bars are written on. A scheme
        // colour, not a theme one, because a scheme (e.g. High Contrast Black)
        // can move Text off black and would then need matching paper.
        public Color Field = Hex("FFFFFF");

        public Color TitleActive = Hex("000080");
        public Color TitleActiveEnd = Hex("1084D0");
        public Color TitleIdle = Hex("808080");
        public Color TitleIdleEnd = Hex("B5B5B5");
        public Color TitleText = Hex("FFFFFF");

        public Color Selection = Hex("000080");
        public Color Desktop = Hex("008080");

        /// <summary>The colour this scheme paints a slot, or <c>false</c> if the slot belongs to the theme instead (field white, link blue, phosphor).</summary>
        public bool TryRead(PaletteSlot slot, out Color color)
        {
            switch (slot)
            {
                case PaletteSlot.Face: color = Face; return true;
                case PaletteSlot.FaceDark: color = FaceDark; return true;
                case PaletteSlot.FaceLight: color = FaceLight; return true;
                case PaletteSlot.Text: color = Text; return true;
                case PaletteSlot.Field: color = Field; return true;
                case PaletteSlot.TitleActive: color = TitleActive; return true;
                case PaletteSlot.TitleActiveEnd: color = TitleActiveEnd; return true;
                case PaletteSlot.TitleIdle: color = TitleIdle; return true;
                case PaletteSlot.TitleIdleEnd: color = TitleIdleEnd; return true;
                case PaletteSlot.TitleText: color = TitleText; return true;
                case PaletteSlot.Selection: color = Selection; return true;
                case PaletteSlot.Desktop: color = Desktop; return true;
                default: color = default; return false;
            }
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }

        // `field` is the list/edit-box paper; null = the white all but one scheme wants.
        static RetroColorScheme Make(string name, string face, string faceDark, string text,
            string titleA, string titleB, string titleText, string selection, string desktop,
            string field = null) =>
            new()
            {
                Name = name,
                Face = Hex(face),
                FaceDark = Hex(faceDark),
                FaceLight = Hex("FFFFFF"),
                Text = Hex(text),
                Field = Hex(field ?? "FFFFFF"),
                TitleActive = Hex(titleA),
                TitleActiveEnd = Hex(titleB),
                TitleIdle = Hex("808080"),
                TitleIdleEnd = Hex("B5B5B5"),
                TitleText = Hex(titleText),
                Selection = Hex(selection),
                Desktop = Hex(desktop)
            };

        /// <summary>The schemes that shipped with the machine, reproduced from the originals.</summary>
        public static IReadOnlyList<RetroColorScheme> All { get; } = new[]
        {
            Make("Windows Standard", "C0C0C0", "808080", "000000",
                "000080", "1084D0", "FFFFFF", "000080", "008080"),
            Make("Brick", "C0C0C0", "808080", "000000",
                "800000", "C05050", "FFFFFF", "800000", "6B4226"),
            Make("Desert", "D6CFAD", "9C9270", "000000",
                "80471C", "C08050", "FFFFFF", "80471C", "A08050"),
            Make("Eggplant", "B0AC9C", "6E6A5E", "000000",
                "403050", "6B5580", "FFFFFF", "403050", "204040"),
            Make("Lilac", "C8C0D0", "8C8498", "000000",
                "6A4B8C", "A084C0", "FFFFFF", "6A4B8C", "9080A8"),
            Make("Marine", "9CB0B8", "6C7C84", "000000",
                "004050", "0080A0", "FFFFFF", "004050", "205868"),
            Make("Plum", "C0B0C0", "887888", "000000",
                "503050", "8C5C8C", "FFFFFF", "503050", "6B4A6B"),
            Make("Rose", "D0C0C0", "988484", "000000",
                "804060", "C07090", "FFFFFF", "804060", "9C6478"),
            Make("Slate", "A8A8A8", "707070", "000000",
                "404040", "707070", "FFFFFF", "404040", "505050"),
            Make("Spruce", "B4C0B4", "7C887C", "000000",
                "004000", "008040", "FFFFFF", "004000", "306030"),
            Make("Storm", "B0B0B8", "787880", "000000",
                "303048", "606078", "FFFFFF", "303048", "484858"),
            Make("Teal (VGA)", "C0C0C0", "808080", "000000",
                "008080", "00A0A0", "FFFFFF", "008080", "008080"),
            Make("Wheat", "D8D0B0", "A09878", "000000",
                "806000", "C0A040", "FFFFFF", "806000", "A89860"),
            Make("High Contrast Black", "000000", "808080", "FFFFFF",
                "000080", "0000C0", "FFFFFF", "0000C0", "000000", field: "000000")
        };
    }
}
