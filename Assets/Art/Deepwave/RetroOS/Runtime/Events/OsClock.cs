using System;
using System.Globalization;
using UnityEngine;

namespace Deepwave.RetroOS.Events
{
    // In-fiction system clock: anchored to the world's system date and advanced in real time
    // (falls back to the real clock). The date is the world's, not each machine's - machines in a room share a year.
    public class OsClock
    {
        readonly DateTime _base;
        readonly float _bootTime;
        TimeSpan _offset;

        /// <summary>Raised when the clock is set or the format changes.</summary>
        public event Action Changed;

        public OsClock(string configuredDate)
        {
            _bootTime = Time.realtimeSinceStartup;
            if (!DateTime.TryParse(configuredDate, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out _base))
                _base = DateTime.Now;
        }

        public DateTime Now => _base.AddSeconds(Time.realtimeSinceStartup - _bootTime) + _offset;

        /// <summary>Whether the tray and every clock in the OS reads 14:05 or 2:05 PM.</summary>
        public bool Use24Hour
        {
            get => _use24Hour;
            set
            {
                if (_use24Hour == value) return;
                _use24Hour = value;
                Changed?.Invoke();
            }
        }

        bool _use24Hour = true;

        /// <summary>The time as the tray shows it.</summary>
        public string ShortTime => Now.ToString(_use24Hour ? "HH:mm" : "h:mm tt",
            CultureInfo.InvariantCulture);

        /// <summary>The date and time, for a status bar or a properties box.</summary>
        public string LongStamp => Now.ToString(_use24Hour
            ? "dddd, d MMMM yyyy  HH:mm" : "dddd, d MMMM yyyy  h:mm tt", CultureInfo.InvariantCulture);

        /// <summary>Sets the machine's clock. It keeps running from there, so a player who winds it forward an hour stays an hour ahead.</summary>
        public void Set(DateTime when)
        {
            _offset = when - Now + _offset;
            Changed?.Invoke();
        }

        /// <summary>Winds the clock by a span - the up/down arrows on a time box.</summary>
        public void Nudge(TimeSpan by)
        {
            _offset += by;
            Changed?.Invoke();
        }
    }
}
