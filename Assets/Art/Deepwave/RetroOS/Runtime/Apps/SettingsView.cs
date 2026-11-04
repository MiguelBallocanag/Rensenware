using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Deepwave.RetroOS.Core.Storage;
using Deepwave.RetroOS.UI;
using Deepwave.RetroOS.UI.Components;

namespace Deepwave.RetroOS.Apps
{
    // Control Panel: the in-fiction settings program, and the fastest way to see every
    // kit widget working at once. Sounds drives the audio module, Network lists
    // reachable machines, System reports where files live on disk.
    public class SettingsView : AppView
    {
        /// <summary>Where the picked colour scheme is remembered.</summary>
        public const string SchemeKey = "ui.scheme";

        /// <summary>Where the 12/24-hour choice is remembered.</summary>
        public const string ClockKey = "ui.clock24";

        /// <summary>Where the chosen wallpaper's path is remembered.</summary>
        public const string WallpaperKey = "ui.wallpaper";

        /// <summary>Where the interface-sounds switch is remembered.</summary>
        public const string SoundsKey = "ui.sounds";

        /// <summary>Where whole-number interface scaling is remembered.</summary>
        public const string PixelPerfectKey = "ui.pixelperfect";

        /// <summary>Where the three volumes are remembered.</summary>
        public const string MasterVolumeKey = "sound.master";
        public const string UiVolumeKey = "sound.ui";
        public const string MusicVolumeKey = "sound.music";

        /// <summary>Where the drawn pointer's speed is remembered.</summary>
        public const string PointerSpeedKey = "mouse.speed";

        /// <summary>Where the gamepad stick's pointer speed is remembered.</summary>
        public const string StickSpeedKey = "mouse.stick";

        /// <summary>Where the double-click window is remembered.</summary>
        public const string DoubleClickKey = "mouse.doubleclick";

        RetroStatusStrip _status;

        // A slider writes on every frame it is dragged, and machine.prefs is a
        // real file. Held for a moment and written once, so a sweep across the
        // volume bar is one write rather than sixty.
        float _saveAt;
        bool _saving;

        protected override void Build()
        {
            RetroUi.VLayout(Root.gameObject, 2, 2);

            var tabs = RetroUi.Tabs(Root);
            RetroUi.Flexible(tabs);

            BuildAppearance(Page(tabs, "Appearance"));
            BuildSound(Page(tabs, "Sounds"));
            BuildMouse(Page(tabs, "Mouse"));
            BuildDateTime(Page(tabs, "Date/Time"));
            BuildAccounts(Page(tabs, "Passwords"));
            BuildNetwork(Page(tabs, "Network"));
            BuildSystem(Page(tabs, "System"));

            _status = RetroUi.StatusStrip(Root);
            _status.Set(0, "Ready");
            tabs.Select(0);
        }

        // Each page scrolls: a properties page whose last option is off the
        // bottom of the frame is the classic way to lose a setting.
        static RectTransform Page(RetroTabControl tabs, string caption)
        {
            var tab = tabs.AddTab(caption);
            var content = RetroUi.VScroll(tab, out var scroll);
            RetroUi.Flexible(RetroUi.ScrollFrame(scroll));

            // No sunken field frame: a tab page is already inside one.
            var frame = RetroUi.ScrollBackdrop(scroll);
            if (frame) RetroPalette.Paint(frame, PaletteSlot.Face);
            var bevel = scroll.GetComponent<RetroBevel>();
            if (bevel) bevel.SetVisible(false);

            return content;
        }

        // --- appearance ----------------------------------------------------

