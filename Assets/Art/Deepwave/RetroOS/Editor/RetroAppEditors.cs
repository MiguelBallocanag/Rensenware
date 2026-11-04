using UnityEditor;
using UnityEngine;
using Deepwave.RetroOS.Apps;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.EditorTools
{
    // Inspector for a program: a live icon preview and picker, window/placement settings, the UnityEvent hooks, plus any subclass fields.
    [CustomEditor(typeof(RetroAppBehaviour), true)]
    [CanEditMultipleObjects]
    public class RetroAppBehaviourEditor : Editor
    {
        static readonly string[] Handled = { "m_Script", "app", "events" };

        bool _pickingIcon;

        public override void OnInspectorGUI()
        {
            var behaviour = (RetroAppBehaviour)target;
            var settings = behaviour.Settings;
            var many = serializedObject.isEditingMultipleObjects;
            serializedObject.Update();

            // With a multiple selection the banner says how many, not one program's name over all.
            RetroEditorStyles.Title(
                many ? $"{targets.Length} programs" : behaviour.Title,
                many
                    ? "Editing several programs. Fields showing a dash differ between them."
                    : $"id: {behaviour.Id}   ·   "
                        + $"{(settings.showOnDesktop ? "on the desktop" : "not on the desktop")}, "
                        + $"{(settings.showInStartMenu ? "in the Start menu" : "hidden from Start")}"
                        + (settings.launchOnBoot ? ", opens on boot" : ""),
                many ? BuiltinIcon.Tools : settings.builtinIcon);

            DrawInstallWarning();

            var app = serializedObject.FindProperty("app");
            DrawIdentity(app);
            DrawIcon(settings, app);
            DrawWindow(app);
            DrawPlacement(app);
            DrawEvents();
            DrawAppSpecific();

            serializedObject.ApplyModifiedProperties();
        }

        // A program only exists if the bootstrap can find it. Every selected one is checked, not just the first.
        void DrawInstallWarning()
        {
            var stranded = 0;
            foreach (var each in targets)
                if (each is RetroAppBehaviour app && !app.GetComponentInParent<RetroOsBootstrap>())
                    stranded++;

            if (stranded == 0)
                return;

            var subject = targets.Length == 1
                ? "This program is"
                : stranded == targets.Length
                    ? "These programs are"
                    : $"{stranded} of the selected programs are";

            EditorGUILayout.HelpBox(
                $"{subject} not under a RetroOsBootstrap, so they will not be installed. "
                    + "Move the object under the computer that should run it.",
                MessageType.Warning);
        }

        void DrawIdentity(SerializedProperty app) => RetroEditorStyles.Section("app.identity", "Identity", () =>
        {
            EditorGUILayout.PropertyField(app.FindPropertyRelative("appId"));
            EditorGUILayout.PropertyField(app.FindPropertyRelative("title"));
            RetroEditorStyles.Hint("The id is what rules and code use: <b>OpenApp</b>, " +
                                   "<b>app.opened</b>, and this program's own events.");
        });

        void DrawIcon(AppSettings settings, SerializedProperty app) =>
            RetroEditorStyles.Section("app.icon", "Icon", () =>
            {
                EditorGUILayout.BeginHorizontal();

                var preview = GUILayoutUtility.GetRect(48, 48, GUILayout.Width(48));
                GUI.DrawTexture(preview, RetroEditorStyles.Icon(settings.builtinIcon, 48),
                    ScaleMode.ScaleToFit);

                EditorGUILayout.BeginVertical();
                EditorGUILayout.PropertyField(app.FindPropertyRelative("icon"),
                    new GUIContent("Custom sprite"));
                EditorGUILayout.PropertyField(app.FindPropertyRelative("builtinIcon"));
                if (GUILayout.Button(_pickingIcon ? "Close picker" : "Pick from the built-in set"))
                    _pickingIcon = !_pickingIcon;
                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();

                if (!_pickingIcon) return;

                var builtin = app.FindPropertyRelative("builtinIcon");
                var current = (BuiltinIcon)builtin.enumValueIndex;
                // The grid measures itself from the rect the layout hands it,
                // so it stays inside this card however narrow the inspector is.
                var picked = RetroEditorStyles.IconGrid(current);
                if (picked != current) builtin.enumValueIndex = (int)picked;
            });

        void DrawWindow(SerializedProperty app) => RetroEditorStyles.Section("app.window", "Window", () =>
        {
            EditorGUILayout.PropertyField(app.FindPropertyRelative("windowSize"));
            EditorGUILayout.PropertyField(app.FindPropertyRelative("minWindowSize"));
            EditorGUILayout.PropertyField(app.FindPropertyRelative("resizable"));
            EditorGUILayout.PropertyField(app.FindPropertyRelative("startMaximized"));
            EditorGUILayout.PropertyField(app.FindPropertyRelative("singleInstance"));
        });

        void DrawPlacement(SerializedProperty app) => RetroEditorStyles.Section("app.placement", "Placement", () =>
        {
            EditorGUILayout.PropertyField(app.FindPropertyRelative("showOnDesktop"));
            EditorGUILayout.PropertyField(app.FindPropertyRelative("showInStartMenu"));
            EditorGUILayout.PropertyField(app.FindPropertyRelative("category"));
            EditorGUILayout.PropertyField(app.FindPropertyRelative("launchOnBoot"));
            EditorGUILayout.PropertyField(app.FindPropertyRelative("sortOrder"));
        });

        void DrawEvents() => RetroEditorStyles.Section("app.events", "Game events", () =>
        {
            RetroEditorStyles.Hint(
                "Wire these to anything in the scene. <b>On Action</b> fires for every " +
                "interaction inside the program, and the hooks below it match a single one " +
                "by name (<b>won</b>, <b>saved</b>, <b>visited</b>, <b>page</b>).");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("events"), true);
        });

        // Whatever the subclass adds - cameras, mail, sites, board size.
        void DrawAppSpecific()
        {
            // Gathered before drawing, so it goes through the same Section (which closes its card in a finally); a card opened mid-loop leaked on a throw.
            var extra = new System.Collections.Generic.List<SerializedProperty>();
            var iterator = serializedObject.GetIterator();
            var enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (System.Array.IndexOf(Handled, iterator.name) >= 0) continue;
                extra.Add(iterator.Copy());
            }

            if (extra.Count == 0) return;

            RetroEditorStyles.Section("app.specific", "This program", () =>
            {
                foreach (var property in extra)
                {
                    // A program's own lists go through the list helper: each element already draws its own header, so Unity's array foldout is a redundant arrow.
                    if (property.isArray && property.propertyType != SerializedPropertyType.String)
                        RetroEditorStyles.List(property);
                    else
                        EditorGUILayout.PropertyField(property, true);
                }
            });
        }
    }
}
