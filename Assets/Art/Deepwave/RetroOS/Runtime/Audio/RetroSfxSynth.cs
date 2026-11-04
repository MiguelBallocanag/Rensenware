using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deepwave.RetroOS.Audio
{
    // Generates the built-in UI sounds in code (same spirit as RetroIcons):
    // simple square/sine tones so the OS makes noise with zero audio assets.
    public static class RetroSfxSynth
    {
        const int Rate = 22050;

        static readonly Dictionary<UiSfx, AudioClip> Cache = new();

        public static AudioClip Get(UiSfx kind)
        {
            if (Cache.TryGetValue(kind, out var cached)) return cached;
            var clip = Build(kind);
            Cache[kind] = clip;
            return clip;
        }

        static AudioClip Build(UiSfx kind) => kind switch
        {
            UiSfx.Click => Clip("sfx_click", Tone(1600, 0.03f, 0.22f, square: true, decay: 60)),
            UiSfx.WindowOpen => Clip("sfx_open", Join(Tone(523, 0.06f, 0.2f), Tone(784, 0.09f, 0.2f, decay: 24))),
            UiSfx.WindowClose => Clip("sfx_close", Join(Tone(784, 0.06f, 0.2f), Tone(523, 0.09f, 0.2f, decay: 24))),
            UiSfx.Error => Clip("sfx_error", Mix(Tone(196, 0.22f, 0.22f, square: true, decay: 8),
                                                 Tone(207, 0.22f, 0.18f, square: true, decay: 8))),
            UiSfx.Startup => Clip("sfx_startup", Startup()),
            UiSfx.Login => Clip("sfx_login", Mix(Tone(880, 0.5f, 0.22f, decay: 6),
                                                 Tone(1320, 0.5f, 0.1f, decay: 8))),
            UiSfx.Notify => Clip("sfx_notify", Join(Tone(1175, 0.09f, 0.2f, decay: 18),
                                                    Silence(0.04f),
                                                    Tone(1568, 0.14f, 0.2f, decay: 14))),
            UiSfx.DiscInsert => Clip("sfx_disc_in", Join(Tone(1600, 0.02f, 0.15f, square: true, decay: 70),
                                                         Sweep(300, 900, 0.14f, 0.18f))),
            UiSfx.DiscEject => Clip("sfx_disc_out", Join(Sweep(900, 300, 0.14f, 0.18f),
                                                         Tone(1600, 0.02f, 0.15f, square: true, decay: 70))),
            _ => Clip("sfx_beep", Tone(880, 0.1f, 0.2f))
        };

        // Soft ascending chord arpeggio, the classic boot fanfare.
        static float[] Startup()
        {
            var notes = new[] { 330f, 494f, 659f, 831f };
            var parts = new float[notes.Length][];
            for (var i = 0; i < notes.Length; i++)
                parts[i] = Tone(notes[i], i == notes.Length - 1 ? 0.9f : 0.22f, 0.18f, decay: i == notes.Length - 1 ? 3 : 6);
            return Join(parts);
        }

        static float[] Tone(float freq, float duration, float amp, bool square = false, float decay = 12)
        {
            var n = Mathf.Max(1, (int)(duration * Rate));
            var data = new float[n];
            for (var i = 0; i < n; i++)
            {
                var t = i / (float)Rate;
                var s = Mathf.Sin(2 * Mathf.PI * freq * t);
                if (square) s = Mathf.Sign(s);
                data[i] = s * amp * Mathf.Exp(-decay * t);
            }
            return Fade(data);
        }

        static float[] Sweep(float from, float to, float duration, float amp)
        {
            var n = Mathf.Max(1, (int)(duration * Rate));
            var data = new float[n];
            var phase = 0f;
            for (var i = 0; i < n; i++)
            {
                var k = i / (float)n;
                phase += 2 * Mathf.PI * Mathf.Lerp(from, to, k) / Rate;
                data[i] = Mathf.Sin(phase) * amp * (1f - 0.4f * k);
            }
            return Fade(data);
        }

        static float[] Silence(float duration) => new float[Mathf.Max(1, (int)(duration * Rate))];

        static float[] Join(params float[][] parts)
        {
            var total = 0;
            foreach (var p in parts) total += p.Length;
            var data = new float[total];
            var at = 0;
            foreach (var p in parts)
            {
                Array.Copy(p, 0, data, at, p.Length);
                at += p.Length;
            }
            return data;
        }

        static float[] Mix(params float[][] parts)
        {
            var length = 0;
            foreach (var p in parts) length = Mathf.Max(length, p.Length);
            var data = new float[length];
            foreach (var p in parts)
                for (var i = 0; i < p.Length; i++) data[i] += p[i];
            return data;
        }

        // Tiny edge fades prevent clicks at clip boundaries.
        static float[] Fade(float[] data)
        {
            var edge = Mathf.Min(64, data.Length / 4);
            for (var i = 0; i < edge; i++)
            {
                var k = i / (float)edge;
                data[i] *= k;
                data[data.Length - 1 - i] *= k;
            }
            return data;
        }

        static AudioClip Clip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