        // The machine's shipped schemes. Picking one repaints everything on screen, windows included.
        void BuildAppearance(RectTransform page)
        {
            var group = RetroUi.GroupBox(page, "Scheme");
            RetroUi.Size(group, h: 240f);

            var list = RetroUi.ListView(group.Body, ListViewMode.List);
            RetroUi.Flexible(list);

            list.Add(new RetroListEntry
            {
                Text = "Theme default",
                Icon = RetroIcons.Get(BuiltinIcon.Paint),
                Tag = null
            });

            foreach (var scheme in RetroColorScheme.All)
                list.Add(new RetroListEntry
                {
                    Text = scheme.Name,
                    Icon = RetroIcons.Get(BuiltinIcon.Paint),
                    Tag = scheme
                });

            list.SelectionChanged += entry =>
            {
                var scheme = entry?.Tag as RetroColorScheme;

                // Through the machine's own skin, so the repaint stops at this screen.
                Os.Skin.Repaint(scheme);

                // The machine remembers what the player picked, the way it
                // remembers everything else they did to it.
                Os.Prefs.Set(SchemeKey, scheme?.Name);
                Os.Prefs.Save();

                _status?.Set(0, "Scheme: " + (scheme?.Name ?? "Theme default"));
                Raise("scheme", scheme?.Name ?? "default");
            };

            // Whatever is already applied is what is already selected.
            var current = Os.Skin.Scheme;
            list.SelectTag(current);

            var wallpaper = RetroUi.GroupBox(page, "Wallpaper");
            RetroUi.Size(wallpaper, h: 84f);
            var row = RetroUi.NewRect("Row", wallpaper.Body);
            RetroUi.Size(row, h: 26f);
            RetroUi.HLayout(row.gameObject, 6, 0);
            RetroUi.Size(RetroUi.PushButton(row, "Browse...", ChooseWallpaper,
                RetroIcons.Get(BuiltinIcon.Photos)), w: 110f);
            RetroUi.Size(RetroUi.PushButton(row, "None", () => SetWallpaper(null)), w: 80f);
            RetroUi.Spacer(row);

            RetroUi.Label(wallpaper.Body,
                "Any picture on this machine can be the wallpaper.", 12);

            var screen = RetroUi.GroupBox(page, "Screen");
            RetroUi.Size(screen, h: 92f);

            var host = Os.Screen;
            var scaling = RetroUi.CheckBox(screen.Body,
                "Scale the interface by whole numbers",
                host ? host.PixelPerfect : true,
                v =>
                {
                    if (host) host.PixelPerfect = v;
                    Os.Prefs.SetBool(PixelPerfectKey, v);
                    Os.Prefs.Save();
                    _status?.Set(0, v ? "Whole-number scaling" : "Stretched to fill");
                    Raise("scaling", v ? "whole" : "stretched");
                });
            RetroUi.Size(scaling, h: 20f);
            scaling.Interactable = host;

            RetroUi.Label(screen.Body,
                host
                    ? "On, a 32-pixel icon lands on 32, 64 or 96 pixels and a one-pixel " +
                      "border stays one pixel, so a bigger display shows more desktop. Off, " +
                      "the interface stretches to fill the display exactly, slightly softly."
                    : "This machine is not drawing on a screen of its own.", 12);
        }

        void ChooseWallpaper() =>
            RetroFileDialog.Open(Os, "Wallpaper", new[] { Core.FileSystem.FileKind.Image },
                file => SetWallpaper(
                    file != null && file.TryLoad(Os.CurrentUser, out var picture)
                        ? picture as Sprite : null,
                    file?.Path));

        // The path is what gets written down, not the sprite: the picture is
        // a file on this machine, and the machine can find it again at boot.
        void SetWallpaper(Sprite sprite, string path = null)
        {
            var found = Desktop.DesktopScreen.SetWallpaper(Os, sprite);
            _status?.Set(0, !found ? "No desktop to change."
                : sprite ? "Wallpaper changed." : "Wallpaper cleared.");
            if (!found) return;

            Os.Prefs.Set(WallpaperKey, sprite ? path : "");
            Os.Prefs.Save();
            Raise("wallpaper", sprite ? sprite.name : "none");
        }

        // --- date and time -------------------------------------------------

