using System;
using System.Collections.Generic;
using UnityEngine;
using Deepwave.RetroOS.Core.FileSystem;
using Deepwave.RetroOS.UI.Components;

namespace Deepwave.RetroOS.UI
{
    /// <summary>The Open and Save As boxes, over the machine's file system. Browsing goes through <see cref="RetroOsContext.List"/>, so only what the signed-in user may see is offered.</summary>
    public static class RetroFileDialog
    {
        /// <summary>Asks the player to pick an existing file.</summary>
        /// <param name="filter">Kinds of file to offer; empty offers everything.</param>
        /// <param name="onPicked">Runs with the chosen file. Not called on Cancel.</param>
        /// <param name="start">Folder to open in; <c>null</c> starts at My Computer.</param>
        public static void Open(RetroOsContext os, string title, IEnumerable<FileKind> filter,
            Action<VfsFile> onPicked, VfsDirectory start = null)
        {
            Show(os, title, filter, start, null, false, (file, _, __) => onPicked?.Invoke(file));
        }

        /// <summary>Asks the player where to write, and under what name.</summary>
        /// <param name="onPicked">Runs with the folder and typed name; any overwrite is already confirmed.</param>
        public static void Save(RetroOsContext os, string title, string suggestedName,
            Action<VfsDirectory, string> onPicked, VfsDirectory start = null)
        {
            Show(os, title, null, start, suggestedName, true,
                (_, folder, name) => onPicked?.Invoke(folder, name));
        }

        static void Show(RetroOsContext os, string title, IEnumerable<FileKind> filter,
            VfsDirectory start, string suggestedName, bool saving,
            Action<VfsFile, VfsDirectory, string> commit)
        {
            if (os == null) return;

            var kinds = filter == null ? null : new List<FileKind>(filter);
            var folder = start ?? os.Drives.Root;
            VfsFile picked = null;

            var (blocker, body) = RetroDialog.Custom(os.Overlay, title, 460f, 340f,
                saving ? BuiltinIcon.Floppy : BuiltinIcon.FolderOpen);

            // Where we are, and the way back up.
            var pathRow = RetroUi.NewRect("Path", body);
            RetroUi.Size(pathRow, h: 24f);
            RetroUi.HLayout(pathRow.gameObject, 4, 0);

            var pathLabel = RetroUi.NoWrap(RetroUi.Label(pathRow, "", 12, TextAnchor.MiddleLeft));
            RetroUi.Flexible(pathLabel);
            var upButton = RetroUi.PushButton(pathRow, "Up", null, RetroIcons.Get(BuiltinIcon.FolderOpen));
            RetroUi.Size(upButton, w: 60f);

            var list = RetroUi.ListView(body, ListViewMode.Details);
            RetroUi.Flexible(list);
            list.SetColumns(new[] { "Name", "Type", "Size" }, new[] { -1f, 100f, 80f });

            RetroTextBox nameField = null;
            if (saving)
            {
                var nameRow = RetroUi.NewRect("Name", body);
                RetroUi.Size(nameRow, h: 26f);
                RetroUi.HLayout(nameRow.gameObject, 4, 0);
                RetroUi.Size(RetroUi.NoWrap(
                    RetroUi.Label(nameRow, "File name:", 12, TextAnchor.MiddleLeft)), w: 70f);
                nameField = RetroUi.TextBox(nameRow, suggestedName ?? "");
                RetroUi.Flexible(nameField);
            }

            var buttons = RetroUi.NewRect("Buttons", body);
            RetroUi.Size(buttons, h: 26f);
            RetroUi.HLayout(buttons.gameObject, 6, 0);
            RetroUi.Spacer(buttons);
            var okButton = RetroUi.PushButton(buttons, saving ? "Save" : "Open", null);
            RetroUi.Size(okButton, w: 76f);
            RetroUi.Size(RetroUi.PushButton(buttons, "Cancel",
                () => RetroDialog.Dismiss(blocker)), w: 76f);

            void Fill()
            {
                list.Clear();
                pathLabel.text = folder.Path;
                upButton.Interactable = folder.Parent != null;

                foreach (var node in os.List(folder))
                {
                    if (node is VfsDirectory dir)
                    {
                        list.Add(new RetroListEntry
                        {
                            Text = dir.DisplayName,
                            Icon = RetroIcons.Get(BuiltinIcon.Folder),
                            Columns = new[] { "Folder", "" },
                            Tag = dir
                        });
                        continue;
                    }

                    if (node is not VfsFile file) continue;
                    if (kinds != null && kinds.Count > 0 && !kinds.Contains(file.Kind)) continue;

                    list.Add(new RetroListEntry
                    {
                        Text = file.DisplayName,
                        Icon = RetroIcons.Get(file.Locked ? BuiltinIcon.Lock : BuiltinIcon.File),
                        Columns = new[] { file.Kind.ToString(), file.SizeText },
                        Tag = file
                    });
                }

                // No selection carries into a folder we just walked into.
                picked = null;
                UpdateOk();
            }

            void UpdateOk() =>
                okButton.Interactable = saving
                    ? !string.IsNullOrWhiteSpace(nameField?.Value)
                    : picked != null;

            void Enter(VfsDirectory dir)
            {
                if (dir == null) return;
                if (dir.Locked)
                {
                    os.PromptUnlock(dir, () => { folder = dir; Fill(); });
                    return;
                }
                folder = dir;
                Fill();
            }

            list.SelectionChanged += entry =>
            {
                picked = entry?.Tag as VfsFile;
                if (picked != null && nameField != null) nameField.Value = picked.Name;
                UpdateOk();
            };

            list.Activated += entry =>
            {
                if (entry?.Tag is VfsDirectory dir) { Enter(dir); return; }
                if (entry?.Tag is VfsFile file && !saving)
                {
                    RetroDialog.Dismiss(blocker);
                    commit(file, folder, file.Name);
                }
            };

            upButton.Clicked += () => Enter(folder.Parent);
            if (nameField != null) nameField.Edited += _ => UpdateOk();

            okButton.Clicked += () =>
            {
                if (!saving)
                {
                    if (picked == null) return;
                    RetroDialog.Dismiss(blocker);
                    commit(picked, folder, picked.Name);
                    return;
                }

                var name = nameField.Value.Trim();

                // A typed name is not a path: reject "../../notes.txt".
                if (!Core.Storage.DiskOperations.IsPlainName(name, out var why))
                {
                    if (name.Length > 0) RetroDialog.Alert(os.Overlay, title, why);
                    return;
                }

                var clash = folder.Find(name);

                // A folder of that name is not something to overwrite.
                if (clash is VfsDirectory)
                {
                    RetroDialog.Alert(os.Overlay, title,
                        $"'{name}' is a folder here.\n\nChoose another name.");
                    return;
                }

                // The clash may belong to another account - invisible here, not ours to overwrite.
                if (clash != null && !os.CanAccess(clash))
                {
                    RetroDialog.Alert(os.Overlay, title,
                        $"'{name}' is already in use here.\n\nChoose another name.");
                    return;
                }

                // Overwriting is the player's call - the only destructive thing this box does.
                if (clash is VfsFile existing)
                {
                    RetroDialog.Confirm(os.Overlay, title,
                        $"'{existing.Name}' already exists.\n\nReplace it?", ok =>
                        {
                            if (!ok) return;
                            RetroDialog.Dismiss(blocker);
                            commit(null, folder, name);
                        });
                    return;
                }

                RetroDialog.Dismiss(blocker);
                commit(null, folder, name);
            };

            Fill();
        }
    }
}
