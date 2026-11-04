using UnityEngine;
using UnityEngine.UI;

namespace Deepwave.RetroOS.UI
{
    // Holds the four bevel edges so the raised/sunken state can be flipped.
    public class RetroBevel : MonoBehaviour
    {
        Image[] _topLeft, _bottomRight;

        public void Init(Image[] topLeft, Image[] bottomRight)
        {
            _topLeft = topLeft;
            _bottomRight = bottomRight;
        }

        public void SetRaised(bool raised)
        {
            var topLeft = raised ? PaletteSlot.FaceLight : PaletteSlot.FaceDark;
            var bottomRight = raised ? PaletteSlot.FaceDark : PaletteSlot.FaceLight;

            foreach (var img in _topLeft) RetroPalette.Paint(img, topLeft);
            foreach (var img in _bottomRight) RetroPalette.Paint(img, bottomRight);
        }

        // Hides the border entirely - a cleared minefield square, a flat cell.
        public void SetVisible(bool visible)
        {
            foreach (var img in _topLeft) img.enabled = visible;
            foreach (var img in _bottomRight) img.enabled = visible;
        }
    }
}
