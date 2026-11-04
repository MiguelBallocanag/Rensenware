using System;
using System.Collections.Generic;
using UnityEngine;
using Deepwave.RetroOS.Data;
using Deepwave.RetroOS.Events;
using Deepwave.RetroOS.UI;
using Deepwave.RetroOS.UI.Components;

namespace Deepwave.RetroOS.Apps
{
    // Reminders timed against the OS clock. The list and clock belong to the program,
    // not this window, so a reminder still fires after the player closes it; a fired
    // one raises reminder.due on the event bus.
    public class ReminderView : AppView
    {
        RetroListView _list;
        RetroStatusStrip _status;
        Action _unsubscribe;
        int _shownMinute = -1;

        IReminderSource Source => Config<IReminderSource>();
        List<ReminderEntry> Entries => Source?.Reminders ?? _local;
        readonly List<ReminderEntry> _local = new();

        protected override void Build()
        {
            RetroUi.VLayout(Root.gameObject, 2, 2);
            BuildMenu();
            BuildToolbar();

            _list = RetroUi.ListView(Root, ListViewMode.Details);
            RetroUi.Flexible(_list);
            _list.SetColumns(new[] { "Time", "Reminder", "Kind", "When" },
                new[] { 60f, -1f, 80f, 70f });
            _list.Activated += _ => EditSelected();

            _status = RetroUi.StatusStrip(Root, -1f, 120f);

            // The program rings the reminders whether this window is open or
            // not; the window listens so the list keeps up.
            _unsubscribe = Os.Events.Subscribe(OsEvents.ReminderDue, id =>
            {
                Refresh();
                Raise("due", id);
            });

            Refresh();
        }

        void BuildMenu()
        {
            var menu = RetroUi.Menu(Root);

            menu.AddMenu("File")
                .Command("New Reminder...", NewReminder, "Ctrl+N", RetroIcons.Get(BuiltinIcon.Clock))
                .Separator()
                .Command("Close", () => Window.Close(), "Alt+F4");

            menu.AddMenu("Edit")
                .Command("Edit...", EditSelected, "Enter")
                .Command("Turn On/Off", ToggleSelected, "Space")
                .Separator()
                .Command("Delete", DeleteSelected, "Del");

            menu.AddMenu("Help").Command("About", () => RetroDialog.Info(Os.Overlay, "Reminders",
                "Reminders are timed against this machine's clock, which the " +
                "Control Panel can wind forward."));
        }

        void BuildToolbar()
        {
            var bar = RetroUi.ToolStrip(Root);
            bar.Add(RetroIcons.Get(BuiltinIcon.Clock), "New reminder", NewReminder, "New");
            bar.Add(RetroIcons.Get(BuiltinIcon.Note), "Edit", EditSelected, "Edit");
            bar.AddSeparator();
            bar.Add(RetroIcons.Get(BuiltinIcon.Key), "Turn on or off", ToggleSelected, "On/Off");
            bar.Add(RetroIcons.Get(BuiltinIcon.Trash), "Delete", DeleteSelected, "Delete");
        }

        // --- list -------------------------------------------------------------

        void Refresh()
        {
            if (!this || _list == null) return;

            var keep = _list.Selection?.Tag as ReminderEntry;
            _list.Clear();

            // Soonest first, so the next thing due is at the top where the
            // player will look for it.
            var sorted = new List<ReminderEntry>(Entries);
            sorted.Sort((a, b) => (a.hour * 60 + a.minute).CompareTo(b.hour * 60 + b.minute));

            var live = 0;
            foreach (var entry in sorted)
            {
                if (entry == null) continue;
                if (entry.enabled) live++;

                _list.Add(new RetroListEntry
                {
                    Text = entry.TimeText,
                    Icon = RetroIcons.Get(entry.Icon),
                    Bold = entry.enabled,
                    TextColor = entry.enabled ? null : RetroSkin.TextDisabled,
                    Columns = new[]
                    {
                        entry.title,
                        entry.kind.ToString(),
                        entry.daily ? "Every day" : "Once"
                    },
                    Tag = entry
                });
            }

            if (sorted.Count == 0)
                _list.Add(new RetroListEntry
                {
                    Text = "--:--",
                    Columns = new[] { "(nothing to remember)", "", "" }
                });

            if (keep != null) _list.SelectTag(keep);

            _status.Set(0, $"{sorted.Count} reminder(s), {live} switched on");
            _status.Set(1, Os.Clock.ShortTime);
        }

        // --- editing ----------------------------------------------------------

        void NewReminder()
        {
            var now = Os.Clock.Now;
            Edit(new ReminderEntry
            {
                title = "New reminder",
                hour = now.Hour,
                minute = (now.Minute + 5) % 60
            }, isNew: true);
        }

        void EditSelected()
        {
            if (_list.Selection?.Tag is ReminderEntry entry) Edit(entry, isNew: false);
        }

