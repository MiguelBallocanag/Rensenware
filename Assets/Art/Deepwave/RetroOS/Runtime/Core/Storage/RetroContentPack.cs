using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Deepwave.RetroOS.Core.Storage
{
    /// <summary>
    /// The whole data set as one file, for the platforms that cannot read
    /// <c>StreamingAssets</c> as a folder. On Windows, macOS and Linux
    /// <see cref="RetroStorage.SourceRoot"/> is a real directory and a player
    /// copies it across; on WebGL it is a URL and on Android a zip entry, and
    /// <c>Directory.Exists</c> on either is simply false - which is why a web
    /// build booted with no disks, no sites, no pictures and no sound.
    ///
    /// The editor bakes this pack into <c>Resources</c>, which every platform can
    /// read synchronously, and the first run unpacks it into the player's own
    /// folder. After that the OS is reading real files again and nothing else in
    /// the package knows the difference.
    /// </summary>
    public static class RetroContentPack
    {
        /// <summary>What <c>Resources.Load</c> is asked for (the file is <c>Resources/RetroOsContent.bytes</c>).</summary>
        public const string ResourceName = "RetroOsContent";

        /// <summary>Where the baked file lives, relative to the project.</summary>
        public const string AssetPath = "Assets/Deepwave/RetroOS/Resources/RetroOsContent.bytes";

        /// <summary>Written into the unpacked root, so a second boot knows this pack is already out and a patched one is not.</summary>
        public const string StampName = ".content";

        const int Magic = 0x314B5052;

        // v3 stores the listing and bodies raw. v2 deflated them with
        // System.IO.Compression, whose native zlib is not linked into a WebGL
        // release build - the one platform this pack exists for - so the first
        // call into it traps the player on boot. The build already compresses
        // its data on the wire (Brotli), so a raw pack costs next to nothing
        // there. The header - magic, version, hash - has always been raw.
        const int Version = 3;

        /// <summary>Every file under <paramref name="root"/> as forward-slash paths relative to it, in a fixed order, minus Unity's sidecars. The order is part of the format: the hash has to mean the same thing on two machines.</summary>
        public static string[] Files(string root)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return Array.Empty<string>();

            var full = Path.GetFullPath(root).Replace('\\', '/').TrimEnd('/');
            var found = new List<string>();

            foreach (var file in Directory.GetFiles(full, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;

                var path = Path.GetFullPath(file).Replace('\\', '/');
                if (path.Length <= full.Length + 1) continue;
                found.Add(path[(full.Length + 1)..]);
            }

            found.Sort(StringComparer.Ordinal);
            return found.ToArray();
        }

        /// <summary>Packs a folder. <c>null</c> when there is nothing under it, so a caller can tell "no content" from "empty pack".</summary>
        public static byte[] Create(string root)
        {
            var files = Files(root);
            if (files.Length == 0) return null;

            var full = Path.GetFullPath(root).Replace('\\', '/').TrimEnd('/');
            var bodies = Read(full, files);

            using var buffer = new MemoryStream();

            // Header, then the listing and every body - all raw. The header
            // (magic, version, hash) stays first so HashIn can read it without
            // touching the rest; v3 drops the DeflateStream that used to wrap
            // everything after it.
            using (var writer = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(Magic);
                writer.Write(Version);
                writer.Write(Hash(files, bodies));

                writer.Write(files.Length);

                for (var i = 0; i < files.Length; i++)
                {
                    writer.Write(files[i]);
                    writer.Write(bodies[i].Length);
                    writer.Write(bodies[i]);
                }
            }

            return buffer.ToArray();
        }

        /// <summary>The hash a pack of this folder would carry, for telling a stale bake from a current one without unpacking either.</summary>
        public static string HashOf(string root)
        {
            var files = Files(root);
            if (files.Length == 0) return "";

            var full = Path.GetFullPath(root).Replace('\\', '/').TrimEnd('/');
            return Hash(files, Read(full, files));
        }

        /// <summary>The hash stored in a packed set, or <c>""</c> when the bytes are not one.</summary>
        public static string HashIn(byte[] pack)
        {
            if (pack == null || pack.Length < 12) return "";

            try
            {
                using var buffer = new MemoryStream(pack, writable: false);
                using var reader = new BinaryReader(buffer, Encoding.UTF8);
                if (reader.ReadInt32() != Magic || reader.ReadInt32() != Version) return "";
                return reader.ReadString();
            }
            catch (Exception)
            {
                // Not a pack, which is what the caller is asking about.
                return "";
            }
        }

        /// <summary>
        /// Writes the packed files under <paramref name="root"/>, skipping any the
        /// player already has - the same rule the folder copy follows, so a content
        /// patch cannot clobber a file they saved from Notepad.
        /// </summary>
        /// <returns>How many files were written, or -1 when the bytes are not a pack.</returns>
        public static int Unpack(byte[] pack, string root)
        {
            if (pack == null || string.IsNullOrEmpty(root)) return -1;

            try
            {
                using var buffer = new MemoryStream(pack, writable: false);
                using var reader = new BinaryReader(buffer, Encoding.UTF8);

                if (reader.ReadInt32() != Magic || reader.ReadInt32() != Version) return -1;
                reader.ReadString(); // the hash; the caller checks it against the source

                var count = reader.ReadInt32();
                if (count < 0) return -1;

                var written = 0;
                for (var i = 0; i < count; i++)
                {
                    var relative = reader.ReadString();
                    var length = reader.ReadInt32();
                    var body = reader.ReadBytes(length);

                    var target = Path.Combine(root, relative).Replace('\\', '/');

                    // A path in the pack is one this package wrote, and it is checked
                    // anyway: a file landing outside the data folder is outside every
                    // sandbox the rest of the OS relies on.
                    if (!RetroStorage.IsInside(root, target)) continue;
                    if (File.Exists(target)) continue;

                    var folder = Path.GetDirectoryName(target);
                    if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

                    // One unwritable file is one missing file, not an empty machine.
                    try
                    {
                        File.WriteAllBytes(target, body);
                        written++;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"RetroOS: could not write '{target}' out of the " +
                                         $"baked content. {e.Message}");
                    }
                }

                return written;
            }
            catch (Exception e)
            {
                Debug.LogWarning("RetroOS: the baked content could not be read, so this machine " +
                                 $"has no files. Bake it again. {e.Message}");
                return -1;
            }
        }

        static byte[][] Read(string full, string[] files)
        {
            var bodies = new byte[files.Length][];
            for (var i = 0; i < files.Length; i++)
                bodies[i] = File.ReadAllBytes(full + "/" + files[i]);
            return bodies;
        }

        // FNV-1a over path and body. Not a crypto hash and not meant to be one -
        // it answers "is this bake still the folder it was made from", and it does
        // it without dragging System.Security into an assembly that ships to WebGL.
        static string Hash(string[] files, byte[][] bodies)
        {
            const ulong prime = 1099511628211;
            var hash = 14695981039346656037;

            for (var i = 0; i < files.Length; i++)
            {
                foreach (var b in Encoding.UTF8.GetBytes(files[i]))
                {
                    hash ^= b;
                    hash *= prime;
                }

                hash ^= 0xFF;
                hash *= prime;

                foreach (var b in bodies[i])
                {
                    hash ^= b;
                    hash *= prime;
                }

                hash ^= 0xFF;
                hash *= prime;
            }

            return hash.ToString("x16");
        }
    }
}
