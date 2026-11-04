using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deepwave.RetroOS.Events
{
    /// <summary>Central pub/sub pipe. Every action the OS takes is raised here, so game code and profile rules can react to it and steer the story. Standard ids live in <see cref="OsEvents"/>.</summary>
    public class OsEventBus
    {
        const int MaxDepth = 16;

        // Ids match case-insensitively, like profile rules - a subscription written from memory still fires.
        readonly Dictionary<string, List<Action<string>>> _handlers =
            new(StringComparer.OrdinalIgnoreCase);
        int _depth;

        /// <summary>Raised for every event that passes through the bus, as (eventId, argument). Used by the rule runner.</summary>
        public event Action<string, string> AnyRaised;

        /// <summary>Raises an event to every subscriber. Handlers may raise further events; a chain deeper than 16 is dropped with a warning rather than recursing forever, which catches rules that trigger each other.</summary>
        /// <param name="eventId">Event id. Empty ids are ignored.</param>
        /// <param name="argument">Optional payload passed to the handlers.</param>
        public void Raise(string eventId, string argument = null)
        {
            if (string.IsNullOrEmpty(eventId)) return;
            if (_depth >= MaxDepth)
            {
                Debug.LogWarning($"RetroOS: event chain too deep, dropped '{eventId}'. Check rules for loops.");
                return;
            }

            _depth++;
            try
            {
                AnyRaised?.Invoke(eventId, argument);
                if (_handlers.TryGetValue(eventId, out var list))
                    foreach (var handler in list.ToArray()) handler?.Invoke(argument);
            }
            finally { _depth--; }
        }

        /// <summary>Listens for one event id.</summary>
        /// <returns>
        /// An action that removes the subscription. Call it from
        /// <c>OnDestroy</c> - the bus lives as long as the OS, so a handler
        /// left behind keeps a destroyed object alive.
        /// </returns>
        public Action Subscribe(string eventId, Action<string> handler)
        {
            // A dictionary throws on a null key, and an id nobody can raise is
            // not worth taking the game down for.
            if (string.IsNullOrEmpty(eventId) || handler == null) return () => { };

            if (!_handlers.TryGetValue(eventId, out var list))
                _handlers[eventId] = list = new List<Action<string>>();
            list.Add(handler);
            return () => list.Remove(handler);
        }
    }
}
