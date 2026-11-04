using UnityEngine;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Apps.Components
{
    // The desk calculator, with memory keys and the sunken readout.
    [AddComponentMenu("")]
    public class RetroCalculatorApp : RetroAppBehaviour
    {
        public override void BuildContent(AppContext ctx) => AppView.Attach<CalculatorView>(ctx);

        public override void ApplyDefaults()
        {
            app.appId = "calculator";
            app.title = "Calculator";
            app.builtinIcon = BuiltinIcon.Calculator;
            app.category = "Accessories";
            // Sized to its keypad; growing it just stretches the keys.
            app.windowSize = new Vector2(320, 300);
            app.minWindowSize = new Vector2(300, 280);
            app.resizable = false;
            app.showOnDesktop = false;
        }
    }
}
