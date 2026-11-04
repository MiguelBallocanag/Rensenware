using UnityEngine;
using UnityEngine.UI;
using Deepwave.RetroOS.Audio;

namespace Deepwave.RetroOS.UI.Components
{
    // How a widget's 3D border is drawn.
    public enum BorderStyle
    {
        None,
        Raised,      // buttons, panels, the taskbar
        Sunken,      // text fields, list boxes, status panes
        RaisedThin,  // toolbar buttons, title-bar glyph buttons
        SunkenThin,  // separators, tray, group boxes
        Etched       // group-box frames, menu separators
    }

    // Base for the component widgets: plain MonoBehaviours built by a static
    // Create(...) factory, each owning its look so a fix here reaches every use.
    [RequireComponent(typeof(RectTransform))]
    public abstract class RetroWidget : MonoBehaviour
    {
        RectTransform _rt;
        bool _interactable = true;

        public RectTransform Rect => _rt ? _rt : _rt = (RectTransform)transform;

        // Disabled widgets stop responding and grey their text.
        public bool Interactable
        {
            get => _interactable;
            set
            {
                if (_interactable == value) return;
                _interactable = value;
                OnInteractableChanged(value);
            }
        }

        protected virtual void OnInteractableChanged(bool on) { }

        // Restyle after a theme swap. Override to repaint cached graphics.
        public virtual void ApplyTheme() { }

        protected virtual void OnEnable() => RetroSkin.Changed += ApplyTheme;
        protected virtual void OnDisable() => RetroSkin.Changed -= ApplyTheme;

        protected static void Click()
        {
            if (RetroSkin.WidgetSounds) RetroSfx.Click();
        }

        // --- shared construction helpers ---

        // Creates the widget's own root rect under parent and attaches T.
        protected static T Spawn<T>(Transform parent, string name) where T : RetroWidget
        {
            var rt = RetroUi.NewRect(name, parent);
            return rt.gameObject.AddComponent<T>();
        }

        protected static Image Background(Transform parent, Color color, string name = "Bg")
        {
            var img = RetroUi.NewRect(name, parent).gameObject.AddComponent<Image>();
            img.color = color;
            RetroUi.Stretch(img.rectTransform);
            return img;
        }

        // The same, painted from the palette and recorded for a scheme change.
        protected static Image Background(Transform parent, PaletteSlot slot, string name = "Bg")
        {
            var img = RetroUi.NewRect(name, parent).gameObject.AddComponent<Image>();
            RetroPalette.Paint(img, slot);
            RetroUi.Stretch(img.rectTransform);
            return img;
        }

        // Draws the requested 3D border onto an existing rect.
        public static RetroBevel Border(RectTransform rt, BorderStyle style)
        {
            switch (style)
            {
                case BorderStyle.Raised: return RetroUi.Bevel(rt, true, RetroSkin.BevelThickness);
                case BorderStyle.Sunken: return RetroUi.Bevel(rt, false, RetroSkin.BevelThickness);
                case BorderStyle.RaisedThin: return RetroUi.Bevel(rt, true, 1f);
                case BorderStyle.SunkenThin: return RetroUi.Bevel(rt, false, 1f);
                case BorderStyle.Etched: return RetroUi.Bevel(rt, false, 1f);
                default: return null;
            }
        }
    }
}
