using System;
using UnityEngine;

namespace Deepwave.RetroOS.Core.FileSystem
{
    // Leaf node: text in Content, media in Asset. Disk-backed files carry a
    // SourcePath and load lazily on first access.
    public class VfsFile : VfsNode
    {
        string _content;
        UnityEngine.Object _asset;
        bool _contentLoaded;

        public FileKind Kind { get; set; }

        public long Size { get; set; }
        public DateTime Modified { get; set; }

        // Original extension, lower case and without the dot ("txt", "html").
        public string Extension { get; set; } = "";

        // Called once, on first access, for disk-backed files.
        public Func<VfsFile, string> ContentLoader { get; set; }
        public Func<VfsFile, UnityEngine.Object> AssetLoader { get; set; }

        public VfsFile(string name, FileKind kind) : base(name) => Kind = kind;

        /// <summary>The file's text, no access checks. Internal - use <see cref="TryRead"/> from outside.</summary>
        internal string RawContent
        {
            get
            {
                if (!_contentLoaded && ContentLoader != null)
                {
                    _contentLoaded = true;
                    _content = ContentLoader(this);
                }
                return _content;
            }
        }

        /// <summary>Replaces the file's text. Saving, not reading.</summary>
        public string Content
        {
            set { _content = value; _contentLoaded = true; }
        }

        /// <summary>The file's decoded media, with nothing checked.</summary>
        internal UnityEngine.Object RawAsset
        {
            get
            {
                if (!_asset && AssetLoader != null) _asset = AssetLoader(this);
                return _asset;
            }
        }

        /// <summary>Replaces the file's decoded media.</summary>
        public UnityEngine.Object Asset
        {
            set => _asset = value;
        }

        /// <summary>The file's text, if <paramref name="viewer"/> may read it. The access gate lives here, on the leaf.</summary>
        /// <param name="viewer">Signed-in account; <c>null</c> is a single-user machine with no privacy to enforce.</param>
        public bool TryRead(string viewer, out string content)
        {
            content = null;
            if (!IsVisibleTo(viewer) || Locked) return false;
            content = RawContent;
            return true;
        }

        /// <summary>The file's decoded media, if <paramref name="viewer"/> may read it. The media half of <see cref="TryRead"/>.</summary>
        public bool TryLoad(string viewer, out UnityEngine.Object asset)
        {
            asset = null;
            if (!IsVisibleTo(viewer) || Locked) return false;
            asset = RawAsset;
            return true;
        }

        // True when the file has text a viewer can show without loading media.
        public bool IsText => Kind is FileKind.Text or FileKind.Web;

        // Human-readable size for list views ("4.2 KB").
        public string SizeText => Size <= 0 ? ""
            : Size < 1024 ? Size + " bytes"
            : Size < 1024 * 1024 ? (Size / 1024f).ToString("0.#") + " KB"
            : (Size / 1048576f).ToString("0.#") + " MB";
    }
}