        void BuildDateTime(RectTransform page)
        {
            var group = RetroUi.GroupBox(page, "System clock");
            RetroUi.Size(group, h: 150f);

            var readout = RetroUi.Label(group.Body, Os.Clock.LongStamp, 15, TextAnchor.MiddleLeft);
            RetroUi.Size(readout, h: 26f);

            void Refresh() => readout.text = Os.Clock.LongStamp;

            var hours = RetroUi.NewRect("Hours", group.Body);
            RetroUi.Size(hours, h: 26f);
            RetroUi.HLayout(hours.gameObject, 4, 0);
            // Five things on one line is more than a narrow window has room
            // for, so they crowd rather than pushing the last one off the edge.
            RetroUi.SoftSize(RetroUi.NoWrap(
                RetroUi.Label(hours, "Wind the clock:", 13, TextAnchor.MiddleLeft)), 110f, 52f);
            RetroUi.SoftSize(RetroUi.PushButton(hours, "-1 hour",
                () => { Os.Clock.Nudge(System.TimeSpan.FromHours(-1)); Refresh(); }), 80f);
            RetroUi.SoftSize(RetroUi.PushButton(hours, "+1 hour",
                () => { Os.Clock.Nudge(System.TimeSpan.FromHours(1)); Refresh(); }), 80f);
            RetroUi.SoftSize(RetroUi.PushButton(hours, "-1 day",
                () => { Os.Clock.Nudge(System.TimeSpan.FromDays(-1)); Refresh(); }), 80f);
            RetroUi.SoftSize(RetroUi.PushButton(hours, "+1 day",
                () => { Os.Clock.Nudge(System.TimeSpan.FromDays(1)); Refresh(); }), 80f);
            RetroUi.Spacer(hours);

            RetroUi.Size(RetroUi.CheckBox(group.Body, "24-hour clock", Os.Clock.Use24Hour, v =>
            {
                Os.Clock.Use24Hour = v;
                Os.Prefs.SetBool(ClockKey, v);
                Os.Prefs.Save();
                Refresh();
                _status?.Set(0, v ? "24-hour clock" : "12-hour clock");
            }), h: 22f);

            RetroUi.Size(RetroUi.PushButton(group.Body, "Set to a date...", PromptForDate), h: 26f);

            // The readout has to keep up with the clock it is showing.
            var ticker = group.Body.gameObject.AddComponent<RetroClockTicker>();
            ticker.Tick = Refresh;
        }

        void PromptForDate() =>
            RetroDialog.Prompt(Os.Overlay, "Set Date/Time",
                "Type a date and time (yyyy-MM-dd HH:mm):", text =>
                {
                    if (!System.DateTime.TryParse(text,
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out var when))
                    {
                        RetroDialog.Alert(Os.Overlay, "Set Date/Time",
                            $"'{text}' is not a date this machine understands.");
                        return;
                    }

                    Os.Clock.Set(when);
                    _status?.Set(0, "Clock set to " + Os.Clock.LongStamp);
                    Raise("clock", when.ToString("s"));
                });

        // --- accounts ------------------------------------------------------

