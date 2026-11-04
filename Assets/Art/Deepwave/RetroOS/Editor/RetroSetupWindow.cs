using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Deepwave.RetroOS.Apps;
using Deepwave.RetroOS.Apps.Components;
using Deepwave.RetroOS.Core.Network;
using Deepwave.RetroOS.Core.Storage;
using Deepwave.RetroOS.Display;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.EditorTools
{
    // Takes a new project to a running OS: a computer in the open scene, ticked programs
    // installed as components under it, and the folders it reads files from. No asset - a computer is its object.
    public class RetroSetupWindow : EditorWindow
    {
        // Everything that can be installed, in the order it should appear.
        static readonly (Type type, string label, bool tool)[] Catalogue =
        {
            (typeof(RetroFileExplorerApp), "My Computer", true),
            (typeof(RetroNotepadApp), "Notepad", true),
            (typeof(RetroBrowserApp), "The Internet", true),
            (typeof(RetroMailApp), "Inbox", true),
            (typeof(RetroCctvApp), "CCTV", true),
            (typeof(RetroTerminalApp), "Terminal", true),
            (typeof(RetroPhotoGalleryApp), "Photo Gallery", true),
            (typeof(RetroControlPanelApp), "Control Panel", true),
            (typeof(RetroImageViewerApp), "Image Viewer", true),
            (typeof(RetroMediaPlayerApp), "Media Player", true),
            (typeof(RetroCdPlayerApp), "CD Player", true),
            (typeof(RetroCalculatorApp), "Calculator", true),
            (typeof(RetroReminderApp), "Reminders", true),
            (typeof(RetroChatApp), "Messenger", true),
            (typeof(RetroMinesApp), "Mines", false),
            (typeof(RetroSolitaireApp), "Solitaire", false),
            (typeof(RetroFreeCellApp), "FreeCell", false),
            (typeof(RetroReversiApp), "Reversi", false),
            (typeof(RetroSnakeApp), "Snake", false),
        };

        bool _installGames = true;
        bool _installTools = true;
        bool _createContent = true;
        Vector2 _scroll;

        [MenuItem("Deepwave/RetroOS/Setup", priority = 0)]
        public static void Open()
        {
            var window = GetWindow<RetroSetupWindow>("RetroOS Setup");
            window.minSize = new Vector2(430, 420);
        }

        void OnGUI()
        {
            RetroEditorStyles.Title(
                "Set up RetroOS",
                "Adds a computer to the open scene, installs the programs you pick as "
                    + "components under it, and writes the machines/ and internet/ folders.",
                BuiltinIcon.Settings
            );

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            RetroEditorStyles.Section("setup.programs", "Programs", () =>
            {
                _installTools = EditorGUILayout.ToggleLeft(
                    "Desktop programs (Explorer, Notepad, Browser, Inbox, CCTV, Terminal, "
                        + "Gallery, Control Panel, players)",
                    _installTools
                );
                _installGames = EditorGUILayout.ToggleLeft(
                    "Games (Mines, Solitaire, FreeCell, Reversi, Snake)",
                    _installGames
                );
                RetroEditorStyles.Hint(
                    "Each one becomes a component under the computer, so you can set its "
                        + "icon, placement and UnityEvents in the inspector afterwards."
                );
            });

            RetroEditorStyles.Section("setup.content", "Content", () =>
            {
                _createContent = EditorGUILayout.ToggleLeft(
                    "Write sample files, a LAN share and a working web site",
                    _createContent
                );
                EditorGUILayout.SelectableLabel(
                    RetroStorage.Root,
                    EditorStyles.textField,
                    GUILayout.Height(18)
                );

                EditorGUILayout.Space(4);

                var web = RetroContentBake.Needs(EditorUserBuildSettings.activeBuildTarget);
                var baked = RetroContentBake.Baked;
                var stale = baked && RetroContentBake.Stale();

                if (!baked)
                    EditorGUILayout.HelpBox(
                        "Nothing is baked yet. It bakes itself the moment a build needs one, "
                            + "and a WebGL or Android build will not go ahead without it.",
                        web ? MessageType.Warning : MessageType.None);
                else if (stale)
                    EditorGUILayout.HelpBox(
                        "The baked copy is behind the files under StreamingAssets. It refreshes "
                            + "itself; the button below forces it now.",
                        web ? MessageType.Warning : MessageType.Info);
                else
                    EditorGUILayout.HelpBox("The baked copy is current.", MessageType.Info);

                if (GUILayout.Button(baked ? "Bake the web/Android content again"
                        : "Bake the web/Android content now"))
                    RetroContentBake.Bake(log: true);

                RetroEditorStyles.Hint(
                    "WebGL and Android cannot open StreamingAssets as a folder, so those two "
                        + "builds read the OS's files out of this baked copy. Desktop builds "
                        + "copy the folder and ignore it. The editor keeps it current for you; "
                        + "this button is only for forcing a rebuild by hand."
                );
            });

            EditorGUILayout.Space(6);
            if (GUILayout.Button("Create everything", GUILayout.Height(32)))
                RunSetup();

            EditorGUILayout.Space(6);

            RetroEditorStyles.Section("setup.more", "More", () =>
            {
                if (GUILayout.Button("Add another computer to this scene"))
                    AddComputer(reuseExisting: false);

                RetroEditorStyles.Hint(
                    "Each computer gets its own address, its own folder under machines/ and "
                        + "its own accounts. Where the files live is the <b>World</b>'s "
                        + "business, so they all share that."
                );

                EditorGUILayout.Space(4);

                if (GUILayout.Button("Write the sample content again"))
                {
                    RetroSampleContent.CreateAll();
                    AssetDatabase.Refresh();
                }

                if (GUILayout.Button("Open the data folder"))
                    EditorUtility.RevealInFinder(RetroStorage.Root);
            });

            RetroEditorStyles.Section("setup.check", "Check", () =>
            {
                if (GUILayout.Button("Run the self test"))
                    RetroSelfTest.Run();

                RetroEditorStyles.Hint(
                    "Checks the parts that are easy to break and hard to see - the HTML and "
                        + "CSS engine, the folder mounts, the address resolver, the icon "
                        + "painter - and writes the result to the console."
                );

                EditorGUILayout.Space(4);

                if (GUILayout.Button("Take screenshots of the running OS"))
                    RetroSmokeRun.Run();

                RetroEditorStyles.Hint(
                    "Boots the OS for real in an empty scene, opens every program in turn and "
                        + "writes what it actually rendered to <b>SmokeShots/</b>. This is the "
                        + "check that catches what a compile cannot: a window that builds empty, "
                        + "a layout that collapses."
                );

                EditorGUILayout.Space(4);

                if (GUILayout.Button("Repaint the component icons"))
                    RetroInspectorIcons.Rebuild();

                RetroEditorStyles.Hint(
                    "The icons the main components wear in the Inspector - the computer, its "
                        + "machine and monitor, the world, the network, the event listener, the "
                        + "camera and the theme. They ship painted with the package; repaint them "
                        + "only after editing the art in <b>RetroInspectorIcons</b>."
                );
            });

            EditorGUILayout.EndScrollView();
        }

        void RunSetup()
        {
            if (_createContent)
            {
                RetroSampleContent.CreateAll();
                AssetDatabase.Refresh();
            }

            var bootstrap = AddComputer();
            var installed = InstallPrograms(bootstrap);

            Selection.activeGameObject = bootstrap.gameObject;
            Debug.Log(
                $"RetroOS: '{bootstrap.name}' is set up with {installed} program(s). Press Play."
            );
        }

        // The computer: one object carrying the machine, the system installed
        // on it and the screen it draws on.
        RetroOsBootstrap AddComputer(bool reuseExisting = true)
        {
            EnsureWorld();

            var existing = reuseExisting ? FindFirstObjectByType<RetroOsBootstrap>() : null;
            if (existing)
            {
                EditorSceneManager.MarkSceneDirty(existing.gameObject.scene);
                return existing;
            }

            var index = FindObjectsByType<RetroOsBootstrap>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            ).Length;

            var go = new GameObject(index == 0 ? "PC" : $"PC {index + 1}");
            Undo.RegisterCreatedObjectUndo(go, "Create RetroOS computer");

            // RequireComponent brings the machine and screen in with the bootstrap, so they are handled afterwards: the machine is named, the screen is there to review.
            var bootstrap = Undo.AddComponent<RetroOsBootstrap>(go);
            NameTheMachine(go.GetComponent<RetroMachine>(), index);

            EditorSceneManager.MarkSceneDirty(go.scene);
            return bootstrap;
        }

        // The machine's folder name must match what the sample content was written under, or it finds no drives after a wizard that just wrote them.
        void NameTheMachine(RetroMachine machine, int index)
        {
            if (!machine)
                return;

            var so = new SerializedObject(machine);

            // A second machine needs its own folder and address, or it is the first machine wearing another object's name, with an address clash.
            if (index > 0)
            {
                var name = $"MACHINE-{index + 1:00}";
                so.FindProperty("machineName").stringValue = name;
                so.FindProperty("folderName").stringValue = RetroStorage.Sanitize(name);
                so.FindProperty("addressMode").enumValueIndex = (int)AddressMode.Automatic;
                so.ApplyModifiedProperties();

                if (_createContent)
                {
                    RetroStorage.CreateMachine(RetroStorage.Sanitize(name));
                    AssetDatabase.Refresh();
                }

                return;
            }

            if (!_createContent)
                return;

            so.FindProperty("machineName").stringValue =
                RetroSampleContent.Workstation.ToUpperInvariant();
            so.ApplyModifiedProperties();
        }

        // Programs are components, so installing one is adding a child object.
        int InstallPrograms(RetroOsBootstrap bootstrap)
        {
            if (!bootstrap)
                return 0;

            var host = bootstrap.transform.Find("Programs");
            if (!host)
            {
                var go = new GameObject("Programs");
                Undo.RegisterCreatedObjectUndo(go, "Install RetroOS programs");
                go.transform.SetParent(bootstrap.transform, false);
                host = go.transform;
            }

            var existing = host.GetComponentsInChildren<RetroAppBehaviour>(true)
                .Select(a => a.GetType())
                .ToHashSet();

            var installed = 0;
            var order = 0;

            foreach (var (type, label, tool) in Catalogue)
            {
                order++;
                if (tool && !_installTools)
                    continue;
                if (!tool && !_installGames)
                    continue;
                if (existing.Contains(type))
                    continue;

                var go = new GameObject(label);
                Undo.RegisterCreatedObjectUndo(go, "Install " + label);
                go.transform.SetParent(host, false);

                var app = (RetroAppBehaviour)Undo.AddComponent(go, type);
                app.ApplyDefaults();
                app.Settings.sortOrder = order;

                // Viewers open through file associations, not from the desktop.
                if (
                    type == typeof(RetroImageViewerApp)
                    || type == typeof(RetroMediaPlayerApp)
                    || type == typeof(RetroCdPlayerApp)
                    || type == typeof(RetroNotepadApp)
                )
                    app.Settings.showOnDesktop = false;

                if (app is RetroBrowserApp browser)
                {
                    var so = new SerializedObject(browser);
                    so.FindProperty("homeUrl").stringValue = RetroSampleContent.Site;
                    so.ApplyModifiedProperties();
                }

                EditorUtility.SetDirty(app);
                installed++;
            }

            EditorSceneManager.MarkSceneDirty(bootstrap.gameObject.scene);
            return installed;
        }

        // Where files live, what year it is, and whether machines see each other are game facts - one per scene, read by every machine.
        static void EnsureWorld()
        {
            if (FindFirstObjectByType<RetroWorld>(FindObjectsInactive.Include))
                return;

            var go = new GameObject("Retro OS World");
            Undo.RegisterCreatedObjectUndo(go, "Create RetroOS World");
            Undo.AddComponent<RetroWorld>(go);

            EditorSceneManager.MarkSceneDirty(go.scene);
        }
    }
}
