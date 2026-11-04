namespace Deepwave.RetroOS.UI
{
    /// <summary>Which palette colour a graphic was painted with. Recorded by <see cref="PaletteBook"/> at paint time - it cannot be guessed back from the colour, since schemes reuse values.</summary>
    public enum PaletteSlot
    {
        // Not from the palette (a picture, an icon tint, a chosen colour). Never repainted.
        None = 0,

        Face,
        FaceDark,
        FaceLight,
        FaceShadow,
        Text,
        TextDisabled,

        TitleActive,
        TitleActiveEnd,
        TitleIdle,
        TitleIdleEnd,
        TitleText,

        Field,
        Selection,
        SelectionText,
        Link,
        LinkVisited,
        Alert,
        Desktop,

        Screen,
        ScreenText,
        ScreenDim
    }
}
