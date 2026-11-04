using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Deepwave.RetroOS.UI;
using Deepwave.RetroOS.UI.Icons;

namespace Deepwave.RetroOS.EditorTools
{
    // Shared look for every RetroOS inspector and window: a title strip with the
    // program's icon, grouped sections, the built-in icon picker. Everything lays
    // out through GUILayout - a hand-computed rect has to guess the width and gets it wrong.
    public static class RetroEditorStyles
    {
        static GUIStyle s_header, s_subHeader, s_card, s_pill, s_note, s_titleBar, s_sectionHeader;

        public static readonly Color AccentSoft = new(0.13f, 0.35f, 0.62f, 0.16f);

        // Preview of a built-in icon. Shared and cached per icon and size by
        // RetroIcons - never destroy what comes back.
        public static Texture2D Icon(BuiltinIcon icon, int size = 32) =>
            RetroIcons.GetTexture(icon, size);

        public static GUIStyle Header => s_header ??= new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            wordWrap = true,
            alignment = TextAnchor.UpperLeft,
            margin = new RectOffset(0, 0, 0, 0)
        };

        public static GUIStyle SubHeader => s_subHeader ??= new GUIStyle(EditorStyles.miniLabel)
        {
            wordWrap = true,
            fontSize = 11,
            margin = new RectOffset(0, 0, 0, 0)
        };

        public static GUIStyle Card => s_card ??= new GUIStyle(EditorStyles.helpBox)
        {
            padding = new RectOffset(8, 8, 6, 8),
            margin = new RectOffset(0, 0, 4, 4)
        };

