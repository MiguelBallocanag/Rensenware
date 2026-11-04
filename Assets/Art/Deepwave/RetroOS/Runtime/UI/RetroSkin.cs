using System;
using UnityEngine;

namespace Deepwave.RetroOS.UI
{
    /// <summary>
    /// The live look of the OS - every colour, metric and font. One per process:
    /// it holds the look interface is being built with right now, which a machine
    /// claims via <see cref="Use"/>. With no theme, stock Windows 95 values.
    /// </summary>
    public static class RetroSkin
    {
        static RetroTheme _theme;

        /// <summary>Raised when the theme changes, so on-screen interface can restyle. Every <see cref="Components.RetroWidget"/> listens while enabled.</summary>
        public static event Action Changed;

        public static RetroTheme Theme
        {
            get => _theme ? _theme : _theme = RetroTheme.CreateDefault();
            set
            {
                _theme = value;
                _ui = null;
                _mono = null;
                _widgetSounds = null;
                _scheme = null;
                Changed?.Invoke();
            }
        }

        static RetroColorScheme _scheme;

        /// <summary>The colour scheme over the theme, or <c>null</c> for the theme's own. Set from Control Panel; session-only, never touches the asset.</summary>
        public static RetroColorScheme Scheme
        {
            get => _scheme;
            set
            {
                if (_scheme == value) return;
                _scheme = value;
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// Hands the palette to one machine: its theme, scheme and sounds from
        /// here on. Nothing on screen is repainted and no <see cref="Changed"/>
        /// fires - use <see cref="MachineSkin.Repaint"/> for that.
        /// </summary>
        /// <param name="book">Where painted graphics are recorded for a later repaint; <c>null</c> for a build nobody repaints.</param>
        public static void Use(RetroTheme theme, RetroColorScheme scheme, bool? widgetSounds = null,
            PaletteBook book = null)
        {
            if (_theme != theme)
            {
                _theme = theme;
                _ui = null;
                _mono = null;
            }

            _scheme = scheme;
            _widgetSounds = widgetSounds;
            _book = book;
        }

        /// <summary>Everything one machine's claim consists of, so it can be restored. Opaque - hand back exactly what you took (see <see cref="MachineSkin.While"/>).</summary>
        public readonly struct Claim
        {
            internal readonly RetroTheme Theme;
            internal readonly RetroColorScheme Scheme;
            internal readonly bool? Sounds;
            internal readonly PaletteBook Book;

            internal Claim(RetroTheme theme, RetroColorScheme scheme, bool? sounds, PaletteBook book)
            {
                Theme = theme;
                Scheme = scheme;
                Sounds = sounds;
                Book = book;
            }
        }

        /// <summary>Who holds the palette right now.</summary>
        public static Claim Held => new(_theme, _scheme, _widgetSounds, _book);

        /// <summary>Hands the palette back to whoever held it.</summary>
        public static void Give(Claim claim) =>
            Use(claim.Theme, claim.Scheme, claim.Sounds, claim.Book);

        static PaletteBook _book;

        /// <summary>Where currently-painted graphics are recorded. Ambient like the colours, and claimed with them, so a graphic lands in the same machine's book as its colour.</summary>
        public static PaletteBook Book => _book;

        /// <summary>Tells every widget in the scene to restyle. For single-machine game code; a machine that owns its look uses <see cref="RetroPalette.Repaint"/>.</summary>
        public static void Refresh() => Changed?.Invoke();

        // --- palette ---

        public static Color Face => _scheme?.Face ?? Theme.face;
        public static Color FaceDark => _scheme?.FaceDark ?? Theme.faceDark;
        public static Color FaceLight => _scheme?.FaceLight ?? Theme.faceLight;
        public static Color FaceShadow => Theme.faceShadow;
        public static Color Text => _scheme?.Text ?? Theme.text;
        public static Color TextDisabled => Theme.textDisabled;

        public static Color Title => _scheme?.TitleActive ?? Theme.titleActive;

        public static Color TitleEnd => Theme.gradientTitleBars
            ? _scheme?.TitleActiveEnd ?? Theme.titleActiveEnd
            : Title;

        public static Color TitleIdle => _scheme?.TitleIdle ?? Theme.titleIdle;

        public static Color TitleIdleEnd => Theme.gradientTitleBars
            ? _scheme?.TitleIdleEnd ?? Theme.titleIdleEnd
            : TitleIdle;

        public static Color TitleText => _scheme?.TitleText ?? Theme.titleText;

        public static Color Field => _scheme?.Field ?? Theme.field;
        public static Color Selection => _scheme?.Selection ?? Theme.selection;
        public static Color SelectionText => Theme.selectionText;
        public static Color Link => Theme.link;
        public static Color LinkVisited => Theme.linkVisited;
        public static Color Alert => Theme.alert;
        public static Color Desktop => _scheme?.Desktop ?? Theme.desktop;

        public static Color Screen => Theme.screen;
        public static Color ScreenText => Theme.screenText;
        public static Color ScreenDim => Theme.screenDim;

        // --- metrics ---

        public static float TaskbarHeight => Theme.taskbarHeight;
        public static float TitleBarHeight => Theme.titleBarHeight;
        public static float MenuBarHeight => Theme.menuBarHeight;
        public static float ToolbarHeight => Theme.toolbarHeight;
        public static float StatusBarHeight => Theme.statusBarHeight;
        public static float ListRowHeight => Theme.listRowHeight;
        public static float ScrollBarSize => Theme.scrollBarSize;
        public static float BevelThickness => Theme.bevelThickness;

        public static bool HoverHighlights => Theme.hoverHighlights;

        static bool? _widgetSounds;

        /// <summary>Whether widgets click and beep. Theme sets the start value; setting this overrides it for the session only (a ScriptableObject edit would stick to the asset).</summary>
        public static bool WidgetSounds
        {
            get => _widgetSounds ?? Theme.widgetSounds;
            set => _widgetSounds = value;
        }

        // --- fonts ---

        static Font _ui, _mono;

        public static Font Ui
        {
            get
            {
                if (_ui) return _ui;
                if (Theme.uiFont) return _ui = Theme.uiFont;
                return _ui = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Mono;
            }
        }

        public static Font Mono
        {
            get
            {
                if (_mono) return _mono;
                if (Theme.monoFont) return _mono = Theme.monoFont;
                return _mono = Font.CreateDynamicFontFromOSFont(
                    new[] { "Consolas", "Courier New", "Lucida Console" }, 14);
            }
        }

        public static int FontSize => Theme.baseFontSize;

        // --- reading a slot ---

        /// <summary>The colour a <see cref="PaletteSlot"/> is worth right now. Resolves through the scheme, then the theme - so a slot the scheme ignores keeps the theme's value.</summary>
        public static Color Read(PaletteSlot slot) => slot switch
        {
            PaletteSlot.Face => Face,
            PaletteSlot.FaceDark => FaceDark,
            PaletteSlot.FaceLight => FaceLight,
            PaletteSlot.FaceShadow => FaceShadow,
            PaletteSlot.Text => Text,
            PaletteSlot.TextDisabled => TextDisabled,
            PaletteSlot.TitleActive => Title,
            PaletteSlot.TitleActiveEnd => TitleEnd,
            PaletteSlot.TitleIdle => TitleIdle,
            PaletteSlot.TitleIdleEnd => TitleIdleEnd,
            PaletteSlot.TitleText => TitleText,
            PaletteSlot.Field => Field,
            PaletteSlot.Selection => Selection,
            PaletteSlot.SelectionText => SelectionText,
            PaletteSlot.Link => Link,
            PaletteSlot.LinkVisited => LinkVisited,
            PaletteSlot.Alert => Alert,
            PaletteSlot.Desktop => Desktop,
            PaletteSlot.Screen => Screen,
            PaletteSlot.ScreenText => ScreenText,
            PaletteSlot.ScreenDim => ScreenDim,
            _ => Color.clear
        };
    }
}
