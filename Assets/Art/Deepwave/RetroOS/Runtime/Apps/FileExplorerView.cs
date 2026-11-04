using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Deepwave.RetroOS.Core.FileSystem;
using Deepwave.RetroOS.Core.Storage;
using Deepwave.RetroOS.Events;
using Deepwave.RetroOS.UI;
using Deepwave.RetroOS.UI.Components;

namespace Deepwave.RetroOS.Apps
{
    // Explorer: a folder tree left, a file list right, a working menu bar and toolbar,
    // three view modes, a right-click menu. Browses the whole machine (disk, LAN,
    // inserted discs) and refreshes on every file-system change.
    public class FileExplorerView : AppView
    {
        VfsDirectory _current;
        string _currentPath = "/";
        bool _showHidden;
        bool _showTree = true;
        RectTransform _treePane;
        GameObject _splitter;
        RetroTreeView _tree;
        RetroListView _list;
        RetroTextBox _path;
        RetroStatusStrip _status;

        protected override void Build()
        {
            _current = Os.Fs.Root;
            _showTree = Os.Prefs.GetBool("explorer.folders", true);

            RetroUi.VLayout(Root.gameObject, 2, 2);
            BuildMenu();
            BuildToolbar();

            var (left, right) = RetroUi.Split(Root, vertical: false, fraction: 0.32f);
            RetroUi.Flexible(((RectTransform)left.parent));

            // The tree can be put away (the Folders button); the divider goes with it.
            _treePane = left;
            var bar = left.parent.GetComponentInChildren<RetroSplitter>(true);
            _splitter = bar ? bar.gameObject : null;

            _tree = RetroUi.TreeView(left);
            RetroUi.Stretch(_tree.Rect);
            _tree.Activated += node =>
            {
                if (node.Tag is VfsDirectory dir) Navigate(dir);
            };

            _list = RetroUi.ListView(right, ListViewMode.Details);
            RetroUi.Stretch(_list.Rect);
            // Type fits the package's own longest stock caption, no wider - Name is the flexible column.
            _list.SetColumns(new[] { "Name", "Size", "Type", "Modified" },
                new[] { -1f, 70f, 96f, 110f });
            _list.Activated += entry => Activate(entry.Tag as VfsNode);
            _list.SelectionChanged += entry => ShowSelection(entry.Tag as VfsNode);
            _list.ContextRequested += (entry, position) => ShowContextMenu(entry.Tag as VfsNode, position);

            _status = RetroUi.StatusStrip(Root, -1f, 130f);

            Os.FsChanged += OnFsChanged;
            Os.Drives.Changed += OnDrivesChanged;
            ShowTree(_showTree);
            RebuildTree();
            Refresh();
        }

        void BuildMenu()
        {
            var menu = RetroUi.Menu(Root);

            menu.AddMenu("File")
                .Command("Open", () => Activate(Selected()), "Enter")
                .Command("Explore Folder", () => { if (Selected() is VfsDirectory d) Navigate(d); })
                .Separator()
                .Command("New Folder", NewFolder, null, RetroIcons.Get(BuiltinIcon.Folder))
                .Command("New Text Document", NewTextFile, null, RetroIcons.Note)
                .Separator()
                .Command("Rename", () => Rename(Selected()), "F2")
                .Command("Delete", () => Delete(Selected()), "Del",
                    RetroIcons.Get(BuiltinIcon.Trash))
                .Separator()
                .Command("Close", () => Window.Close(), "Alt+F4");

            menu.AddMenu("Edit")
                .Command("Cut", () => Hold(Selected(), cut: true), "Ctrl+X")
                .Command("Copy", () => Hold(Selected(), cut: false), "Ctrl+C")
                .Command("Paste", Paste, "Ctrl+V");

            menu.AddMenu("View")
                .Command("Large Icons", () => _list.Mode = ListViewMode.Icons, null,
                    RetroIcons.Get(BuiltinIcon.Photos))
                .Command("List", () => _list.Mode = ListViewMode.List)
                .Command("Details", () => SetDetails(), null, RetroIcons.Get(BuiltinIcon.Chart))
                .Separator()
                .Check("Folders", _showTree, () => ShowTree(!_showTree))
                .Check("Show hidden files", _showHidden, ToggleHidden)
                .Command("Refresh", Refresh, "F5", RetroIcons.Refresh);

            var go = menu.AddMenu("Go");
            go.Command("Up One Level", GoUp, "Backspace")
              .Command("Root", () => Navigate(Os.Fs.Root), null, RetroIcons.Drive);

            menu.AddMenu("Help").Command("About", () => RetroDialog.Alert(Os.Overlay, "Explorer",
                "Browses this machine, the network and any inserted disc.\n\n" +
                "Files are the real folders inside this machine's own folder " +
                "in the RetroOS data folder."));
        }