        void BuildAccounts(RectTransform page)
        {
            var who = Os.Auth.Current;

            var group = RetroUi.GroupBox(page, "This account");
            RetroUi.Size(group, h: 130f);

            var header = RetroUi.NewRect("Header", group.Body);
            RetroUi.Size(header, h: 52f);
            RetroUi.HLayout(header.gameObject, 8, 0);

            // The account's own picture, if it was given one.
            var entry = Os.Accounts.FirstOrDefault(u =>
                u != null && who != null && u.username == who.Username);

            if (entry?.avatar)
            {
                var frame = RetroUi.Panel(header, PaletteSlot.Face, "Portrait");
                RetroUi.Size(frame, w: 52f);
                RetroUi.Bevel(frame.rectTransform, false, 1f);
                RetroUi.Stretch(RetroUi.Picture(frame.transform, entry.avatar).rectTransform, 3f);
            }

            RetroUi.Flexible(RetroUi.Label(header, who == null
                ? "Nobody is signed in on this machine."
                : $"Signed in as {who.DisplayName} ({who.Username}).", 13));

            var row = RetroUi.NewRect("Row", group.Body);
            RetroUi.Size(row, h: 26f);
            RetroUi.HLayout(row.gameObject, 6, 0);
            var change = RetroUi.PushButton(row, "Change Password...", ChangePassword,
                RetroIcons.Get(BuiltinIcon.Key));
            RetroUi.Size(change, w: 170f);
            change.Interactable = who != null;
            RetroUi.Size(RetroUi.PushButton(row, "Log Off", () => Os.RequestLogout()), w: 90f);
            RetroUi.Spacer(row);

            var others = RetroUi.GroupBox(page, "Accounts on this machine");
            RetroUi.Size(others, h: 180f);
            var list = RetroUi.ListView(others.Body, ListViewMode.Details);
            RetroUi.Flexible(list);
            list.SetColumns(new[] { "User", "Name" }, new[] { 140f, -1f });

            foreach (var user in Os.Accounts)
            {
                if (user == null || string.IsNullOrWhiteSpace(user.username)) continue;
                list.Add(new RetroListEntry
                {
                    Text = user.username,
                    Icon = RetroIcons.Get(BuiltinIcon.Contacts),
                    Bold = who != null && user.username == who.Username,
                    Columns = new[] { string.IsNullOrEmpty(user.displayName) ? user.username : user.displayName }
                });
            }
        }

        void ChangePassword()
        {
            var account = Os.Auth.Current;
            if (account == null) return;

            RetroDialog.Prompt(Os.Overlay, "Change Password", "Current password:", current =>
            {
                if (current != account.Password)
                {
                    RetroDialog.Alert(Os.Overlay, "Change Password", "That is not the current password.");
                    return;
                }

                RetroDialog.Prompt(Os.Overlay, "Change Password", "New password:", next =>
                {
                    account.SetPassword(next);
                    _status?.Set(0, "Password changed for this session.");
                    RetroDialog.Info(Os.Overlay, "Change Password",
                        "The password has been changed for this session.\n\n" +
                        "The machine goes back to its authored password when the game restarts.");
                    Raise("password", account.Username);
                }, password: true);
            }, password: true);
        }

        // A percentage: the volumes, and anything else that runs nought to full.
        void Slider(Transform parent, string caption, float value, System.Action<float> onChanged) =>
            Slider(parent, caption, value, 0f, 1f,
                v => Mathf.RoundToInt(v * 100f) + "%", onChanged);

        // The same control over a range that is not a percentage: a pointer speed
        // reads as 1.5x, and a double-click window as a fifth of a second.
        void Slider(Transform parent, string caption, float value, float min, float max,
            System.Func<float, string> readAs, System.Action<float> onChanged)
        {
            var row = RetroUi.NewRect("Row", parent);
            RetroUi.Size(row, h: 24f);
            RetroUi.HLayout(row.gameObject, 6, 0);

            RetroUi.Size(RetroUi.NoWrap(RetroUi.Label(row, caption, 13, TextAnchor.MiddleLeft)), w: 130f);
            var readout = RetroUi.Label(row, "", 12, TextAnchor.MiddleRight);
            RetroUi.Size(readout, w: 52f);

            var bar = RetroUi.TrackBar(row, Mathf.InverseLerp(min, max, value), t =>
            {
                var scaled = Mathf.Lerp(min, max, t);
                readout.text = readAs(scaled);
                onChanged(scaled);
                _status?.Set(0, caption + " " + readout.text);
            });
            RetroUi.Flexible(bar);
            readout.text = readAs(Mathf.Clamp(value, min, max));

            // Keep the readout beside the slider, not after it.
            readout.transform.SetAsLastSibling();
        }

