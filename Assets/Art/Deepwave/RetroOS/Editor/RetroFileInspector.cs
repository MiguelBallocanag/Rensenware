using System.IO;
using UnityEditor;
using UnityEngine;
using Deepwave.RetroOS.Core.Network;
using Deepwave.RetroOS.Core.Storage;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.EditorTools
{
    // Edits a file/folder's RetroOS properties (hidden, encrypted, password, label, note)
    // from the Project window. Saving writes the `<name>.rmeta` sidecar; clearing every field deletes it.
    // Unity's `.meta` cannot carry this - Unity owns that name; the sidecar is readable at runtime.
    [CustomEditor(typeof(DefaultAsset))]
    public class RetroFileInspector : Editor
    {
        string _path;
        bool _isFolder;
        bool _isDrive;
        int _shareDepth;
        RetroMeta _meta;
        bool _dirty;

        void OnEnable() => Load();

        void Load()
        {
            _path = AssetDatabase.GetAssetPath(target);
            _meta = null;
            _dirty = false;

            if (string.IsNullOrEmpty(_path) || !InsideData(_path)) return;

            // A sidecar is not a node: editing one would write `D.rmeta.rmeta`, so hand it back to Unity's default inspector.
            if (IsSidecar(_path)) return;

            var full = Path.GetFullPath(_path).Replace('\\', '/');
            _isFolder = Directory.Exists(full);
            _meta = RetroMeta.Read(full);

            // A top-level folder of a machine is a drive, and gets the extra
            // drive fields.
            _isDrive = _isFolder && IsMachineDrive(full);

            // A share can only sit on a folder inside a machine's folder - a drive,
            // or something under one. Files and the internet tree cannot publish.
            _shareDepth = _isFolder ? DepthBelowMachine(full) : 0;
        }

        static bool InsideData(string assetPath)
        {
            var full = Path.GetFullPath(assetPath).Replace('\\', '/');
            return RetroStorage.IsInside(RetroStorage.Root, full);
        }

        static bool IsSidecar(string assetPath)
        {
            var name = Path.GetFileName(assetPath);
            return name.EndsWith(RetroMeta.Extension, System.StringComparison.OrdinalIgnoreCase)
                   || name.EndsWith(".meta", System.StringComparison.OrdinalIgnoreCase)
                   || name.Equals(".DS_Store", System.StringComparison.OrdinalIgnoreCase);
        }

        static bool IsMachineDrive(string fullPath)
        {
            var parent = Path.GetDirectoryName(fullPath)?.Replace('\\', '/');
            var grandparent = Path.GetDirectoryName(parent ?? "")?.Replace('\\', '/');
            return grandparent != null &&
                   string.Equals(Path.GetFullPath(grandparent).Replace('\\', '/'),
                       Path.GetFullPath(RetroStorage.Machines).Replace('\\', '/'),
                       System.StringComparison.OrdinalIgnoreCase);
        }

        // Folders this many levels below a machine's own folder are where a share
        // can sit: 1 = a drive (C), 2 = a folder on it, and so on. 0 = not inside a
        // machine at all. The network scan only reaches MachineShare.MaxDepth.
        static int DepthBelowMachine(string fullPath)
        {
            try
            {
                var machines = Path.GetFullPath(RetroStorage.Machines).Replace('\\', '/').TrimEnd('/');
                var full = Path.GetFullPath(fullPath).Replace('\\', '/').TrimEnd('/');
                if (!full.StartsWith(machines + "/", System.StringComparison.OrdinalIgnoreCase))
                    return 0;
                return full.Substring(machines.Length + 1).Split('/').Length - 1;
            }
            catch (System.Exception)
            {
                return 0;
            }
        }

        public override void OnInspectorGUI()
        {
            // This is now Unity's one DefaultAsset inspector, for every project that installs the package. Anything outside the RetroOS data folder goes back to Unity.
            if (_meta == null)
            {
                base.OnInspectorGUI();
                return;
            }

            // Unity draws DefaultAsset inspectors disabled; re-enable for these fields and restore after, or controls further down that Unity greyed out come back.
            var wasEnabled = GUI.enabled;
            GUI.enabled = true;

            try
            {
                RetroEditorStyles.Title(Path.GetFileName(_path),
                    _isDrive ? "Drive on a RetroOS machine"
                        : _isFolder ? "Folder inside the RetroOS file system"
                        : "File inside the RetroOS file system",
                    _isDrive ? BuiltinIcon.Drive : _isFolder ? BuiltinIcon.Folder : BuiltinIcon.Note);

                if (_isDrive) DrawDrive();
                DrawNode();
                if (_shareDepth >= 1) DrawSharing();
                DrawSidecar();
                DrawButtons();
            }
            finally
            {
                GUI.enabled = wasEnabled;
            }
        }

        void DrawDrive() => RetroEditorStyles.Section("file.drive", "Drive", () =>
        {
            var letter = Field("Letter", _meta.Drive ?? "", 1);
            if (letter != (_meta.Drive ?? "")) { _meta.Drive = letter; _dirty = true; }

            var kinds = new[] { "(auto)", "fixed", "floppy", "cdrom", "removable" };
            var current = Mathf.Max(0, System.Array.IndexOf(kinds, _meta.Kind ?? "(auto)"));
            var picked = EditorGUILayout.Popup("Type", current, kinds);
            if (picked != current)
            {
                _meta.Kind = picked == 0 ? null : kinds[picked];
                _dirty = true;
            }

            var label = Field("Volume label", _meta.Label ?? "");
            if (label != (_meta.Label ?? "")) { _meta.Label = label; _dirty = true; }

            RetroEditorStyles.Hint("Empty letter = the folder name. Empty type = fixed, " +
                                   "except A and B which are floppies.");
        });

        void DrawNode() => RetroEditorStyles.Section("file.node", _isFolder ? "Folder" : "File", () =>
        {
            var hidden = EditorGUILayout.Toggle(
                new GUIContent("Hidden", "Only shown when the player turns on hidden files, " +
                                         "or a RevealNode rule fires."), _meta.Hidden);
            if (hidden != _meta.Hidden) { _meta.Hidden = hidden; _dirty = true; }

            var encrypted = EditorGUILayout.Toggle(
                new GUIContent("Encrypted", "Asks for a password before it opens."), _meta.Encrypted);
            if (encrypted != _meta.Encrypted) { _meta.Encrypted = encrypted; _dirty = true; }

            if (encrypted)
            {
                var password = Field("Password", _meta.Password ?? "");
                if (password != (_meta.Password ?? "")) { _meta.Password = password; _dirty = true; }
                if (string.IsNullOrEmpty(_meta.Password))
                    EditorGUILayout.HelpBox("An encrypted node with no password opens on any input.",
                        MessageType.Warning);
            }

            var readOnly = EditorGUILayout.Toggle("Read only", _meta.ReadOnly);
            if (readOnly != _meta.ReadOnly) { _meta.ReadOnly = readOnly; _dirty = true; }

            var owner = EditorGUILayout.TextField(
                new GUIContent("Owner", "Account this item belongs to. Empty (or 'everyone') = "
                                        + "visible to all; a username = only that account, or nobody "
                                        + "signed in, may open it. Set automatically under Users/<name>."),
                _meta.Owner ?? "");
            if (owner != (_meta.Owner ?? "")) { _meta.Owner = owner; _dirty = true; }

            if (!_isDrive)
            {
                var label = Field("Display name", _meta.Label ?? "");
                if (label != (_meta.Label ?? "")) { _meta.Label = label; _dirty = true; }
            }

            EditorGUILayout.LabelField("Note", EditorStyles.miniLabel);
            var note = EditorGUILayout.TextArea(_meta.Note ?? "", GUILayout.Height(40));
            if (note != (_meta.Note ?? "")) { _meta.Note = note; _dirty = true; }
        });

        // A share is a property of the folder, so it is edited here beside its
        // password and owner - the same keys the runtime Explorer dialog writes.
        void DrawSharing() => RetroEditorStyles.Section("file.sharing", "Sharing", () =>
        {
            var shared = EditorGUILayout.Toggle(
                new GUIContent("Shared", "Publish this folder to the LAN as \\\\<address>\\<name>."),
                _meta.IsShared);
            if (shared != _meta.IsShared)
            {
                _meta.Share = shared ? DefaultShareName() : null;
                if (!shared)
                {
                    _meta.ShareComment = null;
                    _meta.SharePassword = null;
                    _meta.ShareHidden = false;
                }
                _dirty = true;
            }

            if (_meta.IsShared)
            {
                var name = Field("Share name", _meta.Share ?? "");
                if (name != (_meta.Share ?? "")) { _meta.Share = name; _dirty = true; }

                var comment = Field("Comment", _meta.ShareComment ?? "");
                if (comment != (_meta.ShareComment ?? "")) { _meta.ShareComment = comment; _dirty = true; }

                var password = Field("Password", _meta.SharePassword ?? "");
                if (password != (_meta.SharePassword ?? "")) { _meta.SharePassword = password; _dirty = true; }

                var shareHidden = EditorGUILayout.Toggle(
                    new GUIContent("Hidden", "Published but not listed - unreachable until a rule "
                                             + "turns it on, which is how a story reveals a network "
                                             + "folder partway through."), _meta.ShareHidden);
                if (shareHidden != _meta.ShareHidden) { _meta.ShareHidden = shareHidden; _dirty = true; }

                if (string.IsNullOrWhiteSpace(_meta.Share))
                    EditorGUILayout.HelpBox("A shared folder needs a name.", MessageType.Warning);

                if (_shareDepth > MachineShare.MaxDepth)
                    EditorGUILayout.HelpBox(
                        $"This folder is {_shareDepth} levels below the machine folder, but the "
                        + $"network scan only reaches {MachineShare.MaxDepth}. This share will not "
                        + "appear on the LAN - publish a folder nearer the drive.",
                        MessageType.Warning);
            }

            RetroEditorStyles.Hint(
                "A share lives in this folder's own <b>.rmeta</b> sidecar, so a folder copied to "
                + "another machine arrives shared. Only a folder on a machine can be published, "
                + "and each share name must be unique on its machine.");
        });

        // Matches the runtime Explorer dialog: the folder name, sanitised and upper-cased.
        string DefaultShareName()
        {
            var raw = RetroStorage.Sanitize(Path.GetFileName(_path)).ToUpperInvariant();
            return string.IsNullOrEmpty(raw) ? "SHARE" : raw;
        }

        // Anything the game does not know about is still shown, so a
        // hand-written key is never silently thrown away.
        void DrawSidecar()
        {
            if (_meta.IsEmpty) return;

            var extra = "";
            foreach (var pair in _meta.Values)
            {
                if (pair.Key is "hidden" or "encrypted" or "password" or "readonly"
                    or "label" or "note" or "drive" or "type" or "owner"
                    or "share" or "sharecomment" or "sharepassword" or "sharehidden") continue;
                extra += $"{pair.Key}: {pair.Value}\n";
            }
            if (extra.Length == 0) return;

            RetroEditorStyles.Section("file.sidecar", "Other keys in the sidecar", () =>
                RetroEditorStyles.Hint(extra.TrimEnd()));
        }

        void DrawButtons()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(!_dirty))
            {
                if (GUILayout.Button("Save", GUILayout.Height(24))) Save();
            }

            if (GUILayout.Button("Revert", GUILayout.Height(24))) Load();
            EditorGUILayout.EndHorizontal();

            var sidecar = RetroMeta.SidecarFor(_path);
            EditorGUILayout.LabelField(
                File.Exists(Path.GetFullPath(sidecar))
                    ? Path.GetFileName(sidecar)
                    : "no sidecar yet - saving creates one",
                EditorStyles.miniLabel);

            if (_dirty) EditorGUILayout.HelpBox("Unsaved changes.", MessageType.Info);
        }

        void Save()
        {
            var full = Path.GetFullPath(_path).Replace('\\', '/');
            if (!_meta.Write(full)) return;

            _dirty = false;
            AssetDatabase.Refresh();
            Debug.Log("RetroOS: wrote " + RetroMeta.SidecarFor(_path));
        }

        static string Field(string label, string value, int maxLength = 0)
        {
            var next = EditorGUILayout.TextField(label, value);
            return maxLength > 0 && next.Length > maxLength ? next[..maxLength] : next;
        }
    }
}
