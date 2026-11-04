using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.UI;
using Deepwave.RetroOS.Apps;
using Deepwave.RetroOS.UI;
using Deepwave.RetroOS.UI.Components;

namespace Deepwave.RetroOS.Desktop
{
    // Wallpaper, icon grid and the taskbar. Destroyed on logout.
    public class DesktopScreen : MonoBehaviour
    {
        Taskbar _taskbar;
        RetroOsContext _os;
        RectTransform _icons;
        Image _wallpaper;

        public static DesktopScreen Create(RetroOsContext os, RectTransform background, RectTransform taskbarLayer)
        {
            var root = RetroUi.NewRect("Desktop", background);
            RetroUi.Stretch(root);
            var screen = root.gameObject.AddComponent<DesktopScreen>();
            screen.Build(os, taskbarLayer);
            return screen;
        }

        /// <summary>Hangs a picture on the desktop, or clears it back to the scheme's desktop colour. This is how the Control Panel changes wallpaper.</summary>
        /// <returns><c>false</c> when no desktop is up - during logon, say.</returns>
        public static bool SetWallpaper(RetroOsContext os, Sprite sprite)
        {
            var desktop = os?.Root ? os.Root.GetComponentInChildren<DesktopScreen>(true) : null;
            if (!desktop || !desktop._wallpaper) return false;

            desktop._wallpaper.sprite = sprite;
            // Painted colour, not white: the retint pass reads the desktop colour, and white
            // reads as the bevel highlight. A wallpaper picture is tinted white so it shows through;
            // with no picture the desktop's own colour follows the scheme.
            if (sprite) RetroPalette.PaintOff(desktop._wallpaper, Color.white);
            else RetroPalette.Paint(desktop._wallpaper, PaletteSlot.Desktop);
            desktop._wallpaper.type = Image.Type.Simple;
            desktop._wallpaper.preserveAspect = false;
            return true;
        }

        // The desktop can resize under the icons (game view, resolution); a cell that no longer exists is off the edge.
        void OnRectTransformDimensionsChange() => DesktopIcon.KeepOnScreen(_icons);

        void Build(RetroOsContext os, RectTransform taskbarLayer)
        {
            var wallpaper = RetroUi.Panel(transform, PaletteSlot.Desktop, "Wallpaper");
            RetroUi.Stretch(wallpaper.rectTransform);
            _wallpaper = wallpaper;
            if (os.Wallpaper)
            {
                wallpaper.sprite = os.Wallpaper;
                RetroPalette.PaintOff(wallpaper, Color.white);
            }

            _os = os;

            var icons = RetroUi.NewRect("Icons", transform);
            RetroUi.Stretch(icons);
            icons.offsetMin = new Vector2(0, RetroSkin.TaskbarHeight);
            _icons = icons;

            var shortcuts = new List<IRetroApp>();
            var programs = new List<(string Id, string Title)>();
            foreach (var app in os.Apps.All)
            {
                if (!app.ShowOnDesktop) continue;
                shortcuts.Add(app);
                programs.Add((app.Id, app.Title));
            }

            // The whole grid is decided before the first icon exists. Placing them
            // one at a time dropped a program with no remembered cell straight on
            // top of one that had that cell written down.
            var (cols, rows) = IconGrid(icons);
            var cells = DesktopLayout.Plan(programs, cols, rows, os.Prefs);

            for (var i = 0; i < shortcuts.Count; i++)
            {
                var a = shortcuts[i];
                DesktopIcon.Create(icons, a.IconSprite, a.Id, a.Title, () => os.Apps.Launch(a),
                    cells[i].Col, cells[i].Row, IconMenu, os.Prefs);
            }

            _taskbar = Taskbar.Create(os, taskbarLayer);

            // Clicking bare desktop clears selection and closes the Start menu.
            var click = wallpaper.gameObject.AddComponent<Button>();
            click.transition = Selectable.Transition.None;
            click.onClick.AddListener(() =>
            {
                DesktopIcon.DeselectAll(_icons);
                _taskbar.CloseStart();
            });

            RetroUi.ContextMenu(wallpaper, DesktopMenu);
        }

        // Measured off the icon area rather than the desktop, because that is the
        // rect an icon is dragged around in: two ideas of how many rows there are
        // means a row the layout uses and a drop can never reach.
        static (int Cols, int Rows) IconGrid(RectTransform icons)
        {
            // Built this frame, so uGUI may not have laid it out yet; a rect with
            // no size is assumed to be the usual screen.
            var width = icons.rect.width;
            var height = icons.rect.height;
            if (width < 100) width = 1024;
            if (height < 100) height = 768 - RetroSkin.TaskbarHeight;

            return DesktopIcon.GridOf(width, height);
        }

        // Right-clicking bare desktop.
        RetroMenu DesktopMenu()
        {
            var arrange = new RetroMenu()
                .Command("by Name", () => DesktopIcon.Arrange(_icons,
                    (a, b) => string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase)))
                .Command("Auto Arrange", () => DesktopIcon.Arrange(_icons));

            return new RetroMenu()
                .Submenu("Arrange Icons", arrange)
                .Command("Line up Icons", () => DesktopIcon.LineUp(_icons))
                .Separator()
                .Command("Refresh", Refresh, null, RetroIcons.Refresh)
                .Separator()
                .Command("Properties", OpenDisplayProperties, null, RetroIcons.Settings);
        }

        // Right-clicking a shortcut.
        RetroMenu IconMenu(DesktopIcon icon) =>
            new RetroMenu()
                .Command("Open", icon.Open, null, RetroIcons.FolderOpen)
                .Separator()
                .Command("Properties", OpenDisplayProperties, null, RetroIcons.Info);

        // Re-reads the file system like F5 in 95. Names the root, not "desktop" - which was not a path and no listener trusted.
        void Refresh() => _os.NotifyFsChanged("/", Events.FsChange.Mounted);

        // Desktop properties lived in Control Panel; open it when installed.
        void OpenDisplayProperties()
        {
            if (_os.Apps.Find("settings") != null) _os.Apps.Launch("settings");
        }

        void OnDestroy()
        {
            if (_taskbar) Destroy(_taskbar.gameObject);
        }
    }
}
