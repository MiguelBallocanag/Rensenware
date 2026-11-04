using UnityEngine;
using Deepwave.RetroOS.Apps.Games;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Apps.Components
{
    // FreeCell patience. The card game itself is public domain.
    [AddComponentMenu("")]
    public class RetroFreeCellApp : RetroAppBehaviour
    {
        public override void BuildContent(AppContext ctx) => AppView.Attach<FreeCellView>(ctx);

        public override void ApplyDefaults()
        {
            app.appId = "freecell";
            app.title = "FreeCell";
            app.builtinIcon = BuiltinIcon.FreeCell;
            app.category = "Games";
            app.windowSize = new Vector2(620, 470);
            app.minWindowSize = new Vector2(540, 400);
        }
    }
}
