using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Deepwave.RetroOS.Core.Network;
using Deepwave.RetroOS.Core.Storage;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.EditorTools
{
    // Inspector for the world: where files are kept, what year it is, whether machines see each other - all game facts, not per-computer.
    [CustomEditor(typeof(RetroWorld))]
    public class RetroWorldEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Counted through the cache: a scene-wide search per repaint stutters on a large scene.
            var machines = RetroEditorCache.Get(
                "world.machines",
                () =>
                    FindObjectsByType<RetroOsBootstrap>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None
                    ).Length
            );

            RetroEditorStyles.Title(
                "World",
                machines == 1 ? "1 machine" : $"{machines} machines",
                BuiltinIcon.Folder
            );

            RetroEditorStyles.Section("world.files", "Files", () =>
            {
                RetroEditorStyles.Inline(serializedObject.FindProperty("storage"));
                RetroEditorStyles.Hint(
                    "One folder holds every machine and every web site, so where it is "
                        + "cannot be a machine's own business. Set it here and every computer "
                        + "in the scene reads from it."
                );
                DrawOnDisk();
                DrawWebContent();
            });

            RetroEditorStyles.Section("world.clock", "Clock", () =>
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("systemDate"), true);
                RetroEditorStyles.Hint(
                    "The date every machine here starts from - they are in the same room. "
                        + "Empty runs on the player's own clock."
                );
            });

            RetroEditorStyles.Section("world.network", "Network", () =>
            {
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("showNetworkNeighborhood"),
                    true
                );
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("neighborhoodLabel"),
                    true
                );
                RetroEditorStyles.Hint(
                    "Whether the machines in this world list each other inside This "
                        + "Computer, and what that branch is called."
                );
            });

            serializedObject.ApplyModifiedProperties();
        }

        static void DrawOnDisk()
        {
            var root = RetroStorage.Root;

            EditorGUILayout.Space(4);
            EditorGUILayout.SelectableLabel(
                root,
                EditorStyles.textField,
                GUILayout.Height(18)
            );

            // One cached read for both listings: this runs on every repaint
            // otherwise, and it hits the disk.
            var folders = RetroEditorCache.Get(
                "world.folders",
                () => (machines: RetroStorage.MachineNames(), sites: RetroStorage.SiteNames())
            );

            RetroEditorStyles.Hint(
                $"{folders.machines.Length} machine folder(s): {Join(folders.machines)}"
            );
            RetroEditorStyles.Hint($"{folders.sites.Length} site folder(s): {Join(folders.sites)}");

            using (new EditorGUI.DisabledScope(!Directory.Exists(root)))
            {
                if (GUILayout.Button("Open folder"))
                    EditorUtility.RevealInFinder(root);
            }
        }

        // WebGL and Android ship the OS's files as a baked pack rather than the
        // folder; every other target ignores it. One line so a web build's
        // missing content is visible from the world, not only at build time.
        static void DrawWebContent()
        {
            if (!RetroContentBake.Needs(EditorUserBuildSettings.activeBuildTarget)) return;

            EditorGUILayout.Space(2);

            // File.Exists per repaint is cheap; staleness (hashing 6 MB) is left
            // to Setup and the build check.
            if (RetroContentBake.Baked)
                EditorGUILayout.HelpBox(
                    "Web/Android content is baked into the build and kept current for you.",
                    MessageType.Info);
            else
                EditorGUILayout.HelpBox(
                    "Web/Android content is not baked yet. It bakes itself before a build "
                        + "needs it, or use Deepwave > RetroOS > Setup to do it now.",
                    MessageType.Warning);
        }

        static string Join(string[] names) =>
            names.Length == 0 ? "none yet" : string.Join(", ", names.Take(6));
    }

    // The internet: one switch for everything outside the building.
    [CustomEditor(typeof(RetroInternet))]
    public class RetroInternetEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var internet = (RetroInternet)target;
            serializedObject.Update();

            var hosts = internet.GetComponentsInChildren<RetroInternetHost>(true).Length;

            RetroEditorStyles.Title(
                internet.Carrier,
                internet.Online ? $"Up · {hosts} site(s) here" : "Line down",
                BuiltinIcon.Globe
            );

            DrawDefaultInspector();

            RetroEditorStyles.Hint(
                "Switch the line off and every site is unreachable at once, while the LAN "
                    + "carries on - which is what a cut cable looks like from a desk. One "
                    + "site going quiet is the host's own On switch, not this one."
            );

            if (Application.isPlaying)
            {
                var loose = RetroNetwork.Hosts.Count - hosts;
                if (loose > 0)
                    EditorGUILayout.HelpBox(
                        $"{loose} host(s) are registered from elsewhere in the scene. They "
                            + "still answer; parenting them here is only how you keep them "
                            + "together.",
                        MessageType.None
                    );
            }

            EditorGUILayout.Space(4);

            var known = Application.isPlaying
                ? RetroNetwork.KnownDomains().Take(8).ToArray()
                : RetroEditorCache.Get("internet.sites", RetroStorage.SiteNames).Take(8).ToArray();

            RetroEditorStyles.Hint(
                known.Length == 0 ? "No sites yet." : "Answers to: " + string.Join(", ", known)
            );

            serializedObject.ApplyModifiedProperties();
        }
    }
}
