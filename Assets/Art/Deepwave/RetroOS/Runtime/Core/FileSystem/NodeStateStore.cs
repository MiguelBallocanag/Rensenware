using System;
using System.Collections.Generic;

namespace Deepwave.RetroOS.Core.FileSystem
{
    /// <summary>
    /// What the story has done to the file system (hidden/unlocked), kept by
    /// path so a <see cref="Storage.DriveBay.Refresh"/> remount does not lose it.
    /// </summary>
    public class NodeStateStore
    {
        // Paths whose hidden flag the story overrode. Absent = as the sidecar authored.
        readonly Dictionary<string, bool> _hidden =
            new(StringComparer.OrdinalIgnoreCase);

        // Paths opened this session, by password or a forcing rule.
        readonly HashSet<string> _unlocked =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Whether anything is remembered at all. Checked before a node builds its path.</summary>
        public bool Any => _hidden.Count > 0 || _unlocked.Count > 0;

        /// <summary>The story's opinion on this path being hidden, or <c>null</c> if it has none.</summary>
        public bool? Hidden(string path) =>
            path != null && _hidden.TryGetValue(path, out var hidden) ? hidden : null;

        /// <summary>Remembers that this path is hidden, or is not.</summary>
        public void SetHidden(string path, bool hidden)
        {
            if (!string.IsNullOrEmpty(path)) _hidden[path] = hidden;
        }

        /// <summary>Whether this path has been opened.</summary>
        public bool IsUnlocked(string path) => path != null && _unlocked.Contains(path);

        /// <summary>Remembers that this path has been opened.</summary>
        public void Unlock(string path)
        {
            if (!string.IsNullOrEmpty(path)) _unlocked.Add(path);
        }

        /// <summary>Forgets everything. For a power-off, never for a <see cref="Storage.DriveBay.Refresh"/>.</summary>
        public void Clear()
        {
            _hidden.Clear();
            _unlocked.Clear();
        }
    }
}
