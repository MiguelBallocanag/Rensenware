using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Deepwave.RetroOS.Core.FileSystem;
using Deepwave.RetroOS.Events;
using Deepwave.RetroOS.UI;
using Deepwave.RetroOS.UI.Components;

namespace Deepwave.RetroOS.Apps
{
    // Thumbnail grid of every visible, unlocked image, with a viewer to page through.
    // Re-scans on file-system changes (reveals, unlocks, discs).
    public class PhotoGalleryView : AppView
    {
        // One page of thumbnails is built at a time; the rest wait behind Next.
        const int PageSize = 60;

        readonly List<VfsFile> _photos = new();

        RetroPanelHost _panels;
        RectTransform _grid;
        Image _viewerPicture;
        Text _viewerTitle;
        Text _viewerNote;
        RetroStatusStrip _status;
        RetroPushButton _prevButton, _nextButton;
        int _page;
        int _shown = -1;

        IGallerySource Source => Config<IGallerySource>();

        bool Viewing => _panels != null && _panels.Current == 1;
        VfsFile Current => _shown >= 0 && _shown < _photos.Count ? _photos[_shown] : null;

        protected override void Build()
        {
            RetroUi.VLayout(Root.gameObject, 2, 2);
            BuildMenu();

            _panels = RetroPanelHost.Create(Root);
            RetroUi.Flexible(_panels);
            BuildLibrary(_panels.AddPanel("Library"));
            BuildViewer(_panels.AddPanel("Viewer"));

            _status = RetroUi.StatusStrip(Root, -1f, 110f);

            // Refresh destroys and repaints every thumbnail on screen, so it
            // is worth asking first whether the change was in this folder.
            Os.FsChanged += OnFsChanged;
            Refresh();
        }

        void BuildMenu()
        {
            var menu = RetroUi.Menu(Root);

            menu.AddMenu("File")
                .Command("Open in Viewer", () => Show(_shown < 0 ? 0 : _shown), "Enter")
                .Command("Open With...", OpenWith)
                .Separator()
                .Command("Set as Wallpaper", SetAsWallpaper, null,
                    RetroIcons.Get(BuiltinIcon.Paint))
                .Separator()
                .Command("Delete", Delete, "Del", RetroIcons.Get(BuiltinIcon.Trash))
                .Separator()
                .Command("Close", () => Window.Close(), "Alt+F4");

            menu.AddMenu("View")
                .Command("Library", ShowLibrary, "Backspace")
                .Separator()
                .Command("Previous", Previous, "Left")
                .Command("Next", Next, "Right")
                .Separator()
                .Command("Refresh", Refresh, "F5");

            menu.AddMenu("Help").Command("About", () => RetroDialog.Info(Os.Overlay, "Photo Gallery",
                "Every picture this account can see, in one place.\n" +
                "Click a thumbnail to open it, then use the arrow keys."));
        }

        void BuildLibrary(RectTransform page)
        {
            // autoLayout off: a grid replaces the default vertical layout.
            var content = RetroUi.VScroll(page, out var scroll, autoLayout: false);
            RetroUi.Flexible(RetroUi.ScrollFrame(scroll));

            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(96, 96);
            grid.spacing = new Vector2(6, 6);
            grid.padding = new RectOffset(6, 6, 6, 6);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            _grid = content;
        }

