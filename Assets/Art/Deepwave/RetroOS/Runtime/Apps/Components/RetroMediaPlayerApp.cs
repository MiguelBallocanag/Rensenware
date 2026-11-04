using UnityEngine;
using Deepwave.RetroOS.Core.FileSystem;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Apps.Components
{
    [AddComponentMenu("")]
    public class RetroMediaPlayerApp : RetroAppBehaviour
    {
        public override bool CanOpen(VfsFile file) => file.Kind == FileKind.Video;

        public override string WindowTitle(VfsFile file) =>
            file == null ? Title : $"{file.Name} - {Title}";

        public override void BuildContent(AppContext ctx) => AppView.Attach<MediaPlayerView>(ctx);

        public override void ApplyDefaults()
        {
            app.appId = "mediaplayer";
            app.title = "Media Player";
            app.builtinIcon = BuiltinIcon.Movie;
            app.category = "Accessories";
            app.windowSize = new Vector2(560, 460);
            app.minWindowSize = new Vector2(460, 320);
            app.singleInstance = false;
            app.showOnDesktop = false;
        }
    }
}
