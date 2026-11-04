using UnityEngine;
using Deepwave.RetroOS.Core.FileSystem;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Apps.Components
{
    // Browses the whole machine: the profile's own files, the real folders
    // mounted from disk, the LAN and any inserted disc.
    [AddComponentMenu("")]
    public class RetroFileExplorerApp : RetroAppBehaviour
    {
        public override void BuildContent(AppContext ctx) => AppView.Attach<FileExplorerView>(ctx);

        public override void ApplyDefaults()
        {
            app.appId = "files";
            app.title = "My Computer";
            app.builtinIcon = BuiltinIcon.Explorer;
            app.windowSize = new Vector2(680, 480);
            app.minWindowSize = new Vector2(420, 300);
        }
    }
}
