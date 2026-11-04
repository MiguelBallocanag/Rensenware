using UnityEngine;
using Deepwave.RetroOS.Apps.Games;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Apps.Components
{
    // Klondike patience. The card game itself is public domain.
    [AddComponentMenu("")]
    public class RetroSolitaireApp : RetroAppBehaviour
    {
        public override void BuildContent(AppContext ctx) => AppView.Attach<SolitaireView>(ctx);

        public override void ApplyDefaults()
        {
            app.appId = "solitaire";
            app.title = "Solitaire";
            app.builtinIcon = BuiltinIcon.Cards;
            app.category = "Games";
            app.windowSize = new Vector2(600, 460);
            app.minWindowSize = new Vector2(520, 380);
        }
    }
}
