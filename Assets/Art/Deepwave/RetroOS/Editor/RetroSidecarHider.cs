using UnityEditor;
using UnityEngine;
using Deepwave.RetroOS.Core.Storage;

namespace Deepwave.RetroOS.EditorTools
{
    // `.rmeta` sidecars must be real visible files (Unity strips dotfiles and `.meta` from a
    // StreamingAssets build, and the game reads these at runtime). Not content, so the Project
    // window dims the row and RetroFileInspector leaves them to Unity's default inspector.
    [InitializeOnLoad]
    static class RetroSidecarHider
    {
        static RetroSidecarHider()
        {
            EditorApplication.projectWindowItemOnGUI -= Draw;
            EditorApplication.projectWindowItemOnGUI += Draw;
        }

        static GUIStyle s_tag;

        static void Draw(string guid, Rect rect)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) ||
                !path.EndsWith(RetroMeta.Extension, System.StringComparison.OrdinalIgnoreCase))
                return;

            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.10f));

            s_tag ??= new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.5f, 0.5f, 0.5f, 0.7f) }
            };

            // The tag width is measured, not a fixed 58px, which overlapped the file name in a narrow panel or the grid view.
            var content = new GUIContent("sidecar");
            var width = s_tag.CalcSize(content).x;

            // Below this the row is an icon in grid view, or a name with
            // nothing to spare: the dimming alone has to say it.
            if (rect.width < width * 3f)
                return;

            GUI.Label(
                new Rect(rect.xMax - width - 4f, rect.y, width, rect.height),
                content,
                s_tag);
        }
    }
}
