using System.Linq;
using UnityEditor;
using UnityEngine;
using Deepwave.RetroOS.Display;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.EditorTools
{
    // Inspector for the OS itself: name, look and sound, shutdown behaviour, story rules,
    // and a live report while running. Not here: the machine name/accounts/BIOS (RetroMachine),
    // the picture (RetroMonitor), the installed programs (the hierarchy under this object).
    [CustomEditor(typeof(RetroOsBootstrap))]
    [CanEditMultipleObjects]
    public class RetroOsBootstrapEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var bootstrap = (RetroOsBootstrap)target;
            serializedObject.Update();

            RetroEditorStyles.Title(
                "RetroOS",
                Subtitle(bootstrap),
                BuiltinIcon.Monitor
            );

            DrawMissingMonitor();

            RetroEditorStyles.Section("os.identity", "Identity", () =>
            {
                Field("osName");
                RetroEditorStyles.Hint(
                    "The name of the <b>system</b>, for the Start banner and the About box. "
                        + "The <b>computer</b> is named on its Machine component, and the two "
                        + "are different things: WORKSTATION-04 running RetroOS 95."
                );
            });

            RetroEditorStyles.Section("os.look", "Look", () =>
            {
                Field("theme");
                Field("wallpaper");
                RetroEditorStyles.Hint(
                    "The wallpaper here is the one this machine ships with. Once the player "
                        + "picks their own from the Control Panel, theirs is what comes back "
                        + "the next time it boots."
                );
            });

            RetroEditorStyles.Section("os.sound", "Sound", () =>
                RetroEditorStyles.Inline(serializedObject.FindProperty("sounds")), false);

            RetroEditorStyles.Section("os.power", "Power", () =>
            {
                Field("startPoweredOn");
                Field("startFocused");
                RetroEditorStyles.Hint(
                    "A machine that starts off is built but not booted: dark glass until "
                        + "something calls <b>PowerOn()</b> - a switch on the prop, or the "
                        + "player pressing the interact key at the desk. <b>os.boot</b> waits "
                        + "for that, so a rule that greets them at start-up fires when they "
                        + "are there to read it."
                );
                RetroEditorStyles.Hint(
                    "<b>Shut Down</b> takes the in-fiction machine down to a dead monitor - it "
                        + "never quits the player's game. Listen for <b>os.shutdown</b> to drive "
                        + "your own ending, and call <b>PowerOn()</b> to boot it back up."
                );
            });

            RetroEditorStyles.Section("os.rules", "Story rules", () =>
            {
                RetroEditorStyles.List(serializedObject.FindProperty("rules"), "Rules");
                RetroEditorStyles.Hint(
                    "No-code reactions: when an OS event happens - a file opened, a login, "
                        + "a command typed - run actions. Reveal a file, deliver mail, set a "
                        + "flag, crash the machine."
                );
            });

            if (Application.isPlaying && !serializedObject.isEditingMultipleObjects)
                DrawRunning(bootstrap);

            serializedObject.ApplyModifiedProperties();
        }

        void Field(string name)
        {
            var property = serializedObject.FindProperty(name);
            if (property != null)
                EditorGUILayout.PropertyField(property, true);
        }

        // A computer with no screen - an object built before RequireComponent added one. It boots on a default screen and warns.
        void DrawMissingMonitor()
        {
            var missing = targets
                .OfType<RetroOsBootstrap>()
                .Where(bootstrap => bootstrap && !bootstrap.GetComponent<RetroMonitor>())
                .ToList();

            if (missing.Count == 0)
                return;

            EditorGUILayout.HelpBox(
                "No Monitor on this object, so nothing says how big this machine's picture "
                    + "is, which RenderTexture it renders into or how the player points at "
                    + "it. It boots on a default screen and fills the game view.",
                MessageType.Warning
            );

            if (!GUILayout.Button("Add Monitor"))
                return;

            foreach (var bootstrap in missing)
                Undo.AddComponent<RetroMonitor>(bootstrap.gameObject);
        }

        static string Subtitle(RetroOsBootstrap bootstrap)
        {
            var machine = bootstrap.Machine;
            return machine
                ? $"{bootstrap.OsName} on {machine.MachineName}"
                : bootstrap.OsName;
        }

        static void DrawRunning(RetroOsBootstrap bootstrap)
        {
            var context = bootstrap.Context;
            if (context == null)
                return;

            RetroEditorStyles.Section("os.running", "Running", () =>
            {
                EditorGUILayout.LabelField("Power", bootstrap.PoweredOn ? "on" : "off");
                EditorGUILayout.LabelField(
                    "Signed in",
                    context.Auth.Current?.DisplayName ?? "(nobody)"
                );
                EditorGUILayout.LabelField(
                    "Clock",
                    context.Clock.Now.ToString("yyyy-MM-dd HH:mm:ss")
                );
                EditorGUILayout.LabelField("Programs", context.Apps.All.Count.ToString());
                EditorGUILayout.LabelField("Unread mail", context.Mail.UnreadCount.ToString());
                EditorGUILayout.LabelField("Network", context.Net.Online ? "online" : "offline");

                var flags = context.Flags.All.ToList();
                EditorGUILayout.LabelField(
                    "Flags",
                    flags.Count == 0 ? "-" : string.Join(", ", flags)
                );
            });
        }
    }
}
