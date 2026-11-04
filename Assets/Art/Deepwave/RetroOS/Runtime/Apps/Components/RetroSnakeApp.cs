using UnityEngine;
using Deepwave.RetroOS.Apps.Games;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Apps.Components
{
    // Snake. The 1976 arcade mechanic, written from scratch here.
    [AddComponentMenu("")]
    public class RetroSnakeApp : RetroAppBehaviour
    {
        public override void BuildContent(AppContext ctx) => AppView.Attach<SnakeView>(ctx);

        public override void ApplyDefaults()
        {
            app.appId = "snake";
            app.title = "Snake";
            app.builtinIcon = BuiltinIcon.Joystick;
            app.category = "Games";
            app.windowSize = new Vector2(380, 400);
            app.minWindowSize = new Vector2(360, 360);
        }
    }
}