        void SetDetails()
        {
            _list.Mode = ListViewMode.Details;
            _list.SetColumns(new[] { "Name", "Size", "Type", "Modified" },
                new[] { -1f, 70f, 90f, 120f });
            Refresh();
        }

        void BuildToolbar()
        {
            var bar = RetroUi.ToolStrip(Root);
            bar.Add(RetroIcons.Get(BuiltinIcon.FolderOpen), "Up one level", GoUp);
            bar.Add(RetroIcons.Refresh, "Refresh", Refresh);
            bar.AddSeparator();
            bar.Add(RetroIcons.Get(BuiltinIcon.Book), "Folders", () => ShowTree(!_showTree));
            bar.Add(RetroIcons.Get(BuiltinIcon.Folder), "New folder", NewFolder);
            bar.Add(RetroIcons.Get(BuiltinIcon.Trash), "Delete", () => Delete(Selected()));
            bar.AddSeparator();
            bar.AddToggle(RetroIcons.Get(BuiltinIcon.Eye), "Show hidden files", _showHidden,
                on => { _showHidden = on; Refresh(); });
            bar.AddSeparator();

            RetroUi.Size(RetroUi.NoWrap(
                RetroUi.Label(bar.transform, "Address", 13, TextAnchor.MiddleLeft)), w: 52f);
            _path = RetroUi.TextBox(bar.transform, "/");
            RetroUi.Flexible(_path);
            _path.Submitted += JumpTo;
        }

        // Remembered per machine, so a player who works without the tree gets
        // their window back the way they left it.
        void ShowTree(bool show)
        {
            _showTree = show;
            if (_treePane) _treePane.gameObject.SetActive(show);
            if (_splitter) _splitter.SetActive(show);
            Os.Prefs.SetBool("explorer.folders", show);
            Os.Prefs.Save();
        }

        void ToggleHidden()
        {
            _showHidden = !_showHidden;
            Refresh();
        }

        void GoUp()
        {
            _current = _current.Parent ?? _current;
            Navigate(_current);
        }

        void JumpTo(string path)
        {
            var node = Os.Fs.Resolve(path);
            if (node != null && !Os.CanAccess(node))
            {
                _status.Set(0, "Access denied - '" + node.Name + "' belongs to " + node.Owner);
                return;
            }
            if (node is VfsDirectory dir) { Navigate(dir); return; }
            if (node is VfsFile file) { Os.OpenFile(file); return; }
            _status.Set(0, "Cannot find '" + path + "'");
        }

        void Navigate(VfsDirectory dir)
        {
            _current = dir;
            Os.Events.Raise(OsEvents.ExplorerNavigated, dir.Path);
            Raise("navigated", dir.Path);
            Refresh();
        }

        VfsNode Selected() => _list?.Selection?.Tag as VfsNode;

        // --- tree ----------------------------------------------------------

        // Rebuilt on every fs.changed; the open branches are carried across so it does not collapse to root.
        void RebuildTree()
        {
            var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (_tree != null)
                foreach (var node in _tree.Roots) CollectExpanded(node, expanded);

            var root = NodeFor(Os.Fs.Root, "This Computer", RetroIcons.Get(BuiltinIcon.Computer));
            root.Expanded = true;
            root.Resolve();
            RestoreExpanded(root, expanded);
            _tree.SetRoots(new[] { root });
        }

        static void CollectExpanded(RetroTreeNode node, HashSet<string> into)
        {
            if (node == null || !node.Expanded) return;
            if (node.Tag is VfsDirectory dir && !string.IsNullOrEmpty(dir.Path)) into.Add(dir.Path);
            if (node.Children == null) return;
            foreach (var child in node.Children) CollectExpanded(child, into);
        }

