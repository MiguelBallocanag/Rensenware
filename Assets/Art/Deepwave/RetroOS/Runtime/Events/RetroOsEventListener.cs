using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Deepwave.RetroOS.Events
{
    // Scene-side bridge: drop on any GameObject and wire UnityEvents in the
    // inspector to react to OS events (open a door when a file is read, etc).
    [AddComponentMenu("Deepwave/RetroOS/RetroOS Event Listener")]
    public class RetroOsEventListener : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("Event id to listen for, e.g. 'file.opened'. See OsEvents.")]
            public string eventId;

            [Tooltip("Argument to match. Empty = any. Trailing * = prefix match.")]
            public string argument;

            [Tooltip("Invoked with the event argument.")]
            public UnityEvent<string> response = new();
        }

        [SerializeField] List<Entry> entries = new();

        [Tooltip("Invoked for every OS event with (eventId, argument).")]
        public UnityEvent<string, string> onAnyEvent = new();

        OsEventBus _bus;

        void OnEnable()
        {
            // The machine this listener sits on, if any - without it, two computers had every listener re-point to whichever booted last.
            var mine = RetroOs.For(this);
            if (mine != null) Bind(mine);

            RetroOs.Booted += BindIfMine;
        }

        void OnDisable()
        {
            RetroOs.Booted -= BindIfMine;
            Unbind();
        }

        // A machine booting after this listener: take it only when it is this object's own, or the object belongs to no machine (single-computer scene).
        void BindIfMine(RetroOsContext os)
        {
            var mine = GetComponentInParent<RetroOsBootstrap>(true);
            if (mine && mine.Context != os) return;
            Bind(os);
        }

        void Bind(RetroOsContext os)
        {
            Unbind();
            _bus = os.Events;
            _bus.AnyRaised += OnEvent;
        }

        void Unbind()
        {
            if (_bus != null) _bus.AnyRaised -= OnEvent;
            _bus = null;
        }

        void OnEvent(string id, string arg)
        {
            onAnyEvent.Invoke(id, arg);
            foreach (var entry in entries)
            {
                if (!string.Equals(entry.eventId?.Trim(), id, StringComparison.OrdinalIgnoreCase)) continue;
                if (!MatchArg(entry.argument, arg)) continue;
                entry.response.Invoke(arg);
            }
        }

        static bool MatchArg(string pattern, string arg)
        {
            pattern = pattern?.Trim();
            if (string.IsNullOrEmpty(pattern)) return true;
            arg ??= "";
            return pattern.EndsWith("*")
                ? arg.StartsWith(pattern.Substring(0, pattern.Length - 1), StringComparison.OrdinalIgnoreCase)
                : string.Equals(arg, pattern, StringComparison.OrdinalIgnoreCase);
        }
    }
}