        void Edit(ReminderEntry entry, bool isNew)
        {
            var (blocker, body) = RetroDialog.Custom(Os.Overlay,
                isNew ? "New Reminder" : "Reminder", 400f, 300f, BuiltinIcon.Clock);

            RetroUi.Size(RetroUi.NoWrap(
                RetroUi.Label(body, "Remind me to:", 12, TextAnchor.LowerLeft)), h: 16f);
            var title = RetroUi.TextBox(body, entry.title);
            RetroUi.Size(title, h: 26f);

            var timeRow = RetroUi.NewRect("Time", body);
            RetroUi.Size(timeRow, h: 26f);
            RetroUi.HLayout(timeRow.gameObject, 4, 0);
            RetroUi.Size(RetroUi.NoWrap(RetroUi.Label(timeRow, "At:", 12, TextAnchor.MiddleLeft)), w: 30f);

            var hours = new List<string>();
            for (var h = 0; h < 24; h++) hours.Add(h.ToString("00"));
            var hourBox = RetroUi.ComboBox(timeRow, hours, entry.hour, null);
            RetroUi.Size(hourBox, w: 70f);

            var minutes = new List<string>();
            for (var m = 0; m < 60; m += 5) minutes.Add(m.ToString("00"));
            var minuteBox = RetroUi.ComboBox(timeRow, minutes,
                Mathf.Clamp(entry.minute / 5, 0, minutes.Count - 1), null);
            RetroUi.Size(minuteBox, w: 70f);
            RetroUi.Spacer(timeRow);

            var kindRow = RetroUi.NewRect("Kind", body);
            RetroUi.Size(kindRow, h: 26f);
            RetroUi.HLayout(kindRow.gameObject, 4, 0);
            RetroUi.Size(RetroUi.NoWrap(RetroUi.Label(kindRow, "Kind:", 12, TextAnchor.MiddleLeft)), w: 40f);
            var kinds = new List<string>(Enum.GetNames(typeof(ReminderKind)));
            var kindBox = RetroUi.ComboBox(kindRow, kinds, (int)entry.kind, null);
            RetroUi.Size(kindBox, w: 140f);
            RetroUi.Spacer(kindRow);

            var enabled = RetroUi.CheckBox(body, "Switched on", entry.enabled, null);
            RetroUi.Size(enabled, h: 22f);
            var daily = RetroUi.CheckBox(body, "Every day", entry.daily, null);
            RetroUi.Size(daily, h: 22f);

            RetroUi.Spacer(body);

            var buttons = RetroUi.NewRect("Buttons", body);
            RetroUi.Size(buttons, h: 26f);
            RetroUi.HLayout(buttons.gameObject, 6, 0);
            RetroUi.Spacer(buttons);

            RetroUi.Size(RetroUi.PushButton(buttons, "OK", () =>
            {
                entry.title = string.IsNullOrWhiteSpace(title.Value) ? "Reminder" : title.Value.Trim();
                entry.hour = Mathf.Clamp(hourBox.Index, 0, 23);
                entry.minute = Mathf.Clamp(minuteBox.Index * 5, 0, 59);
                entry.kind = (ReminderKind)Mathf.Clamp(kindBox.Index, 0, kinds.Count - 1);
                entry.enabled = enabled.Checked;
                entry.daily = daily.Checked;

                if (isNew) Entries.Add(entry);

                // Rescheduling a reminder that already fired today lets it go
                // off again at its new time.
                Source?.Rearm(entry.Id);

                RetroDialog.Dismiss(blocker);
                Refresh();
                Raise(isNew ? "added" : "edited", entry.Id);
            }), w: 76f);

            RetroUi.Size(RetroUi.PushButton(buttons, "Cancel",
                () => RetroDialog.Dismiss(blocker)), w: 76f);

            title.Focus();
        }

        void ToggleSelected()
        {
            if (_list.Selection?.Tag is not ReminderEntry entry) return;
            entry.enabled = !entry.enabled;
            if (entry.enabled) Source?.Rearm(entry.Id);
            Refresh();
            Raise(entry.enabled ? "enabled" : "disabled", entry.Id);
        }

        void DeleteSelected()
        {
            if (_list.Selection?.Tag is not ReminderEntry entry) return;

            RetroDialog.Confirm(Os.Overlay, "Reminders",
                $"Delete '{entry.title}'?", ok =>
                {
                    if (!ok) return;
                    Entries.Remove(entry);
                    Source?.Rearm(entry.Id);
                    Refresh();
                    Raise("deleted", entry.Id);
                });
        }

        // The status clock is the only thing left ticking here: the program
        // rings the reminders, so they go off with the window shut.
        void Update()
        {
            var minute = Os.Clock.Now.Minute;
            if (minute == _shownMinute) return;
            _shownMinute = minute;
            _status?.Set(1, Os.Clock.ShortTime);
        }

        void OnDestroy() => _unsubscribe?.Invoke();
    }
}
