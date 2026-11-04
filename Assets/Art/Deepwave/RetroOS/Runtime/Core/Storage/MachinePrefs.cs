using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Deepwave.RetroOS.Core.Storage
{
    /// <summary>
    /// What one machine remembers between sessions (colour scheme, clock format,
    /// best times, icon positions). Stored beside that machine's drive folders,
    /// not in <c>PlayerPrefs</c>, so two machines keep their own.
    /// </summary>
    public class MachinePrefs
    {
        const string FileName = "machine.prefs";

        readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
        readonly string _path;
        bool _dirty;

        /// <summary>The machine folder this store belongs to.</summary>
        public string Folder { get; }

        /// <summary>Whether there is somewhere to read from and write to.</summary>
        public bool Ready => _path != null;

        internal MachinePrefs(string machineFolder)
        {
            Folder = machineFolder;
            if (string.IsNullOrWhiteSpace(machineFolder)) return;

            _path = Path.Combine(machineFolder, FileName).Replace('\\', '/');
            Load();
        }

        // --- reading ---

        public string Get(string key, string fallback = null) =>
            key != null && _values.TryGetValue(key, out var value) ? value : fallback;

        public int GetInt(string key, int fallback = 0) =>
            int.TryParse(Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value : fallback;

        public float GetFloat(string key, float fallback = 0f) =>
            float.TryParse(Get(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value : fallback;

        public bool GetBool(string key, bool fallback = false)
        {
            var raw = Get(key);
            if (raw == null) return fallback;
            return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
        }

        public bool Has(string key) => key != null && _values.ContainsKey(key);

        // --- writing ---

        /// <summary>Records a value. <c>null</c> forgets the key, so a setting returns to its default.</summary>
        public void Set(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            if (value == null)
            {
                if (_values.Remove(key)) _dirty = true;
                return;
            }

            if (_values.TryGetValue(key, out var existing) && existing == value) return;
            _values[key] = value;
            _dirty = true;
        }

        public void SetInt(string key, int value) =>
            Set(key, value.ToString(CultureInfo.InvariantCulture));

        public void SetFloat(string key, float value) =>
            Set(key, value.ToString("0.####", CultureInfo.InvariantCulture));

        public void SetBool(string key, bool value) => Set(key, value ? "1" : "0");

        /// <summary>Keeps the lower (or higher) of a value and what is stored - a best time.</summary>
        /// <returns><c>true</c> when the new value is the better one.</returns>
        public bool SetBest(string key, int value, bool lowerIsBetter = true)
        {
            if (Has(key))
            {
                var stored = GetInt(key);
                var better = lowerIsBetter ? value < stored : value > stored;
                if (!better) return false;
            }

            SetInt(key, value);
            Save();
            return true;
        }

        /// <summary>Throws away everything this machine remembered.</summary>
        public void Clear()
        {
            if (_values.Count == 0) return;
            _values.Clear();
            _dirty = true;
            Save();
        }

        // --- disk ---

        /// <summary>Writes the file if anything changed; a no-op otherwise.</summary>
        public void Save()
        {
            if (!_dirty || _path == null) return;
            _dirty = false;

            try
            {
                var folder = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

                var text = new StringBuilder();
                foreach (var pair in _values)
                {
                    // One key per line, `.rmeta`-style.
                    text.Append(pair.Key).Append(": ").Append(Escape(pair.Value)).Append('\n');
                }

                File.WriteAllText(_path, text.ToString());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"RetroOS: could not save settings to '{_path}'. {e.Message}");
            }
        }

        void Load()
        {
            if (_path == null || !File.Exists(_path)) return;

            try
            {
                foreach (var line in File.ReadAllLines(_path))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var split = line.IndexOf(':');
                    if (split <= 0) continue;

                    var key = line[..split].Trim();
                    if (key.Length == 0) continue;
                    _values[key] = Unescape(line[(split + 1)..].Trim());
                }
            }
            catch (Exception e)
            {
                // An unreadable settings file is not worth failing a boot over.
                Debug.LogWarning($"RetroOS: could not read settings at '{_path}'. {e.Message}");
                _values.Clear();
            }
        }

        // Newlines would break the one-per-line format, so they travel escaped.
        static string Escape(string value) =>
            (value ?? "").Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "");

        static string Unescape(string value)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOf('\\') < 0) return value ?? "";

            var text = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] != '\\' || i + 1 >= value.Length) { text.Append(value[i]); continue; }

                i++;
                text.Append(value[i] switch
                {
                    'n' => '\n',
                    '\\' => '\\',
                    _ => value[i]
                });
            }
            return text.ToString();
        }

        public override string ToString() => $"MachinePrefs({Folder})";
    }
}
