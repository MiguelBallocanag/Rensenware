using UnityEngine;
using UnityEngine.UI;

namespace Deepwave.RetroOS.UI
{
    /// <summary>Paints interface from the palette and repaints on-screen graphics when the scheme changes - what makes Control Panel's Appearance page work on open windows.</summary>
    public static class RetroPalette
    {
        /// <summary>Paints a graphic from the palette and records the slot. A colour of the caller's own (a card face, a lerp) is assigned directly and stays out of the book.</summary>
        public static T Paint<T>(T graphic, PaletteSlot slot) where T : Graphic
        {
            if (!graphic) return graphic;

            var colour = RetroSkin.Read(slot);
            graphic.color = new Color(colour.r, colour.g, colour.b, graphic.color.a);
            RetroSkin.Book?.Record(graphic, slot);
            return graphic;
        }

        /// <summary>Paints from the palette at a caller-chosen alpha - a dimmer sheet, a show-through highlight.</summary>
        public static T Paint<T>(T graphic, PaletteSlot slot, float alpha) where T : Graphic
        {
            if (!graphic) return graphic;

            var colour = RetroSkin.Read(slot);
            graphic.color = new Color(colour.r, colour.g, colour.b, alpha);
            RetroSkin.Book?.Record(graphic, slot);
            return graphic;
        }

        /// <summary>Paints a highlight bar (selected row, open menu title) at full alpha and records the slot. Unlike <see cref="Paint{T}(T, PaletteSlot)"/> it does not inherit the graphic's alpha, so an off (transparent) highlight is not painted invisible.</summary>
        public static T Highlight<T>(T graphic, PaletteSlot slot) where T : Graphic =>
            Paint(graphic, slot, 1f);

        /// <summary>Paints a graphic a caller colour and drops it from the book, so a scheme change leaves it alone.</summary>
        public static T PaintOff<T>(T graphic, Color colour) where T : Graphic
        {
            if (!graphic) return graphic;

            graphic.color = colour;
            RetroSkin.Book?.Forget(graphic);
            return graphic;
        }

        /// <summary>Washes a graphic between two palette slots and records both ends.</summary>
        public static RetroGradient PaintGradient(Graphic graphic, PaletteSlot from, PaletteSlot to,
            GradientDirection direction = GradientDirection.Horizontal)
        {
            var gradient = RetroGradient.Apply(graphic, RetroSkin.Read(from), RetroSkin.Read(to),
                direction);
            RetroSkin.Book?.Record(gradient, from, to);
            return gradient;
        }

        /// <summary>Repaints one machine's interface in the current palette. Call via <see cref="MachineSkin.Repaint"/>, after the machine has claimed the palette.</summary>
        public static void Repaint(Transform root)
        {
            RetroSkin.Book?.Repaint();
            Restyle(root);
        }

        /// <summary>Tells widgets under <paramref name="root"/> to repaint parts a colour swap cannot reach - tick marks, progress blocks, scroll arrows. These rebuild children, so they cannot be in the book.</summary>
        public static void Restyle(Transform root)
        {
            if (!root) return;

            foreach (var widget in root.GetComponentsInChildren<Components.RetroWidget>(true))
                widget.ApplyTheme();
        }

        /// <summary>The colours the OS is painted with now, from scheme or theme.</summary>
        public static RetroColorScheme Current() => new()
        {
            Name = RetroSkin.Scheme?.Name ?? "Theme",
            Face = RetroSkin.Face,
            FaceDark = RetroSkin.FaceDark,
            FaceLight = RetroSkin.FaceLight,
            Text = RetroSkin.Text,
            TitleActive = RetroSkin.Title,
            TitleActiveEnd = RetroSkin.TitleEnd,
            TitleIdle = RetroSkin.TitleIdle,
            TitleIdleEnd = RetroSkin.TitleIdleEnd,
            TitleText = RetroSkin.TitleText,
            Selection = RetroSkin.Selection,
            Desktop = RetroSkin.Desktop
        };
    }
}
