using System;
using System.Collections.Generic;
using System.IO;

namespace Deepwave.RetroOS.Core.Storage
{
    /// <summary>
    /// Hands out one <see cref="MachinePrefs"/> per machine folder. The static
    /// members below act on the last-bound store, for callers with no machine
    /// to hand; inside a running OS use <c>Os.Prefs</c>.
    /// </summary>
    public static class RetroPrefs
    {
        static readonly Dictionary<string, MachinePrefs> Stores =
            new(StringComparer.OrdinalIgnoreCase);

        static MachinePrefs s_last;

        /// <summary>The store for a machine folder, read once and cached.</summary>
        public static MachinePrefs For(string machineFolder)
        {
            var key = Key(machineFolder);
            if (key == null) return new MachinePrefs(null);

            if (!Stores.TryGetValue(key, out var store))
                Stores[key] = store = new MachinePrefs(machineFolder);

            return store;
        }

        /// <summary>Hands out a machine's store and makes it the target of the static members. Once per machine at boot.</summary>
        public static MachinePrefs Bind(string machineFolder)
        {
            // A machine with no folder keeps its settings in memory for the session.
            s_last = For(machineFolder);
            return s_last;
        }

        /// <summary>Saves every machine's store. Called on shutdown.</summary>
        public static void SaveAll()
        {
            foreach (var store in Stores.Values) store.Save();
        }

        /// <summary>Saves and forgets every store. For tests and world teardown; a live OS holds its own.</summary>
        public static void Release()
        {
            SaveAll();
            Stores.Clear();
            s_last = null;
        }

        static string Key(string machineFolder) =>
            string.IsNullOrWhiteSpace(machineFolder)
                ? null
                : machineFolder.Replace('\\', '/').TrimEnd('/');

        // --- the last-bound store ---
        // Same call on whichever machine booted most recently. Compatibility surface, not the good door.

        /// <summary>Whether a machine's settings have been loaded.</summary>
        public static bool Ready => s_last is { Ready: true };

        public static string Get(string key, string fallback = null) =>
            s_last?.Get(key, fallback) ?? fallback;

        public static int GetInt(string key, int fallback = 0) =>
            s_last?.GetInt(key, fallback) ?? fallback;

        public static float GetFloat(string key, float fallback = 0f) =>
            s_last?.GetFloat(key, fallback) ?? fallback;

        public static bool GetBool(string key, bool fallback = false) =>
            s_last?.GetBool(key, fallback) ?? fallback;

        public static bool Has(string key) => s_last?.Has(key) ?? false;

        public static void Set(string key, string value) => s_last?.Set(key, value);

        public static void SetInt(string key, int value) => s_last?.SetInt(key, value);

        public static void SetFloat(string key, float value) => s_last?.SetFloat(key, value);

        public static void SetBool(string key, bool value) => s_last?.SetBool(key, value);

        public static bool SetBest(string key, int value, bool lowerIsBetter = true) =>
            s_last?.SetBest(key, value, lowerIsBetter) ?? false;

        public static void Clear() => s_last?.Clear();

        public static void Save() => s_last?.Save();
    }
}
