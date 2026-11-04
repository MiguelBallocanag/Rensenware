using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deepwave.RetroOS
{
    /// <summary>Static access to the running OS, for game code and scene components that need to reach it without holding a reference.</summary>
    public static class RetroOs
    {
        static readonly List<RetroOsContext> Running = new();

        /// <summary>Every OS currently booted, in the order they came up.</summary>
        public static IReadOnlyList<RetroOsContext> All => Running;

        /// <summary>The OS the player is at: the focused machine, or the last one to boot when none of them has the pointer. <c>null</c> before the first boot and after the last is destroyed.</summary>
        public static RetroOsContext Current
        {
            get
            {
                for (var i = Running.Count - 1; i >= 0; i--)
                    if (Running[i].IsFocused?.Invoke() ?? false)
                        return Running[i];

                return Running.Count > 0 ? Running[^1] : null;
            }
        }

        /// <summary>The OS running on the machine <paramref name="part"/> belongs to.</summary>
        public static RetroOsContext For(Component part)
        {
            if (!part) return Current;

            var bootstrap = part.GetComponentInParent<RetroOsBootstrap>(true);
            return bootstrap && bootstrap.Context != null ? bootstrap.Context : Current;
        }

        /// <summary>Raised when an OS finishes booting. Subscribe from <c>OnEnable</c> and unsubscribe from <c>OnDisable</c>; a component that starts later should check <see cref="Current"/> or <see cref="For(Component)"/> instead, as the event has already been and gone.</summary>
        public static event Action<RetroOsContext> Booted;

        internal static void SetCurrent(RetroOsContext context)
        {
            if (context == null) return;
            if (!Running.Contains(context)) Running.Add(context);
            Booted?.Invoke(context);
        }

        internal static void Forget(RetroOsContext context)
        {
            if (context == null) return;

            Running.Remove(context);
            if (Running.Count == 0) ReleaseSharedResources();
        }

        /// <summary>Frees what every machine in the scene was sharing, once the last one has gone.</summary>
        static void ReleaseSharedResources()
        {
            Core.Storage.MediaLoader.ClearCache();
            UI.RetroIcons.ReleaseAll();
        }

        /// <summary>Raises a custom event on every running OS, so profile rules and scene listeners can react to something that happened in your game. Does nothing before the first machine boots.</summary>
        /// <param name="eventId">Event id, e.g. <c>"story.door.opened"</c>.</param>
        /// <param name="argument">Optional payload the listeners can match on.</param>
        public static void Raise(string eventId, string argument = null)
        {
            // Every machine, not just the focused one - a world event is news to the server in the next room too.
            for (var i = Running.Count - 1; i >= 0; i--)
                Running[i].Events?.Raise(eventId, argument);
        }

        /// <summary>Raises an event on one machine only.</summary>
        public static void RaiseOn(RetroOsContext os, string eventId, string argument = null) =>
            os?.Events?.Raise(eventId, argument);
    }
}
