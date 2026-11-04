using System;
using UnityEngine;

namespace Deepwave.RetroOS.Data
{
    /// <summary>What a reminder is for; changes only its icon and wording.</summary>
    public enum ReminderKind
    {
        Task,
        Meeting,
        Call,
        Birthday,
        Deadline
    }

    // One entry in the machine's reminder list.
    [Serializable]
    public class ReminderEntry
    {
        [Tooltip("Stable id used by rules and events. Empty = the title.")]
        public string id;

        public string title = "Reminder";
        public ReminderKind kind = ReminderKind.Task;

        [Tooltip("Hour on the machine's own clock, 0-23.")]
        [Range(0, 23)] public int hour = 9;
        [Range(0, 59)] public int minute;

        [Tooltip("Off means it sits in the list without ever going off.")]
        public bool enabled = true;

        [Tooltip("Ring again the next day rather than only once.")]
        public bool daily;

        public string Id => string.IsNullOrWhiteSpace(id) ? title : id;

        public string TimeText => $"{hour:00}:{minute:00}";

        /// <summary>Icon for this reminder kind.</summary>
        public UI.BuiltinIcon Icon => kind switch
        {
            ReminderKind.Meeting => UI.BuiltinIcon.Contacts,
            ReminderKind.Call => UI.BuiltinIcon.Phone,
            ReminderKind.Birthday => UI.BuiltinIcon.Star,
            ReminderKind.Deadline => UI.BuiltinIcon.Warning,
            _ => UI.BuiltinIcon.Clock
        };
    }
}