        // Records a value now and writes the file shortly, so dragging a slider
        // does not save on every frame of the drag.
        void Remember(string key, float value)
        {
            Os.Prefs.SetFloat(key, value);
            _saveAt = Time.unscaledTime + 0.4f;
            _saving = true;
        }

        void Update()
        {
            if (!_saving || Time.unscaledTime < _saveAt) return;
            Flush();
        }

        // Closing the window in the middle of the wait must not lose the setting.
        void OnDisable() => Flush();

        void Flush()
        {
            if (!_saving) return;
            _saving = false;
            Os?.Prefs?.Save();
        }

        // --- sound ---------------------------------------------------------

        void BuildSound(RectTransform page)
        {
            var group = RetroUi.GroupBox(page, "Volume");
            RetroUi.Size(group, h: 120f);

            // Remembered like everything else on this page. A volume that goes back
            // to full on the next boot is a setting the player sets twice and then
            // stops touching.
            Slider(group.Body, "Master", Os.Audio.MasterVolume, v =>
            {
                Os.Audio.MasterVolume = v;
                Remember(MasterVolumeKey, v);
            });
            Slider(group.Body, "Interface", Os.Audio.UiVolume, v =>
            {
                Os.Audio.UiVolume = v;
                Remember(UiVolumeKey, v);
            });
            Slider(group.Body, "Music", Os.Audio.MusicVolume, v =>
            {
                Os.Audio.MusicVolume = v;
                Remember(MusicVolumeKey, v);
            });

            var test = RetroUi.GroupBox(page, "Test");
            RetroUi.Size(test, h: 96f);
            var row = RetroUi.NewRect("Row", test.Body);
            RetroUi.Size(row, h: 26f);
            RetroUi.HLayout(row.gameObject, 4, 0);

            // Each button says how wide it is, and a spacer takes what is left. A
            // row gives no width at all to a child that asks for none, so these
            // five drew on top of one another in the corner of the group: one
            // 26-pixel stack with five captions in it.
            foreach (var (sfx, caption) in new[]
                     {
                         (Audio.UiSfx.Click, "Click"),
                         (Audio.UiSfx.WindowOpen, "Window"),
                         (Audio.UiSfx.Error, "Error"),
                         (Audio.UiSfx.Notify, "Notify"),
                         (Audio.UiSfx.Startup, "Startup")
                     })
            {
                var kind = sfx;
                RetroUi.SoftSize(RetroUi.PushButton(row, caption, () => Os.Audio.PlayUi(kind)), 76f);
            }

            RetroUi.Spacer(row);

            // Written to the machine's prefs like the scheme, clock format and wallpaper (it was the one that was not).
            RetroUi.Size(RetroUi.CheckBox(test.Body, "Interface sounds",
                Os.Skin.Sounds ?? RetroSkin.WidgetSounds,
                v =>
                {
                    Os.Skin.SetSounds(v);
                    Os.Prefs.SetBool(SoundsKey, v);
                    Os.Prefs.Save();
                }), h: 20f);
        }

        // --- mouse ---------------------------------------------------------

