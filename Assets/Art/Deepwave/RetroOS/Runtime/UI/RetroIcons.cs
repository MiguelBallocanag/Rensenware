using System;
using System.Collections.Generic;
using UnityEngine;
using Deepwave.RetroOS.UI.Icons;

namespace Deepwave.RetroOS.UI
{
    /// <summary>The built-in icon set, painted at runtime by <c>RetroIconLibrary</c> at the theme's resolution (32/48/64) and cached per icon, size and style.</summary>
    public static class RetroIcons
    {
        static readonly Dictionary<Key, Sprite> s_sprites = new();
        static readonly Dictionary<Key, Texture2D> s_textures = new();

        // Which entry painted each handed-out sprite, recorded at paint time
        // (the only moment it is known without parsing the sprite's name).
        static readonly Dictionary<Sprite, BuiltinIcon> s_painted = new();

        readonly struct Key : IEquatable<Key>
        {
            readonly BuiltinIcon _icon;
            readonly int _size;
            readonly bool _vivid;

            public Key(BuiltinIcon icon, int size, bool vivid)
            {
                _icon = icon;
                _size = size;
                _vivid = vivid;
            }

            public bool Equals(Key other) =>
                _icon == other._icon && _size == other._size && _vivid == other._vivid;

            public override bool Equals(object obj) => obj is Key other && Equals(other);

            public override int GetHashCode() =>
                ((int)_icon * 397 ^ _size) * 397 ^ (_vivid ? 1 : 0);
        }

        /// <summary>Every icon in declaration order - for the editor icon browser and the app inspector's picker.</summary>
        public static readonly BuiltinIcon[] All = (BuiltinIcon[])Enum.GetValues(typeof(BuiltinIcon));

        static RetroIcons() => RetroSkin.Changed += Invalidate;

        // Cosmetic sprite name, for the inspector and profiler captures. Nothing depends on it.
        const string NamePrefix = "RetroIcon.";

        /// <summary>Repaints a built-in icon at the size it is about to be drawn (downscaling drops the outline). A sprite from elsewhere is returned unchanged.</summary>
        public static Sprite AtSize(Sprite sprite, int size)
        {
            if (!sprite || size <= 0 || !sprite.texture) return sprite;
            if (sprite.texture.width == size) return sprite;
            return s_painted.TryGetValue(sprite, out var icon)
                ? Get(icon, size, RetroSkin.Theme.vividIcons)
                : sprite;
        }

        public static Sprite Get(BuiltinIcon icon) =>
            Get(icon, (int)RetroSkin.Theme.iconResolution, RetroSkin.Theme.vividIcons);

        public static Sprite Get(BuiltinIcon icon, int size, bool vivid = true)
        {
            var key = new Key(icon, size, vivid);
            if (s_sprites.TryGetValue(key, out var cached) && cached) return cached;

            var canvas = new IconCanvas(size);
            RetroIconLibrary.Paint(icon, canvas, vivid);
            var sprite = canvas.ToSprite(NamePrefix + icon);
            s_sprites[key] = sprite;
            s_painted[sprite] = icon;
            return sprite;
        }

        /// <summary>A flat texture of one icon for previews, <c>RawImage</c> or <c>GUI.DrawTexture</c>. Shared and cached - use <see cref="CreateTexture"/> for a copy you own.</summary>
        public static Texture2D GetTexture(BuiltinIcon icon, int size, bool vivid = true)
        {
            var key = new Key(icon, size, vivid);
            if (s_textures.TryGetValue(key, out var cached) && cached) return cached;

            var texture = CreateTexture(icon, size, vivid);
            texture.hideFlags = HideFlags.HideAndDontSave;
            s_textures[key] = texture;
            return texture;
        }

        /// <summary>A freshly painted texture the caller owns and must destroy.</summary>
        public static Texture2D CreateTexture(BuiltinIcon icon, int size, bool vivid = true)
        {
            var canvas = new IconCanvas(size);
            RetroIconLibrary.Paint(icon, canvas, vivid);
            return canvas.ToTexture(NamePrefix + icon);
        }

