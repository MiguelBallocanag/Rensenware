namespace Deepwave.RetroOS.Core.FileSystem
{
    // Base for files and folders in the virtual tree.
    public abstract class VfsNode
    {
        string _displayName;
        NodeStateStore _states;

        // Only used when no store backs this node (an in-memory test/game tree);
        // disk-mounted nodes keep unlock state in the store, safe across remounts.
        bool _unlocked;

        public string Name { get; }
        public VfsDirectory Parent { get; internal set; }

        /// <summary>Where story changes to this tree are remembered. Set on the root; children inherit via Parent.</summary>
        public NodeStateStore States
        {
            get => _states ?? Parent?.States;
            set => _states = value;
        }

        /// <summary>Whether the sidecar authored this node hidden. Players see <see cref="Hidden"/>, which the story can override.</summary>
        public bool HiddenByDefault { get; set; }

        /// <summary>Whether this node is hidden right now.</summary>
        public bool Hidden
        {
            get
            {
                var states = States;
                if (states == null || !states.Any) return HiddenByDefault;
                return states.Hidden(Path) ?? HiddenByDefault;
            }
        }

        /// <summary>Hides or reveals this node and remembers it, so a remount does not undo it.</summary>
        public void SetHidden(bool hidden)
        {
            var states = States;
            if (states != null) states.SetHidden(Path, hidden);
            else HiddenByDefault = hidden;
        }

        /// <summary>Whether this node asks for a password before opening. A story lock, not real encryption - the bytes on disk are plain.</summary>
        public bool Encrypted { get; set; }

        /// <summary>What <see cref="Unlock"/> compares against. Stored in the clear.</summary>
        public string Password { get; set; }

        // Real file/folder this was mounted from; null for runtime-created nodes.
        public string SourcePath { get; set; }

        // Optional caption from the .rmeta sidecar, shown instead of Name.
        public string DisplayName
        {
            get => string.IsNullOrEmpty(_displayName) ? Name : _displayName;
            set => _displayName = value;
        }

        // Free text from the sidecar, shown in the Properties dialog.
        public string Note { get; set; }

        // What this node is, for icons and for access rules.
        public NodeRole Role { get; set; } = NodeRole.Normal;

        // Account this node belongs to. Null = visible to everyone; set = only
        // that user (or nobody signed in) may open it.
        public string Owner { get; set; }

        // True when `user` may see this node. No owner, or nobody signed in
        // (the single-user case), counts as visible.
        public bool IsVisibleTo(string user)
        {
            if (string.IsNullOrEmpty(Owner)) return true;
            if (string.IsNullOrEmpty(user)) return true;
            return string.Equals(Owner, user, System.StringComparison.OrdinalIgnoreCase);
        }

        protected VfsNode(string name) => Name = name;

        // An encrypted node stays locked until unlocked.
        public bool Locked
        {
            get
            {
                if (!Encrypted) return false;
                var states = States;
                return states == null ? !_unlocked : !states.IsUnlocked(Path);
            }
        }

        // Absolute path from root, e.g. "/C:/Docs/note.txt".
        public string Path
        {
            get
            {
                if (Parent == null) return "/";
                var p = Parent.Path;
                return p == "/" ? "/" + Name : p + "/" + Name;
            }
        }

        // Returns true when the password matches (or node is open).
        public bool Unlock(string password)
        {
            if (!Encrypted) return true;
            if (Password == password) MarkUnlocked();
            return !Locked;
        }

        // Opens the node without a password (used by game-event rules).
        public void ForceUnlock() => MarkUnlocked();

        void MarkUnlocked()
        {
            var states = States;
            if (states != null) states.Unlock(Path);
            else _unlocked = true;
        }
    }
}
