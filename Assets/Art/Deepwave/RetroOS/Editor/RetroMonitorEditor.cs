using UnityEditor;
using UnityEngine;
using Deepwave.RetroOS.Display;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.EditorTools
{
    // Inspector for the screen: picture size and how the player points at it. The picture is
    // a RenderTexture the OS hands over - hanging it on a material is the game's business.
    // On the screen, not the OS, so two machines in a room can have different monitors.
    [CustomEditor(typeof(RetroMonitor))]
    [CanEditMultipleObjects]
    public class RetroMonitorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var monitor = (RetroMonitor)target;
            serializedObject.Update();

            RetroEditorStyles.Title("Monitor", Subtitle(monitor), BuiltinIcon.Monitor);

            RetroEditorStyles.Section("monitor.picture", "Picture", () =>
            {
                Field("resolution");
                Field("dynamicResolution");
                Field("pixelPerfect");
                Field("unfocusedFps");
                Field("textureAsset");
                DrawPicture(monitor);
                Field("sortingOrder");
                RetroEditorStyles.Hint(
                    "<b>Pixel Perfect</b> scales the interface by whole numbers only, so a "
                        + "32-pixel icon lands on 32, 64 or 96 pixels and a one-pixel border "
                        + "stays one pixel. A larger display then shows more desktop rather "
                        + "than a bigger one, the way a real machine did."
                );

                EditorGUILayout.Space(4);
                Field("workArea");
                RetroEditorStyles.Hint(
                    "<b>Work Area</b> is the part of the picture the OS fills, in fractions "
                        + "of it - the whole picture is 0,0,1,1. Everything lives inside it, "
                        + "windows included, so a bezel that eats the edges of the glass is "
                        + "something the desktop can be held clear of. It was a setting on "
                        + "the OS; the shape of a screen belongs to the screen."
                );
            });

            RetroEditorStyles.Section("monitor.pointer", "Pointer", () =>
            {
                Field("interaction");

                var interaction = serializedObject.FindProperty("interaction");
                var onScreen =
                    interaction != null
                    && interaction.enumValueIndex == (int)ScreenInteraction.OnScreenCursor;

                using (new EditorGUI.DisabledScope(!onScreen))
                {
                    Field("cursorSpeed");
                    Field("stickSpeed");
                }

                RetroEditorStyles.Hint(
                    "<b>On Screen Cursor</b> keeps the OS on its own picture and draws its "
                        + "own pointer, walked by the mouse, a gamepad stick or a dragging "
                        + "finger - so the monitor is always live and the real devices stay "
                        + "free for the game. It needs nothing of the surface: no collider, "
                        + "no camera. <b>Fullscreen Overlay</b> flips the OS to a pixel-exact "
                        + "overlay when the player sits down."
                );
            });

            serializedObject.ApplyModifiedProperties();
        }

        void Field(string name)
        {
            var property = serializedObject.FindProperty(name);
            if (property != null)
                EditorGUILayout.PropertyField(property, true);
        }

        static string Subtitle(RetroMonitor monitor)
        {
            var size = monitor.Resolution;
            var where = monitor.TextureAsset ? monitor.TextureAsset.name : "the game view";
            return $"{(int)size.x}x{(int)size.y} into {where}";
        }

        // The OS renders the picture and hands it over; assigning it to a material is the game's wiring - any shader, visible in edit mode.
        static void DrawPicture(RetroMonitor monitor)
        {
            if (monitor.TextureAsset)
            {
                RetroEditorStyles.Hint(
                    "The OS renders into this asset. Assign the same RenderTexture to your "
                        + "screen material (Base Map and Emission Map for a lit CRT) or to a "
                        + "RawImage - the desktop then shows up in edit mode too."
                );
                return;
            }

            EditorGUILayout.HelpBox(
                "No Render Texture asset, so the picture goes to a texture sized to the "
                    + "display and only a fullscreen machine can see it. For a screen in the "
                    + "world, create a RenderTexture, assign it here, and wire the same asset "
                    + "into the screen material.",
                MessageType.Info
            );
        }
    }
}
