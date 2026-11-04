using System;
using UnityEngine;

namespace Deepwave.RetroOS.Audio
{
    // Configures the audio module. All clips are optional:
    // empty slots fall back to synthesized retro sounds (if enabled).
    [Serializable]
    public class SoundSettings
    {
        [Tooltip("Scales everything this OS plays. It does NOT touch the rest " +
                 "of your game unless the option below is switched on.")]
        [Range(0f, 1f)] public float masterVolume = 1f;

        [Range(0f, 1f)] public float uiVolume = 0.7f;
        [Range(0f, 1f)] public float musicVolume = 1f;

        [Tooltip("Let the OS master volume drive the global AudioListener, so " +
                 "its volume slider controls the whole game. Only for a game " +
                 "that IS this OS - leave it off when the OS is one prop in a " +
                 "larger world, or it will fight your own audio settings.")]
        public bool masterAffectsGame;

        [Tooltip("Use built-in synthesized sounds for empty clip slots.")]
        public bool synthFallback = true;

        [Header("Clip Overrides (optional)")]
        public AudioClip click;
        public AudioClip windowOpen;
        public AudioClip windowClose;
        public AudioClip error;
        public AudioClip startup;
        public AudioClip login;
        public AudioClip notify;
        public AudioClip discInsert;
        public AudioClip discEject;

        public AudioClip ClipFor(UiSfx kind) => kind switch
        {
            UiSfx.Click => click,
            UiSfx.WindowOpen => windowOpen,
            UiSfx.WindowClose => windowClose,
            UiSfx.Error => error,
            UiSfx.Startup => startup,
            UiSfx.Login => login,
            UiSfx.Notify => notify,
            UiSfx.DiscInsert => discInsert,
            UiSfx.DiscEject => discEject,
            _ => null
        };
    }
}
