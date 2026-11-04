using UnityEngine;
using Deepwave.RetroOS.Core.FileSystem;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Apps.Components
{
    [AddComponentMenu("")]
    public class RetroCdPlayerApp : RetroAppBehaviour
    {
        public override bool CanOpen(VfsFile file) => file.Kind == FileKind.Audio;

        public override void BuildContent(AppContext ctx) => AppView.Attach<CdPlayerView>(ctx);

        public override void ApplyDefaults()
        {
            app.appId = "cdplayer";
            app.title = "CD Player";
            app.builtinIcon = BuiltinIcon.Disc;
            app.category = "Accessories";
            // Tall enough for the track list under the transport.
            app.windowSize = new Vector2(400, 420);
            app.minWindowSize = new Vector2(380, 320);
            app.showOnDesktop = false;
        }
    }
}
