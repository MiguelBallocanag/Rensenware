using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Deepwave.RetroOS.Core.FileSystem;
using Deepwave.RetroOS.Core.Storage;
using Deepwave.RetroOS.Events;
using Deepwave.RetroOS.UI;
using Deepwave.RetroOS.UI.Components;

namespace Deepwave.RetroOS.Apps
{
    // Notepad: reads any text file and, for disk-backed files, edits and saves it back.
    // New/Open/Save As work on the machine's own file system, so what the player writes is a real file.
    public class TextViewerView : AppView
    {
        RetroTextBox _editor;
        RetroStatusStrip _status;
        VfsFile _file;
        bool _dirty;
        bool _wordWrap = true;
        string _findTerm = "";

        // A document can be saved once it has somewhere on disk to go: either
        // it came from a real file, or Save As has given it one.
        bool CanSave => _file != null && !string.IsNullOrEmpty(_file.SourcePath);

        protected override void Build()
        {
            _file = Context.File;

            RetroUi.VLayout(Root.gameObject, 2, 2);
            BuildMenu();

            _editor = RetroUi.TextBox(Root, TextOf(_file), multiline: true,
                font: RetroSkin.Mono);
            RetroUi.Flexible(_editor);
            _editor.Field.lineType = InputField.LineType.MultiLineNewline;
            _editor.Edited += _ => MarkDirty();

            _status = RetroUi.StatusStrip(Root, -1f, 110f);
            UpdateStatus();
        }

        void BuildMenu()
        {
            var menu = RetroUi.Menu(Root);

            menu.AddMenu("File")
                .Command("New", NewDocument, "Ctrl+N")
                .Command("Open...", OpenDocument, "Ctrl+O", RetroIcons.Get(BuiltinIcon.FolderOpen))
                .Separator()
                .Command("Save", Save, "Ctrl+S", RetroIcons.Get(BuiltinIcon.Floppy))
                .Command("Save As...", SaveAs)
                .Separator()
                .Command("Print...", Print, "Ctrl+P", RetroIcons.Get(BuiltinIcon.Printer))
                .Separator()
                .Command("Close", TryClose, "Alt+F4");

            menu.AddMenu("Edit")
                .Command("Select All", () => _editor.Focus(), "Ctrl+A")
                .Separator()
                .Command("Find...", Find, "Ctrl+F")
                .Separator()
                .Command("Time/Date", InsertTimestamp, "F5");

            menu.AddMenu("Format")
                .Check("Word Wrap", _wordWrap, ToggleWrap);

            menu.AddMenu("Help").Command("About", () => RetroDialog.Info(Os.Overlay, "Notepad",
                "Notepad edits the machine's own files. Anything you save here " +
                "is a real file the rest of the OS can open."));
        }

        void ToggleWrap()
        {
            _wordWrap = !_wordWrap;
            var text = _editor.Field.textComponent;
            if (text) text.horizontalOverflow = _wordWrap
                ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
        }

        void InsertTimestamp()
        {
            _editor.Value += (_editor.Value.EndsWith("\n") ? "" : "\n") +
                             Os.Clock.Now.ToString("HH:mm dd/MM/yyyy");
            MarkDirty();
        }

        void MarkDirty()
        {
            if (_dirty) return;
            _dirty = true;
            UpdateStatus();
        }

        // --- documents ------------------------------------------------------

        void NewDocument()
        {
            Confirmed(() =>
            {
                _file = null;
                _editor.Value = "";
                _dirty = false;
                Retitle();
                UpdateStatus();
            });
        }

        void OpenDocument()
        {
            Confirmed(() => RetroFileDialog.Open(Os, "Open",
                new[] { FileKind.Text, FileKind.Web }, Load, _file?.Parent));
        }

        void Load(VfsFile file)
        {
            if (file == null) return;

            if (file.Locked)
            {
                Os.PromptUnlock(file, () => Load(file));
                return;
            }

            _file = file;
            _editor.Value = TextOf(file);
            _dirty = false;
            Retitle();
            UpdateStatus();
            Os.Events.Raise(OsEvents.FileOpened, file.Path);
        }