        static void RestoreExpanded(RetroTreeNode node, HashSet<string> expanded)
        {
            if (node?.Children == null) return;
            foreach (var child in node.Children)
            {
                if (child.Tag is not VfsDirectory dir || !expanded.Contains(dir.Path)) continue;
                child.Expanded = true;
                child.Resolve();
                RestoreExpanded(child, expanded);
            }
        }

        RetroTreeNode NodeFor(VfsDirectory dir, string caption = null, Sprite icon = null)
        {
            var resolved = icon ? icon : IconFor(dir);
            var node = new RetroTreeNode
            {
                Text = caption ?? Caption(dir),
                Icon = resolved,
                OpenIcon = icon ? icon : (resolved == RetroIcons.Folder ? RetroIcons.FolderOpen : resolved),
                Tag = dir
            };
            // Children are read the first time the branch is opened, so a disk
            // with thousands of folders costs nothing until it is explored.
            node.LoadChildren = () => Os.List(dir, _showHidden)
                .OfType<VfsDirectory>()
                .Select(child => NodeFor(child))
                .ToList();
            return node;
        }

        // --- list ----------------------------------------------------------

        // Explorer shows the whole machine, so a mount rebuilds both halves; other views redraw only the current folder.
        void OnFsChanged(FsChangedArgs change)
        {
            if (!this || _list == null) return;

            if (change.Kind is FsChange.Mounted or FsChange.Unmounted)
            {
                RebuildTree();
                Refresh();
                return;
            }

            if (change.Touches(_currentPath)) Refresh();
        }

        void Refresh()
        {
            if (!this || _list == null) return;

            // A remount detaches the current folder, so it is re-found by path (root only if it is really gone).
            // The story's own tree changes live in NodeStateStore, off the tree, safe from a remount.
            if (Orphaned(_current))
                _current = Os.Fs.Resolve(_currentPath) as VfsDirectory ?? Os.Fs.Root;

            _currentPath = _current.Path;

            _path.Value = _current.Path;
            _list.Clear();

            var folders = 0;
            var files = 0;
            long bytes = 0;

            foreach (var node in Os.List(_current, _showHidden)
                         .OrderBy(n => n is VfsFile)
                         .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase))
            {
                var entry = new RetroListEntry
                {
                    Text = Caption(node) + (node.Locked ? "  (locked)" : ""),
                    Icon = IconFor(node),
                    Tag = node,
                    TextColor = node.Hidden ? RetroSkin.TextDisabled : (Color?)null
                };

                if (node is VfsFile file)
                {
                    files++;
                    bytes += file.Size;
                    entry.Columns = new[]
                    {
                        file.SizeText,
                        TypeName(file),
                        file.Modified == default ? "" : file.Modified.ToString("yyyy-MM-dd HH:mm")
                    };
                }
                else
                {
                    folders++;
                    var drive = Os.Drives.Drives.FirstOrDefault(d => d.Root == node);
                    var share = SharedAs(node);
                    entry.Columns = drive != null
                        ? new[] { "", DriveTypeName(drive), drive.Ready ? "" : "no disk" }
                        : new[] { "", share != null ? "Shared Folder" : FolderTypeName(node), "" };

                    // Only where there is no Type column to carry it (appending to Name in Details pushed the name out).
                    if (share != null && _list.Mode != ListViewMode.Details)
                        entry.Text += $"  (shared as {share})";
                }

                _list.Add(entry);
            }

            _list.ScrollToTop();
            _status.Set(0, $"{folders + files} object(s)   {folders} folder(s), {files} file(s)");
            _status.Set(1, bytes > 0 ? SizeText(bytes) : "");
        }

        // Volume label, share comment or file name - whatever reads best.
        static string Caption(VfsNode node) => node.DisplayName;

        static string FolderTypeName(VfsNode node) => node.Role switch
        {
            NodeRole.MyDocuments => "System Folder",
            NodeRole.Documents => "Documents",
            NodeRole.Pictures => "Pictures",
            NodeRole.Desktop => "Desktop",
            NodeRole.UserRoot => "User Accounts",
            NodeRole.UserHome => "User Folder",
            NodeRole.NetworkRoot => "Network",
            NodeRole.NetworkHost => "Computer",
            NodeRole.Share => "Shared Folder",
            _ => "File Folder"
        };

