using UnityEditor;
using UnityEngine;
using Deepwave.RetroOS.Data;
using Deepwave.RetroOS.Events;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.EditorTools
{
    // Compact one-line summary rows for the list types a machine is built from; open the fields only when asked.

    /// <summary>One pass over an element's fields, used for both drawing and measuring.</summary>
    static class DrawerRows
    {
        public const float Gap = 2f;

        public static float HeaderHeight => EditorGUIUtility.singleLineHeight;

        /// <summary>Runs the body of an element. Pass a rect to draw it, or <c>null</c> to only measure. Returns the height the body needs.</summary>
        public static float Body(SerializedProperty property, string[] fields, Rect? area)
        {
            var y = area?.y ?? 0f;
            var total = 0f;

            foreach (var name in fields)
            {
                // A field renamed out from under the drawer is skipped rather
                // than thrown on, so a rename never costs the whole inspector.
                var prop = property.FindPropertyRelative(name);
                if (prop == null)
                    continue;

                var height = EditorGUI.GetPropertyHeight(prop, true);

                if (area is { } rect)
                    EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, height), prop, true);

                y += height + Gap;
                total += height + Gap;
            }

            return total;
        }

        /// <summary>Draws the summary line and, when it is open, the fields behind it.</summary>
        public static void Draw(
            Rect position,
            SerializedProperty property,
            GUIContent label,
            string caption,
            BuiltinIcon icon,
            string[] fields
        )
        {
            // Without this the row loses the blue prefab-override bar, the
            // right-click Revert menu and its drag handle in a list.
            EditorGUI.BeginProperty(position, label, property);
            try
            {
                var header = new Rect(position.x, position.y, position.width, HeaderHeight);

                property.isExpanded = EditorGUI.Foldout(
                    header,
                    property.isExpanded,
                    new GUIContent(caption, RetroEditorStyles.Icon(icon)),
                    true
                );

                if (!property.isExpanded)
                    return;

                EditorGUI.indentLevel++;
                try
                {
                    var body = new Rect(
                        position.x,
                        header.yMax + Gap,
                        position.width,
                        position.height
                    );
                    Body(property, fields, body);
                }
                finally
                {
                    EditorGUI.indentLevel--;
                }
            }
            finally
            {
                EditorGUI.EndProperty();
            }
        }

        /// <summary>The height the same row will draw at.</summary>
        public static float Height(SerializedProperty property, string[] fields) =>
            property.isExpanded
                ? HeaderHeight + Gap + Body(property, fields, null)
                : HeaderHeight;

        /// <summary>Reads a string field, for building a summary line.</summary>
        public static string Text(SerializedProperty property, string name)
        {
            var prop = property.FindPropertyRelative(name);
            return prop == null ? "" : prop.stringValue;
        }
    }

    [CustomPropertyDrawer(typeof(UserEntry))]
    public class UserEntryDrawer : PropertyDrawer
    {
        static readonly string[] Fields = { "username", "password", "displayName", "avatar" };

        static string Caption(SerializedProperty property)
        {
            var username = DrawerRows.Text(property, "username");
            var display = DrawerRows.Text(property, "displayName");

            if (string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(display))
                return "(new account)";

            var caption = string.IsNullOrWhiteSpace(display) ? username : display;
            if (!string.IsNullOrWhiteSpace(display) && !string.IsNullOrWhiteSpace(username))
                caption += $"  ({username})";

            if (string.IsNullOrEmpty(DrawerRows.Text(property, "password")))
                caption += "   [no password]";

            return caption;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) =>
            DrawerRows.Draw(position, property, label, Caption(property), BuiltinIcon.Key, Fields);

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            DrawerRows.Height(property, Fields);
    }

    [CustomPropertyDrawer(typeof(MailEntry))]
    public class MailEntryDrawer : PropertyDrawer
    {
        static readonly string[] Fields =
        {
            "id",
            "from",
            "subject",
            "body",
            "unread",
            "delayed",
            "attachments",
        };

        static string Caption(SerializedProperty property)
        {
            var subject = DrawerRows.Text(property, "subject");
            var from = DrawerRows.Text(property, "from");
            var delayed = property.FindPropertyRelative("delayed");

            var caption = string.IsNullOrEmpty(subject) ? "(no subject)" : subject;
            if (!string.IsNullOrEmpty(from))
                caption = from + " - " + caption;
            if (delayed != null && delayed.boolValue)
                caption += "   [held back]";

            return caption;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) =>
            DrawerRows.Draw(position, property, label, Caption(property), BuiltinIcon.Mail, Fields);

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            DrawerRows.Height(property, Fields);
    }

    [CustomPropertyDrawer(typeof(OsRule))]
    public class OsRuleDrawer : PropertyDrawer
    {
        static readonly string[] Fields =
        {
            "note",
            "eventId",
            "argument",
            "requiredFlag",
            "once",
            "actions",
        };

        static string Caption(SerializedProperty property)
        {
            var note = DrawerRows.Text(property, "note");
            if (!string.IsNullOrEmpty(note))
                return note;

            var eventId = DrawerRows.Text(property, "eventId");
            if (string.IsNullOrEmpty(eventId))
                return "(new rule)";

            var actions = property.FindPropertyRelative("actions");
            return $"when {eventId} -> {(actions == null ? 0 : actions.arraySize)} action(s)";
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) =>
            DrawerRows.Draw(position, property, label, Caption(property), BuiltinIcon.Flag, Fields);

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            DrawerRows.Height(property, Fields);
    }

    [CustomPropertyDrawer(typeof(CameraEntry))]
    public class CameraEntryDrawer : PropertyDrawer
    {
        static readonly string[] Fields =
        {
            "label",
            "deviceId",
            "recording",
            "staticImage",
            "allowDvr",
        };

        static string Caption(SerializedProperty property)
        {
            var channel = DrawerRows.Text(property, "label");
            var device = DrawerRows.Text(property, "deviceId");

            var caption = string.IsNullOrEmpty(channel) ? "(channel)" : channel;
            caption += string.IsNullOrEmpty(device) ? "   [no live camera]" : "   live: " + device;

            return caption;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) =>
            DrawerRows.Draw(
                position,
                property,
                label,
                Caption(property),
                BuiltinIcon.Camera,
                Fields
            );

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            DrawerRows.Height(property, Fields);
    }
}
