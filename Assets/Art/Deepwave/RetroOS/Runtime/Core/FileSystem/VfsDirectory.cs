using System;
using System.Collections.Generic;
using System.Linq;

namespace Deepwave.RetroOS.Core.FileSystem
{
    // Folder holding child nodes. A disk-backed folder does not read itself
    // until something looks inside (the network branch is rebuilt constantly).
    public class VfsDirectory : VfsNode
    {
        readonly List<VfsNode> _children = new();

        // Reads this folder off disk, run once on first access. Null for in-memory folders.
        Action<VfsDirectory> _filler;

        // Set when this folder is an alias for another - see PointAt.
        VfsDirectory _alias;

        public VfsDirectory(string name) : base(name) { }

        /// <summary>
        /// Makes this folder a second name for <paramref name="target"/>, not a
        /// second copy - so paths, locks and story state all key off the real folder.
        /// </summary>
        public void PointAt(VfsDirectory target)
        {
            _alias = target == this ? null : target;
            if (_alias == null) return;

            _filler = null;
            SourcePath = _alias.SourcePath;
        }

        /// <summary>The folder this one is a second name for, or <c>null</c>.</summary>
        public VfsDirectory Target => _alias;

        /// <summary>Every entry, hidden and private included. Internal - <see cref="List"/> is the way in that knows who is looking.</summary>
        internal IReadOnlyList<VfsNode> Children
        {
            get
            {
                if (_alias != null) return _alias.Children;
                Fill();
                return _children;
            }
        }

        /// <summary>Sets a filler run once on first access; it should <see cref="Add{T}"/> what it finds.</summary>
        public void FillWith(Action<VfsDirectory> filler) => _filler = filler;

        /// <summary>Whether the folder has been read yet - lets a caller gauge the cost of listing it.</summary>
        public bool Loaded => _alias != null ? _alias.Loaded : _filler == null;

        void Fill()
        {
            if (_filler == null) return;

            // Cleared before the call so a filler that calls Add does not recurse here.
            var filler = _filler;
            _filler = null;
            filler(this);
        }

        public T Add<T>(T node) where T : VfsNode
        {
            if (_alias != null) return _alias.Add(node);

            Fill();
            node.Parent = this;
            _children.Add(node);
            return node;
        }

        public void Remove(VfsNode node)
        {
            if (_alias != null) { _alias.Remove(node); return; }

            Fill();
            if (_children.Remove(node)) node.Parent = null;
        }

        // Fluent helper for the places that build a tree inline.
        public VfsDirectory WithNote(string note)
        {
            Note = note;
            return this;
        }

        public VfsNode Find(string name)
        {
            if (_alias != null) return _alias.Find(name);

            Fill();
            return _children.FirstOrDefault(c =>
                string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        // Children; hidden ones only when asked, never another user's private
        // folders. viewer is the signed-in account; null for an unrestricted listing.
        public IEnumerable<VfsNode> List(bool showHidden, string viewer = null)
        {
            if (_alias != null) return _alias.List(showHidden, viewer);

            Fill();
            return _children.Where(c => (showHidden || !c.Hidden) && c.IsVisibleTo(viewer));
        }
    }
}
