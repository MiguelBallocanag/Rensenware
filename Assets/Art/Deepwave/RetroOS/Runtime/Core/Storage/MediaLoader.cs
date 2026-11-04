using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Deepwave.RetroOS.Core.FileSystem;

namespace Deepwave.RetroOS.Core.Storage
{
    /// <summary>Loads pictures and sound straight off disk - no Unity import step, no asset references.</summary>
    public static class MediaLoader
    {
        // Decoded media, cached by path and owned here: a texture built from a file
        // is not parented to the Image showing it, so every Load returns a shared instance.
        static readonly Dictionary<string, Texture2D> s_textures = new();
        static readonly Dictionary<string, Sprite> s_sprites = new();
        static readonly Dictionary<string, AudioClip> s_clips = new();

        public static UnityEngine.Object Load(string path, FileKind kind) => kind switch
        {
            FileKind.Image => LoadSprite(path),
            FileKind.Audio => LoadAudio(path),
            _ => null
        };

        /// <summary>Releases every decoded file. Cached references are dead after this.</summary>
        public static void ClearCache()
        {
            foreach (var sprite in s_sprites.Values) Release(sprite);
            s_sprites.Clear();

            foreach (var texture in s_textures.Values) Release(texture);
            s_textures.Clear();

            foreach (var clip in s_clips.Values) Release(clip);
            s_clips.Clear();
        }

        static void Release(UnityEngine.Object target)
        {
            if (!target) return;
#if UNITY_EDITOR
            if (!Application.isPlaying) { UnityEngine.Object.DestroyImmediate(target); return; }
#endif
            UnityEngine.Object.Destroy(target);
        }

        // --- images ---

        /// <summary>A sprite for a picture on disk. Shared and cached - do not destroy it.</summary>
        public static Sprite LoadSprite(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (s_sprites.TryGetValue(path, out var cached) && cached) return cached;

            var texture = LoadTexture(path);
            if (!texture) return null;

            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f);
            sprite.name = Path.GetFileNameWithoutExtension(path);
            s_sprites[path] = sprite;
            return sprite;
        }

        /// <summary>The texture behind a picture on disk. Shared and cached - use <see cref="DecodeTexture"/> for a copy you own.</summary>
        public static Texture2D LoadTexture(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (s_textures.TryGetValue(path, out var cached) && cached) return cached;

            var texture = DecodeTexture(path);
            if (texture) s_textures[path] = texture;
            return texture;
        }

        /// <summary>Decodes a picture into a new texture the caller owns and must destroy. Prefer <see cref="LoadTexture"/> inside the OS.</summary>
        public static Texture2D DecodeTexture(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                var bytes = File.ReadAllBytes(path);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = Path.GetFileName(path),
                    wrapMode = TextureWrapMode.Clamp
                };
                if (!texture.LoadImage(bytes, markNonReadable: false))
                {
                    UnityEngine.Object.Destroy(texture);
                    Debug.LogWarning($"RetroOS: '{path}' is not a picture Unity can read, so " +
                                     "nothing will show where it is used. PNG, JPG and GIF are " +
                                     "the formats it decodes from a file.");
                    return null;
                }
                // Period-correct: no smoothing on small pixel art.
                texture.filterMode = texture.width <= 256 && texture.height <= 256
                    ? FilterMode.Point
                    : FilterMode.Bilinear;
                texture.Apply();
                return texture;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"RetroOS: could not load image '{path}'. {e.Message}");
                return null;
            }
        }

        // --- audio ---

        /// <summary>A clip for a sound on disk. Shared and cached. <c>null</c> for formats needing Unity's async decoder.</summary>
        public static AudioClip LoadAudio(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (s_clips.TryGetValue(path, out var cached) && cached) return cached;

            // Compressed formats stream through the media apps' own request.
            if (!path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) return null;

            var clip = LoadWav(path);
            if (clip) s_clips[path] = clip;
            return clip;
        }

        // Minimal RIFF/WAVE reader: PCM 8/16/24/32-bit and 32-bit float, any rate/channels.
        public static AudioClip LoadWav(string path)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                if (bytes.Length < 44 ||
                    bytes[0] != 'R' || bytes[1] != 'I' || bytes[2] != 'F' || bytes[3] != 'F')
                {
                    Debug.LogWarning($"RetroOS: '{path}' is named .wav but is not a RIFF/WAVE " +
                                     "file, so it will not play. Re-export it as an uncompressed " +
                                     "WAV.");
                    return null;
                }

                int channels = 1, sampleRate = 44100, bits = 16, format = 1;
                var dataOffset = -1;
                var dataLength = 0;

                var pos = 12;
                while (pos + 8 <= bytes.Length)
                {
                    var id = System.Text.Encoding.ASCII.GetString(bytes, pos, 4);
                    var size = BitConverter.ToInt32(bytes, pos + 4);
                    var body = pos + 8;

                    if (id == "fmt ")
                    {
                        format = BitConverter.ToInt16(bytes, body);
                        channels = BitConverter.ToInt16(bytes, body + 2);
                        sampleRate = BitConverter.ToInt32(bytes, body + 4);
                        bits = BitConverter.ToInt16(bytes, body + 14);
                    }
                    else if (id == "data")
                    {
                        dataOffset = body;
                        dataLength = Mathf.Min(size, bytes.Length - body);
                        break;
                    }

                    pos = body + size + (size & 1);
                }

                if (dataOffset < 0 || channels <= 0 || bits <= 0)
                {
                    Debug.LogWarning($"RetroOS: '{path}' is a WAV this reader cannot use " +
                                     $"(data chunk {(dataOffset < 0 ? "missing" : "found")}, " +
                                     $"{channels} channel(s), {bits}-bit), so it will not play.");
                    return null;
                }

                var bytesPerSample = bits / 8;
                var sampleCount = dataLength / bytesPerSample;
                var samples = new float[sampleCount];

                for (var i = 0; i < sampleCount; i++)
                {
                    var at = dataOffset + i * bytesPerSample;
                    samples[i] = bits switch
                    {
                        8 => (bytes[at] - 128) / 128f,
                        16 => BitConverter.ToInt16(bytes, at) / 32768f,
                        24 => ((bytes[at] | (bytes[at + 1] << 8) | ((sbyte)bytes[at + 2] << 16)) / 8388608f),
                        32 => format == 3
                            ? BitConverter.ToSingle(bytes, at)
                            : BitConverter.ToInt32(bytes, at) / 2147483648f,
                        _ => 0f
                    };
                }

                var clip = AudioClip.Create(Path.GetFileNameWithoutExtension(path),
                    sampleCount / channels, channels, sampleRate, false);
                clip.SetData(samples, 0);
                return clip;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"RetroOS: could not load audio '{path}'. {e.Message}");
                return null;
            }
        }

        // file:// url for VideoPlayer and anything else that streams.
        public static string FileUrl(string path) =>
            string.IsNullOrEmpty(path) ? "" : "file://" + path.Replace('\\', '/');
    }
}