        // Pointer speed is the drawn pointer's, so it is a setting only a machine
        // that draws one has. Double-click speed belongs to every machine.
        void BuildMouse(RectTransform page)
        {
            var pointer = Os.Pointer;

            var group = RetroUi.GroupBox(page, "Pointer");

            // Two sliders and a note, or just the note: a group box sized for
            // controls that are not there is a page with a hole in it.
            RetroUi.Size(group, h: pointer ? 126f : 58f);

            if (pointer)
            {
                Slider(group.Body, "Speed", pointer.CursorSpeed, 0.25f, 3f,
                    v => v.ToString("0.0#") + "x", v =>
                    {
                        pointer.CursorSpeed = v;
                        Remember(PointerSpeedKey, v);
                    });

                Slider(group.Body, "Gamepad stick", pointer.StickSpeed, 400f, 4000f,
                    v => Mathf.RoundToInt(v) + "/s", v =>
                    {
                        pointer.StickSpeed = v;
                        Remember(StickSpeedKey, v);
                    });

                RetroUi.Label(group.Body,
                    "How far the pointer drawn on this screen travels for a given " +
                    "movement of the hand. The second one is for playing with a " +
                    "controller instead.", 12);
            }
            else
            {
                RetroUi.Label(group.Body,
                    "This screen is pointed at with the computer's own mouse, so how " +
                    "fast that moves is set outside this machine.", 12);
            }

            var clicks = RetroUi.GroupBox(page, "Double-click");
            RetroUi.Size(clicks, h: 150f);

            Slider(clicks.Body, "Speed", RetroClicks.Seconds, 0.15f, 0.9f,
                v => v.ToString("0.00") + "s", v =>
                {
                    RetroClicks.Seconds = v;
                    Remember(DoubleClickKey, v);
                });

            // The Win95 test area, and the reason it is worth having: it is the
            // one place the player can see that the setting reaches the desktop
            // icons and the file lists, not just this page.
            var box = RetroUi.Panel(clicks.Body, PaletteSlot.Field, "TestArea");
            RetroUi.Size(box, h: 54f);
            RetroUi.Bevel(box.rectTransform, false, 1f);

            var mark = RetroUi.Label(box.transform, "Double-click in here to try it out",
                13, TextAnchor.MiddleCenter);
            RetroUi.Stretch(mark.rectTransform, 3f);

            var open = false;
            box.gameObject.AddComponent<RetroDoubleClick>().Clicked = () =>
            {
                open = !open;
                mark.text = open ? "Opened." : "Shut again.";
                Os.Audio.PlayUi(Audio.UiSfx.Click);
                _status?.Set(0, "Double-click caught");
            };
        }

        // --- network -------------------------------------------------------

        void BuildNetwork(RectTransform page)
        {
            var toggleRow = RetroUi.NewRect("Row", page);
            RetroUi.Size(toggleRow, h: 24f);
            RetroUi.HLayout(toggleRow.gameObject, 6, 2);
            // Sized, and followed by a spacer: a row gives no width to a child that asks for none.
            RetroUi.SoftSize(RetroUi.CheckBox(toggleRow, "Modem connected", Os.Net.Online, v =>
            {
                Os.Net.Online = v;
                _status.Set(0, v ? "Connected" : "Disconnected");
                Raise("network", v ? "online" : "offline");
            }), 170f);
            RetroUi.Spacer(toggleRow);

            // The modem above is this machine's; the line below is the building's, the same on every machine.
            RetroUi.Size(RetroUi.Label(page,
                Os.Net.Internet
                    ? (Os.Net.Internet.Online
                        ? $"Line to {Os.Net.Internet.Carrier}: up."
                        : $"Line to {Os.Net.Internet.Carrier}: down. The LAN is unaffected.")
                    : "No exchange in this world; every site answers.", 12), h: 18f);

            var list = RetroUi.ListView(page, ListViewMode.Details);
            RetroUi.Size(list, h: 200f);
            list.SetColumns(new[] { "Address", "Zone", "Status" }, new[] { -1f, 90f, 80f });

            // The machines actually on the LAN right now, then the sites.
            foreach (var neighbour in Os.Net.Neighbours)
                list.Add(new RetroListEntry
                {
                    Text = neighbour.DisplayName,
                    Icon = RetroIcons.Get(BuiltinIcon.Network),
                    Columns = new[] { "LAN", $"{neighbour.VisibleShares.Count()} share(s)" },
                    Tag = neighbour
                });

            foreach (var site in Os.Net.KnownSites)
                list.Add(new RetroListEntry
                {
                    Text = site,
                    Icon = RetroIcons.Get(BuiltinIcon.Globe),
                    Columns = new[] { "Internet", "Online" },
                    Tag = site
                });

            list.Activated += entry =>
            {
                if (entry.Tag is not string domain) return;
                var browser = Os.Apps.Find("web");
                if (browser == null) return;
                var window = Os.Apps.Launch(browser);
                var view = window ? window.Content.GetComponent<WebBrowserView>() : null;
                if (view) view.Navigate(domain);
            };

            var refresh = RetroUi.NewRect("Row", page);
            RetroUi.Size(refresh, h: 26f);
            RetroUi.HLayout(refresh.gameObject, 6, 2);
            RetroUi.Spacer(refresh);
            RetroUi.Size(RetroUi.PushButton(refresh, "Refresh", () =>
            {
                Os.Drives.Refresh();
                _status.Set(0, "Drives and network refreshed");
            }, RetroIcons.Refresh), w: 100f);
        }