        public static GUIStyle Pill => s_pill ??= new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 10
        };

        public static GUIStyle Note => s_note ??= new GUIStyle(EditorStyles.label)
        {
            wordWrap = true,
            fontSize = 11,
            richText = true,
            margin = new RectOffset(0, 0, 2, 2)
        };

        /// <summary>The title row of a section: a foldout that still reads as a heading.</summary>
        public static GUIStyle SectionHeader => s_sectionHeader ??= new GUIStyle(EditorStyles.foldout)
        {
            fontStyle = FontStyle.Bold,
            margin = new RectOffset(0, 0, 0, 0)
        };

        static GUIStyle TitleBar => s_titleBar ??= new GUIStyle
        {
            padding = new RectOffset(6, 6, 6, 6),
            margin = new RectOffset(0, 0, 0, 4)
        };

        /// <summary>The banner every RetroOS inspector opens with: the thing's icon, its name, and one line about what it is doing.</summary>
        public static void Title(string title, string subtitle, BuiltinIcon icon)
        {
            // The tint comes from the rect the group reports, so the band is exactly as tall as the wrapped subtitle.
            var band = EditorGUILayout.BeginHorizontal(TitleBar);
            if (Event.current.type == EventType.Repaint && band.height > 1f)
                EditorGUI.DrawRect(band, AccentSoft);

            GUILayout.Label(Icon(icon), GUILayout.Width(32), GUILayout.Height(32));
            GUILayout.Space(6);

            EditorGUILayout.BeginVertical();
            GUILayout.Label(title, Header);
            if (!string.IsNullOrEmpty(subtitle))
                GUILayout.Label(subtitle, SubHeader);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>A titled block of fields.</summary>
        public static void Section(string title, Action body)
        {
            EditorGUILayout.BeginVertical(Card);
            try
            {
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                body();
            }
            finally
            {
                // A field renamed out from under FindProperty throws in body(); close the group here or Unity reports a mismatched layout group forever after.
                EditorGUILayout.EndVertical();
            }
        }

        /// <summary>A titled block that can be folded away, remembered per key so it stays as the user left it.</summary>
        public static void Section(string key, string title, Action body, bool openByDefault = true)
        {
            EditorGUILayout.BeginVertical(Card);
            try
            {
                if (Foldout(key, title, openByDefault))
                    body();
            }
            finally
            {
                EditorGUILayout.EndVertical();
            }
        }

        // Unity's foldout style reserves no room for its triangle, so in a padded card it lands on the border. Inset the header row so it draws inside the padding.
        const float ArrowWidth = 14f;

        public static bool Foldout(string key, string title, bool defaultOpen = true)
        {
            var stateKey = "RetroOS.Fold." + key;
            var open = EditorPrefs.GetBool(stateKey, defaultOpen);

            // A rect this method owns: EditorGUILayout.Foldout's is positioned from the window and indent level, not the box it sits in.
            var rect = GUILayoutUtility.GetRect(
                GUIContent.none,
                SectionHeader,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(EditorGUIUtility.singleLineHeight));

            rect.xMin += ArrowWidth;

            var next = EditorGUI.Foldout(rect, open, title, true, SectionHeader);
            if (next != open) EditorPrefs.SetBool(stateKey, next);
            return next;
        }

        /// <summary>A paragraph of explanation. Wraps, so it reserves the height it actually needs instead of one line.</summary>
        public static void Hint(string message)
        {
            // GUILayout.Label, not LabelField: LabelField gives a wrapping style a single-line rect and it spills over what follows.
            GUILayout.Label(message, Note);
        }

        /// <summary>Draws a nested settings block's own fields, without the grey disclosure triangle Unity would wrap them in.</summary>
        public static void Inline(SerializedProperty property)
        {
            if (property == null)
                return;

            // hasVisibleChildren is false for a plain value, and drawing its
            // children would then draw nothing at all.
            if (!property.hasVisibleChildren)
            {
                EditorGUILayout.PropertyField(property, true);
                return;
            }

            var end = property.GetEndProperty();
            var child = property.Copy();

            if (!child.NextVisible(true))
                return;

            do
            {
                if (SerializedProperty.EqualContents(child, end))
                    return;

                EditorGUILayout.PropertyField(child, true);
            } while (child.NextVisible(false));
        }

        // --- lists ----------------------------------------------------------

        static readonly Dictionary<string, ReorderableList> Lists = new();

        /// <summary>A list with drag handles and no second layer of disclosure chrome.</summary>
        public static void List(SerializedProperty property, string title = null)
        {
            if (property == null || !property.isArray)
                return;

            var key = property.serializedObject.targetObject.GetInstanceID() + ":" + property.propertyPath;

            if (!Lists.TryGetValue(key, out var list) || list.serializedProperty == null ||
                list.serializedProperty.serializedObject != property.serializedObject)
            {
                list = new ReorderableList(property.serializedObject, property, true, true, true, true);
                var heading = title ?? property.displayName;

                list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, heading);

                list.elementHeightCallback = index =>
                {
                    var element = list.serializedProperty.GetArrayElementAtIndex(index);
                    return EditorGUI.GetPropertyHeight(element, true) + 4f;
                };

                list.drawElementCallback = (rect, index, _, _) =>
                {
                    var element = list.serializedProperty.GetArrayElementAtIndex(index);
                    rect.y += 2f;
                    rect.height = EditorGUI.GetPropertyHeight(element, true);
                    EditorGUI.PropertyField(rect, element, GUIContent.none, true);
                };

                Lists[key] = list;
            }

            list.DoLayoutList();
        }

        // --- icon picker ------------------------------------------------------

        // The width the grid was last drawn at - the only honest source for how many rows it needs.
        static float s_gridWidth = 200f;

        /// <summary>Icon picker: a grid of every built-in icon, drawn at real size and wrapped to whatever width it is given.</summary>
        public static BuiltinIcon IconGrid(BuiltinIcon selected)
        {
            const int cell = 40;

            var icons = RetroIcons.All;
            var columns = Mathf.Max(1, Mathf.FloorToInt(s_gridWidth / cell));
            var rows = Mathf.CeilToInt(icons.Length / (float)columns);

            var area = GUILayoutUtility.GetRect(cell, rows * cell, GUILayout.ExpandWidth(true));

            if (Event.current.type == EventType.Repaint && area.width > 1f &&
                !Mathf.Approximately(area.width, s_gridWidth))
            {
                s_gridWidth = area.width;
                // The row count was computed from the old width, so the grid is
                // laid out again before anybody sees it clipped.
                EditorWindow.focusedWindow?.Repaint();
            }

            var result = selected;

            for (var i = 0; i < icons.Length; i++)
            {
                var x = area.x + i % columns * cell;
                var y = area.y + i / columns * cell;
                var rect = new Rect(x, y, cell - 2, cell - 2);

                if (icons[i] == selected) EditorGUI.DrawRect(rect, AccentSoft);
                GUI.DrawTexture(new Rect(rect.x + 4, rect.y + 4, cell - 10, cell - 10),
                    Icon(icons[i]), ScaleMode.ScaleToFit);

                if (GUI.Button(rect, new GUIContent("", icons[i].ToString()), GUIStyle.none))
                    result = icons[i];
            }

            return result;
        }
    }

    /// <summary>A short-lived cache for anything an inspector reads from outside itself.</summary>
    public static class RetroEditorCache
    {
        const double DefaultSeconds = 1.0;

        static readonly Dictionary<string, (double at, object value)> Entries = new();

        public static T Get<T>(string key, Func<T> build, double seconds = DefaultSeconds)
        {
            var now = EditorApplication.timeSinceStartup;

            if (Entries.TryGetValue(key, out var entry) && now - entry.at < seconds && entry.value is T cached)
                return cached;

            var value = build();
            Entries[key] = (now, value);
            return value;
        }

        /// <summary>Drops a cached answer, for a tool that just changed it.</summary>
        public static void Forget(string key) => Entries.Remove(key);
    }
}
