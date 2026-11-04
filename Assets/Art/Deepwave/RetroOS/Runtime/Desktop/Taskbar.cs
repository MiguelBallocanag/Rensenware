using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Deepwave.RetroOS.UI;
using Deepwave.RetroOS.UI.Components;
using Deepwave.RetroOS.Windowing;

namespace Deepwave.RetroOS.Desktop
{
    // Win95 bottom bar: Start menu, window buttons, sunken clock tray.
    public class Taskbar : MonoBehaviour
    {
        class Entry
        {
            public Image Bg;
            public RetroBevel Bevel;
            public Text Label;
        }

        readonly Dictionary<Window, Entry> _entries = new();
        RetroOsContext _os;
        RectTransform _strip;
        Text _clock;
        StartMenu _startMenu;
        Window _active;
        int _shownMinute = -1;

        public static Taskbar Create(RetroOsContext os, RectTransform layer)
        {
            var bar = RetroUi.Panel(layer, PaletteSlot.Face, "Taskbar");
            // Top edge only: 95 drew nothing on the display edges, and a full bevel put seams down three sides of the screen.
            RetroUi.Bevel(bar.rectTransform, true, 2f, RetroUi.BevelEdges.Top);
            var rt = bar.rectTransform;
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(0, RetroSkin.TaskbarHeight);

            var taskbar = bar.gameObject.AddComponent<Taskbar>();
            taskbar.Init(os, bar);
            return taskbar;
        }

        void Init(RetroOsContext os, Image bar)
        {
            _os = os;
            RetroUi.HLayout(bar.gameObject, 4, 4);

            var start = RetroUi.Button(bar.transform, "Start", ToggleStart, RetroIcons.Flag);
            start.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
            RetroUi.Size(start, w: 66);

            _strip = RetroUi.NewRect("Windows", bar.transform);
            RetroUi.Flexible(_strip);
            RetroUi.HLayout(_strip.gameObject, 3, 0);

            var tray = RetroUi.Panel(bar.transform, PaletteSlot.Face, "Tray");
            RetroUi.Size(tray, w: 70);
            RetroUi.Bevel(tray.rectTransform, false, 1);
            _clock = RetroUi.Label(tray.transform, "", 13, TextAnchor.MiddleCenter);
            RetroUi.Stretch(_clock.rectTransform);

            _startMenu = StartMenu.Create(_os);

            RetroUi.ContextMenu(bar, TaskbarMenu);
            RetroUi.ContextMenu(tray, TaskbarMenu);

            _os.Windows.Opened += AddWindow;
            _os.Windows.Closed += RemoveWindow;
            _os.Windows.Focused += StyleActive;

            // Switching to a 12-hour clock has to show up now, not at the top
            // of the next minute.
            _os.Clock.Changed += RewriteClock;
        }

        void RewriteClock()
        {
            _shownMinute = -1;
            if (_clock) _clock.text = _os.Clock.ShortTime;
        }

        void ToggleStart() => _startMenu.Toggle();

        // Right-clicking the bar - the 95 window-management commands.
        RetroMenu TaskbarMenu() =>
            new RetroMenu()
                .Command("Cascade", Cascade)
                .Command("Tile Horizontally", () => Tile(horizontal: true))
                .Command("Tile Vertically", () => Tile(horizontal: false))
                .Separator()
                .Command("Minimize All Windows", MinimizeAll)
                .Separator()
                .Command("Properties", () =>
                {
                    if (_os.Apps.Find("settings") != null) _os.Apps.Launch("settings");
                }, null, RetroIcons.Settings);

        // Windows that can actually be arranged: open and not minimized.
        List<Window> Arrangeable()
        {
            var list = new List<Window>();
            foreach (var window in _os.Windows.Windows)
                if (window && !window.Minimized) list.Add(window);
            return list;
        }

        void Cascade()
        {
            var windows = Arrangeable();
            if (windows.Count == 0) return;

            var area = _os.Windows.Layer.rect.size;
            var size = new Vector2(area.x * 0.6f, (area.y - RetroSkin.TaskbarHeight) * 0.6f);
            var step = RetroSkin.TitleBarHeight + 4f;
            var start = new Vector2(-area.x * 0.5f + size.x * 0.5f + 8f,
                                     area.y * 0.5f - size.y * 0.5f - 8f);

            for (var i = 0; i < windows.Count; i++)
            {
                windows[i].SetBounds(start + new Vector2(i * step, -i * step), size);
                _os.Windows.Focus(windows[i]);
            }
        }