        /// <summary>Drops the icon cache so the next request repaints at the theme's new size/style. Does NOT destroy the sprites - on-screen Images still hold them; the orphaned set is collected by <see cref="ReleaseAll"/> on shutdown.</summary>
        public static void Invalidate()
        {
            s_sprites.Clear();
            s_textures.Clear();

            // Only drop keys for sprites Unity has already destroyed; the rest are still on screen.
            Forget();
        }

        /// <summary>Destroys every icon this class painted. For OS teardown only - anything still showing one is left with a dead sprite.</summary>
        public static void ReleaseAll()
        {
            foreach (var sprite in s_sprites.Values)
            {
                if (!sprite) continue;
                var texture = sprite.texture;
                Destroy(sprite);
                Destroy(texture);
            }
            s_sprites.Clear();

            foreach (var texture in s_textures.Values) Destroy(texture);
            s_textures.Clear();
            s_painted.Clear();
        }

        // Drops keys for sprites Unity has already destroyed, so the map is not a slow leak.
        static void Forget()
        {
            List<Sprite> dead = null;
            foreach (var sprite in s_painted.Keys)
            {
                if (sprite) continue;
                (dead ??= new List<Sprite>()).Add(sprite);
            }

            if (dead == null) return;
            foreach (var sprite in dead) s_painted.Remove(sprite);
        }

        // Generated, not assets, so use the immediate variant outside play mode.
        static void Destroy(UnityEngine.Object target)
        {
            if (!target) return;
#if UNITY_EDITOR
            if (!Application.isPlaying) { UnityEngine.Object.DestroyImmediate(target); return; }
#endif
            UnityEngine.Object.Destroy(target);
        }

        // --- named shortcuts for existing call sites ---

        public static Sprite Computer => Get(BuiltinIcon.Computer);
        public static Sprite Terminal => Get(BuiltinIcon.Terminal);
        public static Sprite Camera => Get(BuiltinIcon.Camera);
        public static Sprite Mail => Get(BuiltinIcon.Mail);
        public static Sprite Globe => Get(BuiltinIcon.Globe);
        public static Sprite Photos => Get(BuiltinIcon.Photos);
        public static Sprite Disc => Get(BuiltinIcon.Disc);
        public static Sprite Folder => Get(BuiltinIcon.Folder);
        public static Sprite FolderOpen => Get(BuiltinIcon.FolderOpen);
        public static Sprite Flag => Get(BuiltinIcon.Flag);
        public static Sprite Clock => Get(BuiltinIcon.Clock);
        public static Sprite Movie => Get(BuiltinIcon.Movie);
        public static Sprite Note => Get(BuiltinIcon.Note);
        public static Sprite File => Get(BuiltinIcon.File);
        public static Sprite Drive => Get(BuiltinIcon.Drive);
        public static Sprite Network => Get(BuiltinIcon.Network);
        public static Sprite Lock => Get(BuiltinIcon.Lock);
        public static Sprite Warning => Get(BuiltinIcon.Warning);
        public static Sprite Error => Get(BuiltinIcon.Error);
        public static Sprite Info => Get(BuiltinIcon.Info);
        public static Sprite Question => Get(BuiltinIcon.Question);
        public static Sprite Trash => Get(BuiltinIcon.Trash);
        public static Sprite Search => Get(BuiltinIcon.Search);
        public static Sprite Settings => Get(BuiltinIcon.Settings);
        public static Sprite Home => Get(BuiltinIcon.Home);
        public static Sprite Refresh => Get(BuiltinIcon.Refresh);
        public static Sprite Back => Get(BuiltinIcon.Back);
        public static Sprite Forward => Get(BuiltinIcon.Forward);
        public static Sprite Stop => Get(BuiltinIcon.Stop);
        public static Sprite Bookmark => Get(BuiltinIcon.Bookmark);
        public static Sprite BrokenImage => Get(BuiltinIcon.BrokenImage);
    }
}
