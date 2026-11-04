using System.IO;
using UnityEditor;
using UnityEngine;
using Deepwave.RetroOS.UI;
using Deepwave.RetroOS.UI.Icons;

namespace Deepwave.RetroOS.EditorTools
{
    // Browses the built-in icon set at any of the three resolutions and exports any as PNG for use outside the OS.
    public class RetroIconBrowser : EditorWindow
    {
        int _size = 48;
        bool _vivid = true;
        string _filter = "";
        Vector2 _scroll;
        BuiltinIcon _selected = BuiltinIcon.Computer;

        [MenuItem("Deepwave/RetroOS/Icon Browser", priority = 20)]
        public static void Open()
        {
            var window = GetWindow<RetroIconBrowser>("RetroOS Icons");
            window.minSize = new Vector2(420, 380);
        }

        void OnGUI()
        {
            RetroEditorStyles.Title("Built-in icons",
                $"{RetroIcons.All.Length} icons, painted at runtime - no textures to import.",
                BuiltinIcon.Paint);

            DrawToolbar();
            DrawGrid();
            DrawSelected();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            _filter = GUILayout.TextField(_filter, EditorStyles.toolbarSearchField,
                GUILayout.Width(160));

            GUILayout.Label("Size", EditorStyles.miniLabel, GUILayout.Width(30));
            foreach (var option in new[] { 32, 48, 64 })
            {
                var on = _size == option;
                if (GUILayout.Toggle(on, option.ToString(), EditorStyles.toolbarButton,
                        GUILayout.Width(34)) != on)
                    _size = option;
            }

            var vivid = GUILayout.Toggle(_vivid, "Vivid", EditorStyles.toolbarButton, GUILayout.Width(50));
            if (vivid != _vivid) _vivid = vivid;

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Export all as PNG", EditorStyles.toolbarButton))
                ExportAll();

            // One sheet with every icon, for docs and for eyeballing the whole set after a painter change.
            if (GUILayout.Button("Contact sheet", EditorStyles.toolbarButton))
                RetroIconSheet.Export();

            EditorGUILayout.EndHorizontal();
        }

        void DrawGrid()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            var cell = _size + 26;
            var columns = Mathf.Max(1, Mathf.FloorToInt((position.width - 24) / cell));
            var shown = 0;

            EditorGUILayout.BeginHorizontal();
            foreach (var icon in RetroIcons.All)
            {
                if (!string.IsNullOrEmpty(_filter) &&
                    icon.ToString().IndexOf(_filter, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (shown > 0 && shown % columns == 0)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }
                shown++;

                EditorGUILayout.BeginVertical(GUILayout.Width(cell));
                var rect = GUILayoutUtility.GetRect(cell - 4, _size + 4);
                if (icon == _selected) EditorGUI.DrawRect(rect, RetroEditorStyles.AccentSoft);

                var texture = RetroIcons.GetTexture(icon, _size, _vivid);
                GUI.DrawTexture(new Rect(rect.center.x - _size / 2f, rect.y + 2, _size, _size),
                    texture, ScaleMode.ScaleToFit);

                if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) _selected = icon;
                GUILayout.Label(icon.ToString(), RetroEditorStyles.Pill, GUILayout.Width(cell - 4));
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndScrollView();
        }

        void DrawSelected()
        {
            EditorGUILayout.BeginVertical(RetroEditorStyles.Card);
            EditorGUILayout.BeginHorizontal();

            var preview = GUILayoutUtility.GetRect(64, 64, GUILayout.Width(64));
            GUI.DrawTexture(preview, RetroIcons.GetTexture(_selected, 64, _vivid), ScaleMode.ScaleToFit);

            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(_selected.ToString(), EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel($"BuiltinIcon.{_selected}", EditorStyles.textField,
                GUILayout.Height(18));
            EditorGUILayout.SelectableLabel($"RetroIcons.Get(BuiltinIcon.{_selected})",
                EditorStyles.textField, GUILayout.Height(18));
            if (GUILayout.Button("Export this icon")) Export(_selected);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        void Export(BuiltinIcon icon)
        {
            var path = EditorUtility.SaveFilePanel("Export icon", Application.dataPath,
                icon + "_" + _size, "png");
            if (string.IsNullOrEmpty(path)) return;
            Write(icon, path);
            AssetDatabase.Refresh();
            EditorUtility.RevealInFinder(path);
        }

        void ExportAll()
        {
            var folder = EditorUtility.SaveFolderPanel("Export every icon", Application.dataPath, "RetroIcons");
            if (string.IsNullOrEmpty(folder)) return;

            foreach (var icon in RetroIcons.All)
                Write(icon, Path.Combine(folder, $"{icon}_{_size}.png"));

            AssetDatabase.Refresh();
            EditorUtility.RevealInFinder(folder);
            Debug.Log($"RetroOS: exported {RetroIcons.All.Length} icons to {folder}");
        }

        void Write(BuiltinIcon icon, string path)
        {
            var texture = RetroIcons.CreateTexture(icon, _size, _vivid);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            DestroyImmediate(texture);
        }
    }
}
