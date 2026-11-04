using UnityEngine;

namespace Deepwave.RetroOS.Audio
{
    /// <summary>The OS sound module: a UI channel for interaction blips and a music channel for the CD player. Master volume scales this OS only - the rest of the game is left alone unless the profile opts in.</summary>
    public class AudioManager : MonoBehaviour
    {
        SoundSettings _settings = new();
        AudioSource _ui;
        AudioSource _music;

        // The game's own listener volume, so switching the opt-in off (or
        // destroying the OS) puts it back where the game had it.
        float _listenerBefore = -1f;

        public static AudioManager Create(Transform parent, SoundSettings settings)
        {
            var go = new GameObject("RetroOS Audio");
            go.transform.SetParent(parent, false);
            var manager = go.AddComponent<AudioManager>();
            manager._settings = settings ?? new SoundSettings();
            manager._ui = manager.NewSource();
            manager._music = manager.NewSource();
            manager.ApplyVolumes();
            RetroSfx.Bind(manager);
            return manager;
        }

        AudioSource NewSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        void ApplyVolumes()
        {
            // Master scales this OS's own sources only; it touches AudioListener.volume only if the profile asks.
            var master = Mathf.Clamp01(_settings.masterVolume);
            if (_ui) _ui.volume = master * _settings.uiVolume;
            if (_music) _music.volume = master * _settings.musicVolume;

            if (_settings.masterAffectsGame)
            {
                if (_listenerBefore < 0f) _listenerBefore = AudioListener.volume;
                AudioListener.volume = master;
            }
            else if (_listenerBefore >= 0f)
            {
                AudioListener.volume = _listenerBefore;
                _listenerBefore = -1f;
            }
        }

        // Puts the game's listener volume back if this OS had taken it over.
        void RestoreListener()
        {
            if (_listenerBefore < 0f) return;
            AudioListener.volume = _listenerBefore;
            _listenerBefore = -1f;
        }

        public float MasterVolume
        {
            get => _settings.masterVolume;
            set { _settings.masterVolume = Mathf.Clamp01(value); ApplyVolumes(); }
        }

        public float UiVolume
        {
            get => _settings.uiVolume;
            set { _settings.uiVolume = Mathf.Clamp01(value); ApplyVolumes(); }
        }

        public float MusicVolume
        {
            get => _settings.musicVolume;
            set { _settings.musicVolume = Mathf.Clamp01(value); ApplyVolumes(); }
        }

        // --- UI sounds -------------------------------------------------

        public void PlayUi(UiSfx kind)
        {
            var clip = Resolve(kind);
            if (clip && _ui) _ui.PlayOneShot(clip);
        }

        AudioClip Resolve(UiSfx kind)
        {
            var clip = _settings.ClipFor(kind);
            if (clip) return clip;
            return _settings.synthFallback ? RetroSfxSynth.Get(kind) : null;
        }

        // --- One-shots -------------------------------------------------

        public void Play(AudioClip clip, float volume = 1f)
        {
            if (clip && _ui) _ui.PlayOneShot(clip, volume);
        }

        // --- Music channel (one track at a time, like one CD drive) ----

        public AudioSource MusicSource => _music;
        public bool MusicPlaying => _music && _music.isPlaying;

        public void PlayMusic(AudioClip clip, bool loop = false)
        {
            if (!_music || !clip) return;
            _music.clip = clip;
            _music.loop = loop;
            _music.Play();
        }

        public void PauseMusic() { if (_music) _music.Pause(); }
        public void ResumeMusic() { if (_music) _music.UnPause(); }

        public void StopMusic()
        {
            if (!_music) return;
            _music.Stop();
            _music.clip = null;
        }

        void OnDestroy()
        {
            RestoreListener();
            RetroSfx.Unbind(this);
        }
    }

    // Static hook so code-built widgets (RetroUi, RetroDialog) can make noise
    // without holding a context reference. Bound by the running AudioManager.
    public static class RetroSfx
    {
        static AudioManager _bound;

        internal static void Bind(AudioManager manager) => _bound = manager;

        internal static void Unbind(AudioManager manager)
        {
            if (_bound == manager) _bound = null;
        }

        public static void Play(UiSfx kind) => _bound?.PlayUi(kind);
        public static void Click() => Play(UiSfx.Click);
        public static void Error() => Play(UiSfx.Error);
    }
}
