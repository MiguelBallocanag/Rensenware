using UnityEngine;
using Deepwave.RetroOS.Apps.Games;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Apps.Components
{
    // Reversi. The board game dates from 1883 and is public domain.
    [AddComponentMenu("")]
    public class RetroReversiApp : RetroAppBehaviour
    {
        public override void BuildContent(AppContext ctx) => AppView.Attach<ReversiView>(ctx);

        public override void ApplyDefaults()
        {
            app.appId = "reversi";
            app.title = "Reversi";
            app.builtinIcon = BuiltinIcon.Board;
            app.category = "Games";
            app.windowSize = new Vector2(340, 400);
            app.minWindowSize = new Vector2(320, 360);
        }
    }
}
