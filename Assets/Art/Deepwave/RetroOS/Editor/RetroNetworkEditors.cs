using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Deepwave.RetroOS.Core.Network;
using Deepwave.RetroOS.Core.Storage;
using Deepwave.RetroOS.Data;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.EditorTools
{
    // Inspector for a computer: name, drives folder, address, accounts, BIOS screen, shares.
    // Accounts and BIOS live here because they belong to the machine - the next desk is a different user, and firmware survives a disk wipe.
    [CustomEditor(typeof(RetroMachine))]
    public class RetroMachineEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var machine = (RetroMachine)target;
            serializedObject.Update();

            RetroEditorStyles.Title(
                machine.MachineName,
                machine.Online
                    ? $"{machine.Address} · {machine.VisibleShares.Count()} share(s)"
                    : "Off the network",
                BuiltinIcon.Computer
            );

            RetroEditorStyles.Section("machine.identity", "Identity", () =>
            {
                Field("machineName");
                Field("folderName");
                Field("workgroup");
            });

            RetroEditorStyles.Section("machine.network", "Network", () =>
            {
                Field("addressMode");
                Field("address");
                Field("poweredOn");
                Field("connected");
                RetroEditorStyles.Hint(
                    "Switch either off and this machine disappears from every other "
                        + "machine's Network Neighborhood - which is what pulling the plug on "
                        + "a file server does."
                );
            });

            RetroEditorStyles.Section("machine.accounts", "Accounts", () =>
            {
                RetroEditorStyles.List(serializedObject.FindProperty("users"), "Users");
                EditorGUILayout.Space(4);
                RetroEditorStyles.Inline(serializedObject.FindProperty("login"));

                if (machine.Users.Count == 0 && machine.Login.mode == LoginMode.AlwaysAsk)
                    EditorGUILayout.HelpBox(
                        "Login is set to Always Ask but there are no users, so nobody can "
                            + "sign in at this machine.",
                        MessageType.Error
                    );

                RetroEditorStyles.Hint(
                    "Accounts belong to the computer, the way they did on a real 95 box. No "
                        + "users at all means no logon screen and no privacy, which is right "
                        + "for a single-user machine."
                );
            });

            RetroEditorStyles.Section("machine.boot", "Start-up", () =>
            {
                RetroEditorStyles.Inline(serializedObject.FindProperty("boot"));
                RetroEditorStyles.Hint(
                    "The firmware screen this machine prints before the OS loads. It belongs "
                        + "to the hardware, so it stays the same when a different system is "
                        + "installed on the disk."
                );
            });

            DrawDrives(machine);
            DrawShares(machine);

            serializedObject.ApplyModifiedProperties();
        }

        void Field(string name)
        {
            var property = serializedObject.FindProperty(name);
            if (property != null)
                EditorGUILayout.PropertyField(property, true);
        }

        // Reading the machine folder hits disk, and an inspector repaints continuously, so the answer is cached for a second.
        static List<RetroDrive> Drives(string folder) =>
            RetroEditorCache.Get("machine.drives:" + folder, () => DiskMount.MountDrives(folder));

        static bool Exists(string folder) =>
            RetroEditorCache.Get("dir:" + folder, () => Directory.Exists(folder));

        void DrawDrives(RetroMachine machine)
        {
            RetroEditorStyles.Section("machine.drives", "Drives on disk", () =>
            {
                var folder = machine.Folder;
                EditorGUILayout.SelectableLabel(
                    folder,
                    EditorStyles.textField,
                    GUILayout.Height(18)
                );

                if (!Exists(folder))
                {
                    EditorGUILayout.HelpBox(
                        "This folder does not exist, so the machine has no drives.",
                        MessageType.Warning
                    );

                    if (GUILayout.Button("Create it with C:, A: and D:"))
                    {
                        RetroStorage.CreateMachine(machine.FolderName);
                        RetroEditorCache.Forget("dir:" + folder);
                        RetroEditorCache.Forget("machine.drives:" + folder);
                        AssetDatabase.Refresh();
                    }

                    return;
                }

                foreach (var drive in Drives(folder))
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label(
                        new GUIContent(
                            $" {drive.DisplayName}",
                            RetroEditorStyles.Icon(DriveIcon(drive.Kind), 32)
                        ),
                        GUILayout.Height(20)
                    );
                    GUILayout.Label(
                        drive.Ready ? drive.Kind.ToString() : "no disk",
                        RetroEditorStyles.Pill,
                        GUILayout.Width(80)
                    );
                    EditorGUILayout.EndHorizontal();
                }

                if (GUILayout.Button("Open folder"))
                    EditorUtility.RevealInFinder(folder);
            });
        }

        static BuiltinIcon DriveIcon(DriveKind kind) =>
            kind switch
            {
                DriveKind.Floppy => BuiltinIcon.Floppy,
                DriveKind.CdRom => BuiltinIcon.CdRom,
                DriveKind.Network => BuiltinIcon.Network,
                _ => BuiltinIcon.Drive,
            };

        void DrawShares(RetroMachine machine)
        {
            var shares = machine.Shares.ToList();

            RetroEditorStyles.Section("machine.shares", $"Shares ({shares.Count})", () =>
            {
                if (shares.Count == 0)
                    RetroEditorStyles.Hint(
                        "Nothing published. Other machines will see this one only when it "
                            + "shares a folder - select the folder under this machine's drives "
                            + "and give it a <b>Share name</b>, or share it from Explorer while "
                            + "the game runs."
                    );

                foreach (var share in shares)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label(
                        new GUIContent(
                            $" \\\\{machine.Address}\\{share.ShareName}",
                            RetroEditorStyles.Icon(BuiltinIcon.Network, 32)
                        ),
                        GUILayout.Height(20)
                    );
                    GUILayout.Label(
                        share.Available ? "ok" : share.Visible ? "unavailable" : "hidden",
                        RetroEditorStyles.Pill,
                        GUILayout.Width(70)
                    );
                    if (GUILayout.Button("Open", GUILayout.Width(56)))
                        EditorUtility.RevealInFinder(share.Folder);
                    EditorGUILayout.EndHorizontal();
                }

                RetroEditorStyles.Hint(
                    "A share is a property of the folder, kept in its <b>.rmeta</b> sidecar "
                        + "beside its password and its owner - so a folder copied to another "
                        + "machine arrives shared, and nothing in the scene can drift from the "
                        + "folder it points at."
                );

                if (GUILayout.Button("Re-read shares"))
                    machine.Notify();
            });
        }
    }

    // A site on the fake internet.
    [CustomEditor(typeof(RetroInternetHost))]
    public class RetroInternetHostEditor : Editor
    {
        // Same reason as the machine inspector: do not walk the site folder on
        // every repaint.
        static List<string> Pages(string folder) =>
            RetroEditorCache.Get(
                "site.pages:" + folder,
                () => Directory.GetFiles(folder, "*.htm*").Select(Path.GetFileName).ToList()
            );

        public override void OnInspectorGUI()
        {
            var host = (RetroInternetHost)target;

            RetroEditorStyles.Title(
                host.Domain,
                host.Online ? "Serving" : "Off the air",
                BuiltinIcon.Globe
            );

            DrawDefaultInspector();

            EditorGUILayout.Space(4);
            EditorGUILayout.SelectableLabel(
                host.Folder,
                EditorStyles.textField,
                GUILayout.Height(18)
            );

            if (!host.Exists)
            {
                EditorGUILayout.HelpBox(
                    "That folder does not exist. The site will report 'Host not found'.",
                    MessageType.Warning
                );

                if (GUILayout.Button("Create it"))
                {
                    Directory.CreateDirectory(host.Folder);
                    File.WriteAllText(
                        Path.Combine(host.Folder, host.IndexPage),
                        $"<html>\n<head><title>{host.Domain}</title></head>\n"
                            + "<body>\n  <h1>Under construction</h1>\n"
                            + "  <p>Edit this file in any editor.</p>\n</body>\n</html>\n"
                    );
                    RetroEditorCache.Forget("site.pages:" + host.Folder);
                    AssetDatabase.Refresh();
                }

                return;
            }

            var pages = Pages(host.Folder);
            RetroEditorStyles.Hint(
                pages.Count == 0
                    ? "No pages in the folder yet."
                    : $"{pages.Count} page(s): " + string.Join(", ", pages.Take(6))
            );

            if (GUILayout.Button("Open folder"))
                EditorUtility.RevealInFinder(host.Folder);
        }
    }
}