        void Tile(bool horizontal)
        {
            var windows = Arrangeable();
            if (windows.Count == 0) return;

            var area = _os.Windows.Layer.rect.size;
            var usable = new Vector2(area.x, area.y - RetroSkin.TaskbarHeight);
            var columns = horizontal ? 1 : windows.Count;
            var rows = horizontal ? windows.Count : 1;
            var size = new Vector2(usable.x / columns, usable.y / rows);

            for (var i = 0; i < windows.Count; i++)
            {
                var col = horizontal ? 0 : i;
                var row = horizontal ? i : 0;
                var centre = new Vector2(
                    -area.x * 0.5f + size.x * (col + 0.5f),
                     area.y * 0.5f - size.y * (row + 0.5f));
                windows[i].SetBounds(centre, size);
            }
        }

        void MinimizeAll()
        {
            foreach (var window in Arrangeable()) window.Minimize();
            StyleActive(null);
        }

        public void CloseStart() => _startMenu.Close();

        void AddWindow(Window window)
        {
            var button = RetroUi.Button(_strip, "", null, window.Icon);

            // Buttons shrink as more windows open rather than pushing the last off the bar, like the original.
            RetroUi.SoftSize(button, 150f, 40f);
            button.onClick.AddListener(() => OnTaskClick(window));

            // The caption is a window title of the program's choosing, so it goes in a masked rect clipped where the button ends.
            var clip = RetroUi.Clipped(button.transform);
            clip.offsetMin = new Vector2(24, 0);
            clip.offsetMax = new Vector2(-4, 0);
            var label = RetroUi.NoWrap(
                RetroUi.Label(clip, window.Title, 13, TextAnchor.MiddleLeft));
            RetroUi.Stretch(label.rectTransform);

            _entries[window] = new Entry
            {
                Bg = (Image)button.targetGraphic,
                Bevel = button.GetComponent<RetroBevel>(),
                Label = label
            };
        }

        void OnTaskClick(Window window)
        {
            if (window.Minimized)
            {
                window.Restore();
                _os.Windows.Focus(window);
            }
            else if (_active == window)
            {
                window.Minimize();
                StyleActive(null);
            }
            else
            {
                _os.Windows.Focus(window);
            }
        }

        void RemoveWindow(Window window)
        {
            if (!_entries.TryGetValue(window, out var entry)) return;
            Destroy(entry.Bg.gameObject);
            _entries.Remove(window);
            if (_active == window) _active = null;
        }

        void StyleActive(Window window)
        {
            _active = window;
            foreach (var pair in _entries)
            {
                var on = pair.Key == window && !pair.Key.Minimized;

                // Sunk, bold, on the dithered face 95 gave a pressed taskbar button - a checkerboard
                // of face and highlight, not a fill of the highlight (which is white in every scheme and would hide the caption on High Contrast Black).
                pair.Value.Bevel.SetRaised(!on);
                RetroPalette.Paint(pair.Value.Bg, PaletteSlot.Face);
                pair.Value.Bg.sprite = on ? RetroTextures.Checker : null;
                pair.Value.Bg.type = Image.Type.Tiled;
                pair.Value.Label.fontStyle = on ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        // The tray clock ticks on the in-fiction system clock; update it only when the minute rolls over.
        void Update()
        {
            var now = _os.Clock.Now;
            var minute = now.Hour * 60 + now.Minute;
            if (minute == _shownMinute) return;
            _shownMinute = minute;
            _clock.text = _os.Clock.ShortTime;
        }

        void OnDestroy()
        {
            // The window manager outlives the taskbar (rebuilt on every login), so unsubscribe or the next window talks to a destroyed strip.
            if (_os?.Windows != null)
            {
                _os.Windows.Opened -= AddWindow;
                _os.Windows.Closed -= RemoveWindow;
                _os.Windows.Focused -= StyleActive;
            }

            if (_os?.Clock != null)
            {
                _os.Clock.Changed -= RewriteClock;
            }

            if (_startMenu) Destroy(_startMenu.gameObject);
        }
    }
}
