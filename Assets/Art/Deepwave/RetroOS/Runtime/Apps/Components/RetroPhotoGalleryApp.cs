using UnityEngine;
using Deepwave.RetroOS.Core.FileSystem;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Apps.Components
{
    [AddComponentMenu("")]
    public class RetroPhotoGalleryApp : RetroAppBehaviour, IGallerySource
    {
        [Header("Pictures")]
        [Tooltip("Folder the gallery shows, as a path in the OS " +
                 "(e.g. /C:/Users/operator/Pictures). Leave empty to collect " +
                 "every picture on the machine.")]
        [SerializeField] string folderPath = "";

        public string FolderPath => folderPath;

        public override void BuildContent(AppContext ctx) => AppView.Attach<PhotoGalleryView>(ctx);

        public override void ApplyDefaults()
        {
            app.appId = "gallery";
            app.title = "Photo Gallery";
            app.builtinIcon = BuiltinIcon.Photos;
            app.category = "Accessories";
            app.windowSize = new Vector2(640, 480);
            app.minWindowSize = new Vector2(540, 340);
        }
    }
}