        void Save()
        {
            if (!CanSave) { SaveAs(); return; }

            if (!DiskMount.SafeWriteText(_file.SourcePath, _editor.Value))
            {
                RetroDialog.Alert(Os.Overlay, "Notepad", "The file could not be written.");
                return;
            }

            _file.Content = _editor.Value;
            _file.Size = _editor.Value.Length;
            _dirty = false;
            UpdateStatus();

            Os.Events.Raise(OsEvents.FileSaved, _file.Path);
            Os.NotifyFsChanged(_file.Path, FsChange.Written);
            Raise("saved", _file.Path);
        }

        // Access check: Load() already handled the password box, so a refusal here means the file is someone else's.
        string TextOf(VfsFile file) =>
            file != null && file.TryRead(Os.CurrentUser, out var text) ? text ?? "" : "";

        void SaveAs()
        {
            RetroFileDialog.Save(Os, "Save As", _file?.Name ?? "Untitled.txt",
                WriteTo, _file?.Parent);
        }

        // Only a real disk folder (a drive, share, user's documents) can hold a file; a made-up branch has no path.
        void WriteTo(VfsDirectory folder, string name)
        {
            if (folder == null || string.IsNullOrWhiteSpace(folder.SourcePath))
            {
                RetroDialog.Alert(Os.Overlay, "Save As",
                    "Files cannot be written here.\n\nChoose a folder on a drive.");
                return;
            }

            var path = Path.Combine(folder.SourcePath, name);
            if (!DiskMount.SafeWriteText(path, _editor.Value))
            {
                RetroDialog.Alert(Os.Overlay, "Save As", "The file could not be written.");
                return;
            }

            // Adopt whatever node the folder now holds for that name, so the
            // document keeps saving to the file it just created.
            Os.Drives.Refresh();
            _file = Os.Fs.Resolve(folder.Path + "/" + name) as VfsFile;

            _dirty = false;
            Retitle();
            UpdateStatus();

            Os.Events.Raise(OsEvents.FileSaved, path);
            Os.NotifyFsChanged(_file?.Path ?? path, FsChange.Created);
            Raise("saved", _file?.Path ?? path);
        }

        void Print()
        {
            var job = RetroPrinter.Print(Os, _file?.Name ?? "Untitled", _editor.Value);
            _status.Set(0, job.Message);
            if (job.Ok) Raise("print", job.Path);
            else RetroDialog.Alert(Os.Overlay, "Print", job.Message);
        }

        // --- find -----------------------------------------------------------

        void Find()
        {
            RetroDialog.Prompt(Os.Overlay, "Find", "Find what:", term =>
            {
                _findTerm = term ?? "";
                if (_findTerm.Length == 0) return;

                var at = _editor.Value.IndexOf(_findTerm, StringComparison.OrdinalIgnoreCase);
                if (at < 0)
                {
                    RetroDialog.Info(Os.Overlay, "Find", $"Cannot find '{_findTerm}'.");
                    return;
                }

                // uGUI can put the caret somewhere, which is as close to
                // "scroll there and select it" as an InputField gets.
                _editor.Focus();
                _editor.Field.caretPosition = at;
                _editor.Field.selectionAnchorPosition = at;
                _editor.Field.selectionFocusPosition = at + _findTerm.Length;
                _editor.Field.ForceLabelUpdate();
                _status.Set(0, $"Found '{_findTerm}' at character {at}.");
            });
        }

        // --- closing --------------------------------------------------------

        void TryClose() => Confirmed(() => Window.Close());

        // Runs an action, offering to save first if there is unsaved work.
        void Confirmed(Action action)
        {
            if (!_dirty) { action(); return; }

            RetroDialog.Confirm(Os.Overlay, "Notepad",
                "Save changes to this document?", ok =>
                {
                    if (ok) Save();
                    action();
                });
        }

        void Retitle()
        {
            var title = App?.Title ?? "Notepad";
            Window.SetTitle(_file == null ? $"Untitled - {title}" : $"{_file.Name} - {title}");
        }

        void UpdateStatus()
        {
            _status.Set(0, _file == null ? "Untitled" : _file.Path + (_dirty ? " *" : ""));
            _status.Set(1, CanSave ? (_dirty ? "Modified" : "Saved") : "Not saved yet");
        }
    }
}