        static string DriveTypeName(RetroDrive drive) => drive.Kind switch
        {
            // The fraction, the way the original wrote it - and two characters
            // shorter than 3.5", which is what lets the column fit it.
            DriveKind.Floppy => "3½ Floppy Disk",
            DriveKind.CdRom => "CD-ROM Drive",
            DriveKind.Network => "Network Drive",
            DriveKind.Removable => "Removable Disk",
            _ => "Local Disk"
        };

        static string SizeText(long bytes) =>
            bytes < 1024 ? bytes + " bytes"
            : bytes < 1024 * 1024 ? (bytes / 1024f).ToString("0.#") + " KB"
            : (bytes / 1048576f).ToString("0.#") + " MB";

        static string TypeName(VfsFile file) => file.Kind switch
        {
            FileKind.Text => "Text Document",
            FileKind.Web => "HTML Document",
            FileKind.Image => "Image",
            FileKind.Audio => "Sound",
            FileKind.Video => "Video Clip",
            FileKind.Executable => "Application",
            FileKind.Binary => string.IsNullOrEmpty(file.Extension)
                ? "File" : file.Extension.ToUpperInvariant() + " File",
            _ => "File"
        };

        // Whether this folder is still part of the machine's tree, or a
        // leftover from before the last remount.
        bool Orphaned(VfsDirectory dir)
        {
            for (var node = (VfsNode)dir; node != null; node = node.Parent)
                if (node == Os.Fs.Root) return false;
            return true;
        }

        Sprite IconFor(VfsNode node)
        {
            if (node is VfsDirectory dir)
            {
                // A drive gets the icon of the hardware it is pretending to be.
                var drive = Os.Drives.Drives.FirstOrDefault(d => d.Root == dir);
                if (drive != null) return DriveIcon(drive);

                return dir.Role switch
                {
                    NodeRole.MyDocuments or NodeRole.Documents => RetroIcons.Get(BuiltinIcon.Note),
                    NodeRole.Pictures => RetroIcons.Get(BuiltinIcon.Photos),
                    NodeRole.Desktop => RetroIcons.Get(BuiltinIcon.Monitor),
                    NodeRole.UserRoot => RetroIcons.Get(BuiltinIcon.Contacts),
                    NodeRole.UserHome => RetroIcons.Get(BuiltinIcon.Contacts),
                    NodeRole.System => RetroIcons.Get(BuiltinIcon.Settings),
                    NodeRole.Programs => RetroIcons.Get(BuiltinIcon.Chip),
                    NodeRole.NetworkRoot or NodeRole.NetworkHost => RetroIcons.Network,
                    NodeRole.Share => RetroIcons.Get(BuiltinIcon.Folder),
                    _ => RetroIcons.Folder
                };
            }

            var file = (VfsFile)node;
            if (file.Locked) return RetroIcons.Lock;
            return file.Kind switch
            {
                FileKind.Image => RetroIcons.Get(BuiltinIcon.Photos),
                FileKind.Audio => RetroIcons.Get(BuiltinIcon.Music),
                FileKind.Video => RetroIcons.Get(BuiltinIcon.Movie),
                FileKind.Web => RetroIcons.Get(BuiltinIcon.Globe),
                FileKind.Executable => RetroIcons.Get(BuiltinIcon.Chip),
                FileKind.Text => RetroIcons.Get(BuiltinIcon.Note),
                _ => RetroIcons.File
            };
        }

        static Sprite DriveIcon(RetroDrive drive) => drive.Kind switch
        {
            DriveKind.Floppy => RetroIcons.Get(BuiltinIcon.Floppy),
            DriveKind.CdRom => RetroIcons.Get(BuiltinIcon.CdRom),
            DriveKind.Network => RetroIcons.Network,
            DriveKind.Removable => RetroIcons.Get(BuiltinIcon.Floppy),
            _ => RetroIcons.Drive
        };

        void ShowSelection(VfsNode node)
        {
            if (node == null) return;

            if (node is VfsFile file)
            {
                _status.Set(0, $"{file.Name}   {TypeName(file)}   {file.SizeText}");
                return;
            }

            // The share name goes here rather than beside the folder: the
            // status bar is the one place in the window with room for it.
            var share = SharedAs(node);
            _status.Set(0, share == null ? node.Path : $"{node.Path}   shared as {share}");
        }

