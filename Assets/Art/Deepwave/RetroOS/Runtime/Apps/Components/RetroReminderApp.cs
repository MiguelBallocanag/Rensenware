using System;
using System.Collections.Generic;
using UnityEngine;
using Deepwave.RetroOS.Data;
using Deepwave.RetroOS.Events;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Apps.Components
{
    // Reminders timed against the machine's own clock. Each one that goes off
    // raises reminder.due, so a rule can turn it into a story beat.
    [AddComponentMenu("")]
    public class RetroReminderApp : RetroAppBehaviour, IReminderSource
    {
        [Header("Reminders")]
        [Tooltip("What is already in the list when the machine boots. The " +
                 "player can add, edit and delete for the session.")]
        [SerializeField] List<ReminderEntry> reminders = new();

        public List<ReminderEntry> Reminders => reminders;

        // The clock lives here rather than in the window, because a reminder
        // the player set and then closed the program on still has to go off.
        readonly HashSet<string> _ranToday = new();
        DateTime _lastChecked;
        bool _started;

        public void Rearm(string reminderId)
        {
            if (!string.IsNullOrEmpty(reminderId)) _ranToday.Remove(reminderId);
        }

        void Update()
        {
            var os = RetroOs.For(this);
            if (os?.Clock == null) return;

            var now = os.Clock.Now;

            // The first tick only notes the time, so a reminder for the boot minute does not fire at the logon screen.
            if (!_started) { _started = true; _lastChecked = now; return; }

            // A reminder is due at a minute, not at a frame.
            if (now.Hour == _lastChecked.Hour && now.Minute == _lastChecked.Minute &&
                now.Date == _lastChecked.Date) return;

            // A new day gives every daily reminder its turn again.
            if (now.Date != _lastChecked.Date) _ranToday.Clear();
            _lastChecked = now;

            foreach (var entry in reminders)
            {
                if (entry == null || !entry.enabled) continue;
                if (entry.hour != now.Hour || entry.minute != now.Minute) continue;
                if (!_ranToday.Add(entry.Id)) continue;

                Ring(os, entry);
                if (!entry.daily) entry.enabled = false;
            }
        }

        // A balloon, not a dialog: a reminder going off while the player is
        // reading something should not take the pointer away from them.
        void Ring(RetroOsContext os, ReminderEntry entry)
        {
            os.Notify(entry.TimeText, entry.title, RetroIcons.Get(entry.Icon),
                () => os.Apps.Launch(Id));

            os.Events.Raise(OsEvents.ReminderDue, entry.Id);
        }

        public override void BuildContent(AppContext ctx) => AppView.Attach<ReminderView>(ctx);

        public override void ApplyDefaults()
        {
            app.appId = "reminders";
            app.title = "Reminders";
            app.builtinIcon = BuiltinIcon.Calendar;
            app.category = "Accessories";
            app.windowSize = new Vector2(520, 360);
            app.minWindowSize = new Vector2(470, 280);
            app.showOnDesktop = false;
        }
    }
}
