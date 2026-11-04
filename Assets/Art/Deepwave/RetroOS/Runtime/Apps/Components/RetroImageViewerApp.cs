using UnityEngine;
using Deepwave.RetroOS.Core.FileSystem;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Apps.Components
{
    [AddComponentMenu("")]
    public class RetroImageViewerApp : RetroAppBehaviour
    {
        public override bool CanOpen(VfsFile file) => file.Kind == FileKind.Image;

        public override string WindowTitle(VfsFile file) =>
            file == null ? Title : $"{file.Name} - {Title}";

        public override void BuildContent(AppContext ctx) => AppView.Attach<ImageViewerView>(ctx);

        public override void ApplyDefaults()
        {
            app.appId = "imageviewer";
            app.title = "Image Viewer";
            app.builtinIcon = BuiltinIcon.Picture;
            app.category = "Accessories";
            app.windowSize = new Vector2(520, 440);
            app.minWindowSize = new Vector2(340, 280);
            app.singleInstance = false;
            app.showOnDesktop = false;
        }
    }
}
