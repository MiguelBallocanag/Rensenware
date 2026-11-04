using UnityEngine;
using Deepwave.RetroOS.Core.FileSystem;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Apps.Components
{
    // Reads any text file, and saves it back when the file came off disk.
    [AddComponentMenu("")]
    public class RetroNotepadApp : RetroAppBehaviour
    {
        public override bool CanOpen(VfsFile file) => file.Kind == FileKind.Text;

        public override string WindowTitle(VfsFile file) =>
            file == null ? Title : $"{file.Name} - {Title}";

        public override void BuildContent(AppContext ctx) => AppView.Attach<TextViewerView>(ctx);

        public override void ApplyDefaults()
        {
            app.appId = "notepad";
            app.title = "Notepad";
            app.builtinIcon = BuiltinIcon.Note;
            app.category = "Accessories";
            app.windowSize = new Vector2(560, 440);
            app.minWindowSize = new Vector2(360, 260);
            app.singleInstance = false;
            app.showOnDesktop = false;
        }
    }
}