        void Activate(VfsNode node)
        {
            if (node == null) return;
            if (node.Locked) { Os.PromptUnlock(node, Refresh); return; }
            if (node is VfsDirectory dir) { Navigate(dir); return; }
            if (node is VfsFile file)
            {
                Os.OpenFile(file);
                Raise("opened", file.Path);
            }
        }

        void ShowContextMenu(VfsNode node, Vector2 position)
        {
            if (node == null) return;

            var menu = new RetroMenu();
            menu.Command("Open", () => Activate(node), null, RetroIcons.FolderOpen);

            if (node is VfsFile file)
            {
                menu.Command("Open With Notepad", () =>
                {
                    var notepad = Os.Apps.Find("notepad");
                    if (notepad != null) Os.Apps.Launch(notepad, file);
                    else Os.OpenFile(file);
                }, null, RetroIcons.Note);
            }

            if (node.Locked)
                menu.Command("Unlock...", () => Os.PromptUnlock(node, Refresh), null, RetroIcons.Get(BuiltinIcon.Key));

            menu.Separator()
                .Command("Cut", () => Hold(node, cut: true))
                .Command("Copy", () => Hold(node, cut: false))
                .Command("Paste", Paste, null, null, _clipboard != null)
                .Separator()
                .Command("Rename", () => Rename(node), "F2")
                .Command("Delete", () => Delete(node), "Del", RetroIcons.Get(BuiltinIcon.Trash))
                .Separator()
                .Check("Hidden", node.Hidden, () =>
                {
                    node.SetHidden(!node.Hidden);
                    Os.NotifyFsChanged(node.Path, FsChange.Revealed);
                })
                // Greyed rather than absent on a file or a LAN-mounted folder, so the player still sees sharing exists.
                .Command("Sharing...", () => ShowSharing(node), null,
                    RetroIcons.Network, ShareableFolder(node) != null)
                .Separator()
                .Command("Properties", () => ShowProperties(node), null, RetroIcons.Info);

            RetroMenuPopup.Show(menu.Items, _list.Rect, RetroMenuPopup.Placement.AtPointer,
                null, position);
        }

        // --- file operations ---
        // Each writes to the real folder behind the current node and re-reads disk. A
        // made-up folder (My Computer, the network branch) has none and refuses.

        string WritablePath(VfsDirectory dir) =>
            dir != null && !string.IsNullOrWhiteSpace(dir.SourcePath) ? dir.SourcePath : null;

        void NewFolder() => Create("New Folder", "Folder name:",
            (parent, name) => DiskOperations.CreateFolder(parent, name));

        void NewTextFile() => Create("New Text Document", "File name:",
            (parent, name) => DiskOperations.CreateFile(
                parent, name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ? name : name + ".txt"));

        void Create(string title, string prompt, Func<string, string, DiskResult> make)
        {
            var parent = WritablePath(_current);
            if (parent == null)
            {
                RetroDialog.Alert(Os.Overlay, title,
                    "Nothing can be created here.\n\nOpen a folder on a drive first.");
                return;
            }

            RetroDialog.Prompt(Os.Overlay, title, prompt, name => Apply(make(parent, name), title));
        }

        void Rename(VfsNode node)
        {
            if (node == null) return;
            if (string.IsNullOrWhiteSpace(node.SourcePath))
            {
                RetroDialog.Alert(Os.Overlay, "Rename", $"'{node.Name}' cannot be renamed.");
                return;
            }

            RetroDialog.Prompt(Os.Overlay, "Rename", $"New name for '{node.Name}':",
                name => Apply(DiskOperations.Rename(node.SourcePath, name), "Rename"));
        }

        void Delete(VfsNode node)
        {
            if (node == null) return;
            if (string.IsNullOrWhiteSpace(node.SourcePath))
            {
                RetroDialog.Alert(Os.Overlay, "Delete", $"'{node.Name}' cannot be deleted.");
                return;
            }

            // There is no recycle bin behind this, so it asks - and says so
            // when it is a folder, which takes everything inside with it.
            var warning = node is VfsDirectory
                ? $"Delete '{node.Name}' and everything in it?\n\nThis cannot be undone."
                : $"Delete '{node.Name}'?\n\nThis cannot be undone.";

            RetroDialog.Confirm(Os.Overlay, "Delete", warning, ok =>
            {
                if (!ok) return;
                Apply(DiskOperations.Delete(node.SourcePath), "Delete");
            });
        }