        void BuildViewer(RectTransform page)
        {
            var frame = RetroUi.Panel(page, PaletteSlot.Screen, "Frame");
            RetroUi.Flexible(frame);
            RetroUi.Bevel(frame.rectTransform, false, 1);

            _viewerPicture = RetroUi.Picture(frame.transform, null);
            RetroUi.Stretch(_viewerPicture.rectTransform, 6);

            // Double-clicking the picture goes back to the wall of thumbnails.
            frame.gameObject.AddComponent<RetroDoubleClick>().Clicked = ShowLibrary;

            var caption = RetroUi.NewRect("Caption", page);
            RetroUi.Size(caption, h: 34f);
            RetroUi.VLayout(caption.gameObject, 0, 0);
            _viewerTitle = RetroUi.NoWrap(RetroUi.Label(caption, "", 14, TextAnchor.MiddleCenter));
            _viewerTitle.fontStyle = FontStyle.Bold;
            _viewerNote = RetroUi.NoWrap(RetroUi.Label(caption, "", 12, TextAnchor.MiddleCenter));
            RetroPalette.Paint(_viewerNote, PaletteSlot.TextDisabled);

            var row = RetroUi.NewRect("Nav", page);
            RetroUi.Size(row, h: 26f);
            RetroUi.HLayout(row.gameObject, 6, 0);
            _prevButton = RetroUi.PushButton(row, "< Previous", Previous);
            RetroUi.SoftSize(_prevButton, 100f, 62f);
            RetroUi.SoftSize(RetroUi.PushButton(row, "Library", ShowLibrary), 90f, 56f);
            RetroUi.SoftSize(RetroUi.PushButton(row, "Open With...", OpenWith), 110f, 66f);
            RetroUi.SoftSize(RetroUi.PushButton(row, "Wallpaper", SetAsWallpaper), 96f, 60f);
            RetroUi.Spacer(row);
            _nextButton = RetroUi.PushButton(row, "Next >", Next);
            RetroUi.SoftSize(_nextButton, 100f, 62f);
        }

        // The folder the program points at, or the whole machine when it
        // names nothing (or names something that is not there any more).
        VfsDirectory RootFolder()
        {
            var path = Source?.FolderPath;
            if (string.IsNullOrWhiteSpace(path)) return Os.Fs.Root;
            return Os.Fs.Resolve(path.Trim()) as VfsDirectory ?? Os.Fs.Root;
        }

        void OnFsChanged(FsChangedArgs change)
        {
            if (!this || !_grid) return;

            if (change.Touches(RootFolder()?.Path)) Refresh();
        }

        void Refresh()
        {
            if (!this || !_grid) return;
            foreach (Transform child in _grid) Destroy(child.gameObject);

            var keep = Current;
            _photos.Clear();
            foreach (var file in Collect(RootFolder())) _photos.Add(file);

            var pages = Mathf.Max(1, Mathf.CeilToInt(_photos.Count / (float)PageSize));
            _page = Mathf.Clamp(_page, 0, pages - 1);

            var from = _page * PageSize;
            var to = Mathf.Min(from + PageSize, _photos.Count);

            for (var i = from; i < to; i++)
            {
                var index = i;
                var file = _photos[i];
                var cell = RetroUi.Button(_grid, "", () => Show(index));
                var picture = RetroUi.Picture(cell.transform, PictureOf(file));
                RetroUi.Stretch(picture.rectTransform, 4);
                RetroUi.ToolTip(cell, file.Name);
            }

            if (_photos.Count == 0)
            {
                RetroUi.Label(_grid, "No photos found.", 13, TextAnchor.MiddleCenter);
                _shown = -1;
                ShowLibrary();
                SetStatus();
                return;
            }

            if (pages > 1)
            {
                if (_page > 0) RetroUi.Button(_grid, "< Back", () => { _page--; Refresh(); });
                if (_page < pages - 1) RetroUi.Button(_grid, "Next >", () => { _page++; Refresh(); });
            }

            // A shown picture survives a refresh; one deleted while open sends the viewer back to the library.
            var restored = keep != null ? _photos.IndexOf(keep) : -1;
            _shown = Mathf.Min(_shown, _photos.Count - 1);
            if (Viewing)
            {
                if (restored >= 0) { _shown = -1; Show(restored); }
                else ShowLibrary();
            }
            SetStatus();
        }

        IEnumerable<VfsFile> Collect(VfsDirectory dir)
        {
            // Walks with the signed-in user's eyes, so the gallery never shows
            // somebody else's photos.
            foreach (var node in Os.List(dir))
            {
                if (node.Locked) continue;
                if (node is VfsDirectory sub)
                    foreach (var file in Collect(sub)) yield return file;
                else if (node is VfsFile file && file.Kind == FileKind.Image)
                    yield return file;
            }
        }

