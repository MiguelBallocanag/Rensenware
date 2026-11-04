using UnityEngine;
using Deepwave.RetroOS.Apps.Games;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Apps.Components
{
    // Mine-clearing grid puzzle. Public-domain mechanic, original code.
    [AddComponentMenu("")]
    public class RetroMinesApp : RetroAppBehaviour, IMineFieldSource
    {
        [Header("Field")]
        [Range(5, 30)] public int columns = 9;
        [Range(5, 24)] public int rows = 9;
        [Range(1, 200)] public int mines = 10;

        public int Columns => columns;
        public int Rows => rows;
        public int Mines => mines;

        public override void BuildContent(AppContext ctx) => AppView.Attach<MinesView>(ctx);

        public override void ApplyDefaults()
        {
            app.appId = "mines";
            app.title = "Mines";
            app.builtinIcon = BuiltinIcon.Bomb;
            app.category = "Games";
            app.windowSize = new Vector2(320, 380);
            app.minWindowSize = new Vector2(260, 300);
        }
    }
}