        // --- system --------------------------------------------------------

        void BuildSystem(RectTransform page)
        {
            var about = RetroUi.GroupBox(page, "System");
            RetroUi.Size(about, h: 130f);
            RetroUi.Label(about.Body,
                $"{Os.OsName}\n" +
                $"Computer name: {(Os.Machine ? Os.Machine.MachineName : "-")}\n" +
                $"Programs installed: {Os.Apps.All.Count}\n" +
                $"System clock: {Os.Clock.Now:yyyy-MM-dd HH:mm}\n" +
                $"Signed in as: {Os.Auth.Current?.DisplayName ?? "(no user)"}", 13);

            var storage = RetroUi.GroupBox(page, "Files");
            RetroUi.Size(storage, h: 190f);
            var path = RetroUi.Label(storage.Body, StorageDescription(), 12,
                TextAnchor.UpperLeft, RetroSkin.Mono);
            RetroUi.Flexible(path);

            var row = RetroUi.NewRect("Row", storage.Body);
            RetroUi.Size(row, h: 26f);
            RetroUi.HLayout(row.gameObject, 6, 0);
            RetroUi.Spacer(row);
            RetroUi.Size(RetroUi.PushButton(row, "Forget Settings...", ForgetSettings), w: 140f);
            RetroUi.Size(RetroUi.PushButton(row, "Flags...", ShowFlags), w: 90f);
        }

        // Puts the machine back the way it came (colours, clock, icon positions, folded panes). Touches no files - that is Explorer's job.
        void ForgetSettings()
        {
            RetroDialog.Confirm(Os.Overlay, "Forget Settings",
                "Put this machine's appearance and layout back to how it " +
                "started?" + (char)10 + (char)10 +
                "No files are deleted. The change is complete after the " +
                "machine is restarted.", ok =>
                {
                    if (!ok) return;
                    Os.Prefs.Clear();
                    Os.Prefs.Save();
                    Raise("forgot", "settings");
                    RetroDialog.Info(Os.Overlay, "Forget Settings",
                        "Settings forgotten.");
                });
        }

        // What is actually on disk. No lan/ folder - a network share is a folder on another machine (`share: NAME` in its .rmeta).
        static string StorageDescription()
        {
            var nl = "" + (char)10;
            return "Data folder:" + nl + RetroStorage.Root + nl + nl +
                   "machines/  one folder per computer, with one folder" + nl +
                   "           inside it per drive (C, A, D...)" + nl +
                   "internet/  one folder per site, named by domain or IP";
        }

        void ShowFlags()
        {
            var flags = Os.Flags.All.ToList();
            RetroDialog.Info(Os.Overlay, "Story flags",
                flags.Count == 0 ? "No flags are set." : string.Join("\n", flags));
            Raise("flags");
        }
    }

    // Keeps a clock readout honest without writing to it every frame - a Text
    // written every frame dirties the canvas every frame.
    public class RetroClockTicker : MonoBehaviour
    {
        public System.Action Tick;

        float _next;

        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 1f;
            Tick?.Invoke();
        }
    }
}
