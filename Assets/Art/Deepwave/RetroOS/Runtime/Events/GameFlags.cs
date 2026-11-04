using System;
using System.Collections.Generic;

namespace Deepwave.RetroOS.Events
{
    /// <summary>Named story flags: the shared memory between what the player does in the OS and what your game logic decides to do about it. Setting or clearing one raises <c>flag.set</c> / <c>flag.cleared</c> on the bus.</summary>
    public class GameFlags
    {
        readonly HashSet<string> _flags = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Raised as (flag, isSet) whenever a flag changes.</summary>
        public event Action<string, bool> Changed;

        /// <summary>Every flag currently set.</summary>
        public IReadOnlyCollection<string> All => _flags;

        /// <summary>Whether a flag is set. Flag names ignore case.</summary>
        public bool IsSet(string flag) => !string.IsNullOrEmpty(flag) && _flags.Contains(flag);

        /// <summary>Sets a flag. Does nothing if it was already set.</summary>
        public void Set(string flag)
        {
            if (string.IsNullOrEmpty(flag) || !_flags.Add(flag)) return;
            Changed?.Invoke(flag, true);
        }

        /// <summary>Clears a flag. Does nothing if it was not set.</summary>
        public void Clear(string flag)
        {
            if (string.IsNullOrEmpty(flag) || !_flags.Remove(flag)) return;
            Changed?.Invoke(flag, false);
        }

        /// <summary>Tests a flag requirement as written in a rule: <c>flag</c> requires it to be set, <c>!flag</c> requires it to be unset, and an empty requirement always passes.</summary>
        public bool Check(string requirement)
        {
            if (string.IsNullOrWhiteSpace(requirement)) return true;
            requirement = requirement.Trim();
            return requirement.StartsWith("!") ? !IsSet(requirement.Substring(1)) : IsSet(requirement);
        }
    }
}