        // --- viewer ---------------------------------------------------------

        void Show(int index)
        {
            if (index < 0 || index >= _photos.Count) return;
            _shown = index;

            var file = _photos[index];
            var sprite = PictureOf(file);
            _viewerPicture.sprite = sprite;
            _viewerPicture.enabled = sprite;
            _viewerTitle.text = file.Name;
            _viewerNote.text = sprite
                ? $"{sprite.rect.width:0} x {sprite.rect.height:0}   {file.SizeText}"
                : "This picture could not be read.";

            _prevButton.Interactable = index > 0;
            _nextButton.Interactable = index < _photos.Count - 1;

            _panels.Show(1);
            Raise("view", file.Path);
            SetStatus();
        }

        void ShowLibrary()
        {
            _panels.Show(0);
            SetStatus();
        }

        void Next() { if (_shown < _photos.Count - 1) Show(_shown + 1); }
        void Previous() { if (_shown > 0) Show(_shown - 1); }

        void OpenWith()
        {
            var file = Current;
            if (file != null) Os.OpenFile(file);
        }

        void SetAsWallpaper()
        {
            var file = Current;
            if (PictureOf(file) is not Sprite sprite)
            {
                _status.Set(0, "Open a picture first.");
                return;
            }

            if (!Desktop.DesktopScreen.SetWallpaper(Os, sprite))
            {
                _status.Set(0, "No desktop to change.");
                return;
            }

            _status.Set(0, $"'{file.Name}' is the wallpaper.");
            Raise("wallpaper", file.Path);
        }

        // Deleting a photo deletes the file, because in this OS a photo is a
        // file - there is no library to remove it from.
        void Delete()
        {
            var file = Current;
            if (file == null) { _status.Set(0, "Open a picture first."); return; }

            if (string.IsNullOrWhiteSpace(file.SourcePath))
            {
                RetroDialog.Alert(Os.Overlay, "Delete", $"'{file.Name}' cannot be deleted.");
                return;
            }

            RetroDialog.Confirm(Os.Overlay, "Delete",
                $"Delete '{file.Name}'?\n\nThis cannot be undone.", ok =>
                {
                    if (!ok) return;

                    var result = Core.Storage.DiskOperations.Delete(file.SourcePath);
                    if (!result.Ok)
                    {
                        RetroDialog.Alert(Os.Overlay, "Delete", result.Message);
                        return;
                    }

                    Os.Drives.Refresh();
                    Os.NotifyFsChanged(file.Path, FsChange.Deleted);
                    Raise("deleted", file.Path);
                });
        }

        // Access check: a sealed photo must not be legible at 64px either.
        Sprite PictureOf(VfsFile file) =>
            file != null && file.TryLoad(Os.CurrentUser, out var asset) ? asset as Sprite : null;

        void SetStatus()
        {
            if (_status == null) return;

            var pages = Mathf.Max(1, Mathf.CeilToInt(_photos.Count / (float)PageSize));
            _status.Set(0, Viewing && Current != null
                ? Current.Path
                : pages > 1
                    ? $"{_photos.Count} photo(s) - page {_page + 1} of {pages}"
                    : $"{_photos.Count} photo(s)");
            _status.Set(1, Viewing ? $"{_shown + 1} of {_photos.Count}" : "Library");
        }

        // Arrow keys page the viewer - only the top window, never while a text field has the keyboard.
        void Update()
        {
            if (!Viewing || !HasKeyboard) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.rightArrowKey.wasPressedThisFrame) Next();
            else if (keyboard.leftArrowKey.wasPressedThisFrame) Previous();
            else if (keyboard.escapeKey.wasPressedThisFrame) ShowLibrary();
        }

        void OnDestroy()
        {
            if (Os != null) Os.FsChanged -= OnFsChanged;
        }
    }
}