        void Hold(VfsNode node, bool cut)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.SourcePath))
            {
                _status.Set(0, "Nothing to " + (cut ? "cut" : "copy"));
                return;
            }

            _clipboard = node.SourcePath;
            _clipboardCut = cut;
            _status.Set(0, (cut ? "Cut " : "Copied ") + node.Name);
        }

        void Paste()
        {
            if (_clipboard == null) { _status.Set(0, "Nothing to paste"); return; }

            var into = WritablePath(_current);
            if (into == null)
            {
                RetroDialog.Alert(Os.Overlay, "Paste", "Files cannot be put here.");
                return;
            }

            var result = _clipboardCut
                ? DiskOperations.Move(_clipboard, into)
                : DiskOperations.Copy(_clipboard, into);

            // A cut is spent once it lands; a copy can be pasted again.
            if (result.Ok && _clipboardCut) _clipboard = null;
            Apply(result, "Paste");
        }

        // Reports what happened and puts the tree back in step with the disk.
        void Apply(DiskResult result, string title)
        {
            if (!result.Ok)
            {
                RetroDialog.Alert(Os.Overlay, title, result.Message);
                return;
            }

            Os.Drives.Refresh();
            Os.NotifyFsChanged(result.Path ?? _current?.Path, FsChange.Moved);
            _status.Set(0, title + " done");
            Raise(title.ToLowerInvariant(), result.Path);
        }

        string _clipboard;
        bool _clipboardCut;

        void ShowProperties(VfsNode node)
        {
            var file = node as VfsFile;
            var lines = new List<string>
            {
                "Name:      " + node.Name,
                "Location:  " + (node.Parent?.Path ?? "/"),
                "Type:      " + (file != null ? TypeName(file) : "File Folder")
            };
            if (file != null)
            {
                lines.Add("Size:      " + (string.IsNullOrEmpty(file.SizeText) ? "-" : file.SizeText));
                if (file.Modified != default) lines.Add("Modified:  " + file.Modified);
                if (!string.IsNullOrEmpty(file.SourcePath)) lines.Add("On disk:   " + file.SourcePath);
            }
            if (!string.IsNullOrEmpty(node.Owner)) lines.Add("Owner:     " + node.Owner);
            if (!string.IsNullOrEmpty(node.Note)) lines.Add("Note:      " + node.Note);
            if (node.Encrypted) lines.Add("Encrypted: yes" + (node.Locked ? " (locked)" : " (unlocked)"));

            var shareable = ShareableFolder(node);
            if (shareable != null)
            {
                var meta = RetroMeta.Read(shareable);
                lines.Add("Shared:    " + (meta.IsShared
                    ? meta.Share
                      + (meta.ShareHidden ? " (hidden)" : "")
                      + (string.IsNullOrEmpty(meta.SharePassword) ? "" : ", password protected")
                    : "not shared"));
            }

            RetroDialog.Alert(Os.Overlay, node.Name + " Properties", string.Join("\n", lines));
            Raise("properties", node.Path);
        }

        // --- sharing ---
        // A share lives in the folder's .rmeta sidecar, so this dialog writes what a
        // designer would and the LAN picks it up on re-read.

        /// <summary>
        /// The real folder behind a node when this machine may publish it, or
        /// <c>null</c>. Files and folders mounted from another computer are out.
        /// </summary>
        string ShareableFolder(VfsNode node)
        {
            if (node is not VfsDirectory || string.IsNullOrEmpty(node.SourcePath)) return null;

            var machine = Os.Net?.Self;
            if (!machine) return null;

            return RetroStorage.IsInside(machine.Folder, node.SourcePath) ? node.SourcePath : null;
        }

        // The name a folder is published under, or null. Only ever asked of
        // the folders on screen, so this is one sidecar read per visible row.
        string SharedAs(VfsNode node)
        {
            var folder = ShareableFolder(node);
            if (folder == null) return null;

            var meta = RetroMeta.Read(folder);
            return meta.IsShared ? meta.Share : null;
        }

        void ShowSharing(VfsNode node)
        {
            var folder = ShareableFolder(node);
            if (folder == null)
            {
                Debug.LogWarning($"RetroOS: '{node?.Name}' cannot be shared. Only a folder on " +
                                 "this machine can be published to the LAN - not a file, and " +
                                 "not a folder mounted from another computer.");
                RetroDialog.Alert(Os.Overlay, "Sharing",
                    "Only a folder on this computer can be shared.");
                return;
            }

            var meta = RetroMeta.Read(folder);
            // Tall enough for its contents (two radios, three boxes, the hidden check, the button row = 316). FitCheck warns if this drifts.
            var (blocker, body) = RetroDialog.Custom(Os.Overlay,
                node.Name + " Properties", 380f, 330f, BuiltinIcon.Network);

            var group = new RetroRadioGroup();
            RetroUi.Size(group.Add(body, "Not Shared"), h: 22f);
            RetroUi.Size(group.Add(body, "Shared As:"), h: 22f);

            var name = LabelledBox(body, "Share Name:",
                meta.IsShared ? meta.Share : RetroStorage.Sanitize(node.Name).ToUpperInvariant());
            var comment = LabelledBox(body, "Comment:", meta.ShareComment);
            var password = LabelledBox(body, "Password:", meta.SharePassword);

            var hidden = RetroUi.CheckBox(body, "Hidden - published, but not listed",
                meta.ShareHidden, null);
            RetroUi.Size(hidden, h: 22f);

            // The four controls below belong to the "Shared As" case, so they
            // follow the radio rather than sitting live under "Not Shared".
            group.SelectionChanged += selected =>
            {
                var on = selected == 1;
                name.Interactable = on;
                comment.Interactable = on;
                password.Interactable = on;
                hidden.Interactable = on;
            };
            group.Select(meta.IsShared ? 1 : 0);

            RetroUi.Spacer(body);

            var buttons = RetroUi.NewRect("Buttons", body);
            RetroUi.Size(buttons, h: 26f);
            RetroUi.HLayout(buttons.gameObject, 6, 0);
            RetroUi.Spacer(buttons);

            RetroUi.Size(RetroUi.PushButton(buttons, "OK", () =>
            {
                if (ApplySharing(node, folder, group.Selected == 1,
                        name.Value, comment.Value, password.Value, hidden.Checked))
                    RetroDialog.Dismiss(blocker);
            }), w: 76f);
            RetroUi.Size(RetroUi.PushButton(buttons, "Cancel",
                () => RetroDialog.Dismiss(blocker)), w: 76f);
        }

        // A caption above a box, the way every properties page of the era did.
        static RetroTextBox LabelledBox(Transform body, string caption, string value)
        {
            RetroUi.Size(RetroUi.NoWrap(
                RetroUi.Label(body, caption, 12, TextAnchor.LowerLeft)), h: 16f);
            var box = RetroUi.TextBox(body, value ?? "");
            RetroUi.Size(box, h: 24f);
            return box;
        }

        /// <returns>Whether the sidecar was written, so the dialog can stay
        /// open on the answer the player still has to fix.</returns>
        bool ApplySharing(VfsNode node, string folder, bool share, string name, string comment,
            string password, bool hidden)
        {
            var trimmed = name?.Trim();
            if (share && string.IsNullOrEmpty(trimmed))
            {
                RetroDialog.Alert(Os.Overlay, "Sharing", "A shared folder needs a name.");
                return false;
            }

            var meta = RetroMeta.Read(folder);
            meta.Share = share ? trimmed : null;
            meta.ShareComment = share ? comment?.Trim() : null;
            meta.SharePassword = share ? password : null;
            meta.ShareHidden = share && hidden;

            // Write already logs why it failed; this says what the player lost
            // by it, which is the part they can do something about.
            if (!meta.Write(folder))
            {
                RetroDialog.Alert(Os.Overlay, "Sharing",
                    $"'{node.Name}' could not be saved, so its sharing is unchanged.");
                return false;
            }

            // The machine re-reads what it publishes, and the network tells
            // every other computer browsing it.
            Os.Net?.Self?.Notify();
            Os.NotifyFsChanged(node.Path, FsChange.Mounted);
            Refresh();
            Raise("shared", node.Path);

            _status.Set(0, share
                ? $"{node.Name} shared as {trimmed}"
                : $"{node.Name} is no longer shared");
            return true;
        }

        void OnDrivesChanged()
        {
            if (!this) return;
            RebuildTree();
            Refresh();
        }

        void OnDestroy()
        {
            if (Os != null) Os.FsChanged -= OnFsChanged;
            if (Os?.Drives != null) Os.Drives.Changed -= OnDrivesChanged;
        }
    }
}
