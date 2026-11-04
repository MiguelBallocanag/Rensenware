using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Deepwave.RetroOS.UI
{
    /// <summary>Instant Win95 hover: navy background, white text, no animation. Reads colours by slot rather than storing them, so a scheme change leaves no stale rows.</summary>
    public class HoverHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        Image _bg;
        Text _label;
        PaletteSlot _bgSlot, _textSlot;

        /// <param name="bgSlot">The row's background slot, or <see cref="PaletteSlot.None"/> for a row transparent when not hovered.</param>
        public void Init(Image bg, Text label, PaletteSlot bgSlot, PaletteSlot textSlot)
        {
            _bg = bg;
            _label = label;
            _bgSlot = bgSlot;
            _textSlot = textSlot;
        }

        public void OnPointerEnter(PointerEventData e)
        {
            RetroPalette.Paint(_bg, PaletteSlot.TitleActive);
            RetroPalette.Paint(_label, PaletteSlot.TitleText);
        }

        public void OnPointerExit(PointerEventData e)
        {
            if (_bgSlot == PaletteSlot.None) RetroPalette.PaintOff(_bg, Color.clear);
            else RetroPalette.Paint(_bg, _bgSlot);

            RetroPalette.Paint(_label, _textSlot);
        }

        void OnDisable()
        {
            if (_bg) OnPointerExit(null);
        }
    }
}
