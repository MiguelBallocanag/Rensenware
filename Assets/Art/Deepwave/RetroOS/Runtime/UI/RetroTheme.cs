using UnityEngine;

namespace Deepwave.RetroOS.UI
{
    // Skinning asset for the whole OS - every colour, metric and font the UI draws
    // with. A game reskins RetroOS (teal 95, amber terminal...) without code.
    // Assign one on RetroOsBootstrap > Look > Theme; none = built-in Windows 95.
    [CreateAssetMenu(menuName = "RetroOS/Theme", fileName = "RetroTheme")]
    public class RetroTheme : ScriptableObject
    {
        [Header("Chrome")]
        [Tooltip("Window/button body colour - the classic silver.")]
        public Color face = Hex("C0C0C0");
        [Tooltip("Bevel shadow (bottom-right when raised).")]
        public Color faceDark = Hex("808080");
        [Tooltip("Bevel highlight (top-left when raised).")]
        public Color faceLight = Hex("FFFFFF");
        [Tooltip("Outermost 1px frame line on 3D borders.")]
        public Color faceShadow = Hex("000000");
        public Color text = Hex("000000");
        public Color textDisabled = Hex("808080");

        [Header("Title Bars")]
        public Color titleActive = Hex("000080");
        [Tooltip("Second colour of the title-bar gradient. Match titleActive for flat 95.")]
        public Color titleActiveEnd = Hex("1084D0");
        public Color titleIdle = Hex("808080");
        public Color titleIdleEnd = Hex("B5B5B5");
        public Color titleText = Hex("FFFFFF");
        [Tooltip("Draw title bars as a left-to-right gradient (Win98) or flat (Win95).")]
        public bool gradientTitleBars = true;

        [Header("Content")]
        public Color field = Hex("FFFFFF");
        public Color selection = Hex("000080");
        public Color selectionText = Hex("FFFFFF");
        public Color link = Hex("0000EE");
        public Color linkVisited = Hex("551A8B");
        public Color alert = Hex("990000");
        public Color desktop = Hex("008080");

        [Header("Terminal / CRT")]
        public Color screen = Hex("0A0A0A");
        public Color screenText = Hex("33FF66");
        public Color screenDim = Hex("1E7A38");

        [Header("Metrics")]
        public float taskbarHeight = 34f;
        public float titleBarHeight = 22f;
        public float menuBarHeight = 18f;
        public float toolbarHeight = 26f;
        public float statusBarHeight = 20f;
        public float listRowHeight = 20f;
        public float scrollBarSize = 16f;
        [Tooltip("Thickness of the raised/sunken 3D borders, in pixels.")]
        public float bevelThickness = 2f;

        [Header("Typography")]
        [Tooltip("Optional UI font. Empty = Unity's legacy runtime font.")]
        public Font uiFont;
        [Tooltip("Optional monospace font for terminals and code. Empty = OS Consolas/Courier.")]
        public Font monoFont;
        public int baseFontSize = 14;

        [Header("Icons")]
        [Tooltip("Resolution asked for when nothing says what size it wants. " +
                 "Widgets ask for the size of the box they are drawing into, so " +
                 "this is a floor rather than a ceiling: leave it at 32 unless " +
                 "your own code calls RetroIcons.Get without a size.")]
        public IconResolution iconResolution = IconResolution.Classic32;
        [Tooltip("Draw the extra highlight/shadow passes that make icons pop. " +
                 "Turn off for a flatter, more period-accurate look.")]
        public bool vividIcons = true;

        [Header("Feel")]
        [Tooltip("Sound every widget interaction (clicks, toggles, menu opens).")]
        public bool widgetSounds = true;
        [Tooltip("Highlight rows and buttons under the cursor.")]
        public bool hoverHighlights = true;

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }

        // A fresh instance of the stock Windows 95 look.
        public static RetroTheme CreateDefault()
        {
            var theme = CreateInstance<RetroTheme>();
            theme.name = "RetroOS 95 (built-in)";
            return theme;
        }
    }

    public enum IconResolution
    {
        Classic32 = 32,
        High48 = 48,
        Ultra64 = 64
    }
}
