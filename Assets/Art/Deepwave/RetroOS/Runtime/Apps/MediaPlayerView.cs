using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Deepwave.RetroOS.Core.FileSystem;
using Deepwave.RetroOS.Core.Storage;
using Deepwave.RetroOS.Events;
using Deepwave.RetroOS.UI;
using Deepwave.RetroOS.UI.Components;

namespace Deepwave.RetroOS.Apps
{
    // Media Player: a video surface with a transport. Plays a clip asset or streams a
    // file off disk, with the rest of the folder queued behind it.
    public class MediaPlayerView : AppView
    {
        readonly List<VfsFile> _clips = new();

        VideoPlayer _player;
        AudioSource _audio;
        RenderTexture _texture;
        RawImage _surface;
        RetroTrackBar _seek;
        RetroStatusStrip _status;
        RetroPushButton _playButton;
        RetroListView _list;
        RetroNavDrawer _playlist;

        int _index = -1;
        int _shownSecond = -1;
        bool _scrubbing;
        bool _loop;
        float _speed = 1f;

        VfsFile Current => _index >= 0 && _index < _clips.Count ? _clips[_index] : null;
        double Duration => _player && _player.frameCount > 0 && _player.frameRate > 0f
            ? _player.frameCount / _player.frameRate : 0d;

        protected override void Build()
        {
            RetroUi.VLayout(Root.gameObject, 2, 2);
            BuildMenu();
            BuildSurface();
            BuildTransport();

            _status = RetroUi.StatusStrip(Root, -1f, 70f, 60f);

            Os.FsChanged += OnFsChanged;
            Rescan();
            if (Current != null) OpenClip(_index, autoPlay: false);
        }

        void BuildMenu()
        {
            var menu = RetroUi.Menu(Root);

            menu.AddMenu("File")
                .Command("Open Playlist", () => TogglePlaylist(), "Ctrl+L")
                .Separator()
                .Command("Close", () => Window.Close(), "Alt+F4");

            menu.AddMenu("Play")
                .Command("Play/Pause", TogglePlay, "Space")
                .Command("Stop", Stop)
                .Separator()
                .Command("Rewind 10s", () => Seek(-10), "Left")
                .Command("Forward 10s", () => Seek(10), "Right")
                .Separator()
                .Command("Previous", Previous)
                .Command("Next", Next);

            menu.AddMenu("View")
                .Command("Full Window", () => Window.ToggleMaximize(), "Alt+Enter")
                .Separator()
                .Check("Repeat", _loop, ToggleLoop);

            menu.AddMenu("Speed")
                .Command("Slow (0.5x)", () => SetSpeed(0.5f))
                .Command("Normal (1x)", () => SetSpeed(1f))
                .Command("Fast (1.5x)", () => SetSpeed(1.5f))
                .Command("Double (2x)", () => SetSpeed(2f));

            menu.AddMenu("Help").Command("About", () => RetroDialog.Info(Os.Overlay, "Media Player",
                "Plays the video files on this machine.\n" +
                "Open the playlist to jump between them."));
        }

        void BuildSurface()
        {
            var split = RetroUi.NewRect("Split", Root);
            RetroUi.Flexible(split);
            RetroUi.HLayout(split.gameObject, 2, 0);

            var screen = RetroUi.Panel(split, PaletteSlot.Screen, "Screen");
            RetroUi.Flexible(screen);
            RetroUi.Bevel(screen.rectTransform, false, 1);

            _surface = RetroUi.Raw(screen.transform);
            RetroUi.Stretch(_surface.rectTransform, 2);

            // Double-clicking the picture fills the window, as it has since
            // every media player ever written.
            var relay = screen.gameObject.AddComponent<RetroDoubleClick>();
            relay.Clicked = () => Window.ToggleMaximize();

            _texture = new RenderTexture(640, 480, 0) { name = "Media Player" };
            _surface.texture = _texture;

            _player = Root.gameObject.AddComponent<VideoPlayer>();
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.targetTexture = _texture;
            _player.isLooping = false;
            _player.playOnAwake = false;
            _player.skipOnDrop = true;
            _player.loopPointReached += _ => ClipEnded();

            _audio = Root.gameObject.AddComponent<AudioSource>();
            _player.audioOutputMode = VideoAudioOutputMode.AudioSource;
            _player.SetTargetAudioSource(0, _audio);

            // The playlist folds away rather than disappearing, so the picture
            // widens into the room it leaves instead of jumping.
            _playlist = RetroNavDrawer.Create(split, "Playlist", 190f, "media.playlist", Os.Prefs);

            _list = RetroUi.ListView(_playlist.Body, ListViewMode.List);
            RetroUi.Flexible(_list);
            _list.Activated += entry =>
            {
                if (entry?.Tag is VfsFile file) OpenClip(_clips.IndexOf(file), autoPlay: true);
            };
        }

        void BuildTransport()
        {
            _seek = RetroUi.TrackBar(Root, 0f, null);
            RetroUi.Size(_seek, h: 18f);
            _seek.Changed += Scrub;
            _seek.Committed += CommitScrub;

            var row = RetroUi.NewRect("Controls", Root);
            RetroUi.Size(row, h: 28);
            RetroUi.HLayout(row.gameObject, 3, 0);

            RetroUi.SoftSize(RetroUi.PushButton(row, "|<<", Previous), 38f, 28f);
            RetroUi.SoftSize(RetroUi.PushButton(row, "<<", () => Seek(-10)), 34f, 26f);
            _playButton = RetroUi.PushButton(row, "> Play", TogglePlay);
            RetroUi.SoftSize(_playButton, 66f, 48f);
            RetroUi.SoftSize(RetroUi.PushButton(row, "[] Stop", Stop), 62f, 44f);
            RetroUi.SoftSize(RetroUi.PushButton(row, ">>", () => Seek(10)), 34f, 26f);
            RetroUi.SoftSize(RetroUi.PushButton(row, ">>|", Next), 38f, 28f);
            RetroUi.Spacer(row);

            RetroUi.Size(RetroUi.NoWrap(RetroUi.Label(row, "Vol", 12, TextAnchor.MiddleRight)), w: 24f);
            var volume = RetroUi.TrackBar(row, 1f, v => { if (_audio) _audio.volume = v; });
            RetroUi.SoftSize(volume, 84f, 50f);
        }

        // --- playlist -------------------------------------------------------

        void OnFsChanged(FsChangedArgs change)
        {
            if (!this || _list == null) return;

            var folder = Context.File?.Parent;
            if (folder == null || change.Touches(folder.Path)) Rescan();
        }

        void Rescan()
        {
            if (!this || _list == null) return;

            var keep = Current;
            _clips.Clear();

            var folder = Context.File?.Parent;
            if (folder != null) CollectHere(folder);
            else Collect(Os.Fs.Root);

            _list.Clear();
            foreach (var file in _clips)
                _list.Add(new RetroListEntry
                {
                    Text = file.Name,
                    Icon = RetroIcons.Get(BuiltinIcon.Movie),
                    Tag = file
                });

            var restored = keep != null ? _clips.IndexOf(keep) : -1;
            if (restored >= 0) _index = restored;
            else if (Context.File != null) _index = _clips.IndexOf(Context.File);
            if (_index < 0 && _clips.Count > 0) _index = 0;

            if (Current != null) _list.SelectTag(Current);
            UpdateStatus();
        }

        void CollectHere(VfsDirectory dir)
        {
            foreach (var node in Os.List(dir))
                if (node is VfsFile file && file.Kind == FileKind.Video && !file.Locked)
                    _clips.Add(file);
        }

        void Collect(VfsDirectory dir)
        {
            foreach (var node in Os.List(dir))
            {
                if (node.Locked) continue;
                if (node is VfsDirectory sub) Collect(sub);
                else if (node is VfsFile file && file.Kind == FileKind.Video) _clips.Add(file);
            }
        }

        void TogglePlaylist()
        {
            if (_playlist) _playlist.Toggle();
        }

        // --- playback -------------------------------------------------------

        // An imported VideoClip if the file has one, else stream by URL (the only way a runtime-added video plays).
        void OpenClip(int index, bool autoPlay)
        {
            if (index < 0 || index >= _clips.Count) return;
            _index = index;

            var file = _clips[index];
            file.TryLoad(Os.CurrentUser, out var asset);
            if (asset is VideoClip clip)
            {
                _player.source = VideoSource.VideoClip;
                _player.clip = clip;
            }
            else if (!string.IsNullOrEmpty(file.SourcePath))
            {
                _player.source = VideoSource.Url;
                _player.url = MediaLoader.FileUrl(file.SourcePath);
            }
            else
            {
                UpdateStatus($"'{file.Name}' could not be read.");
                return;
            }

            _player.playbackSpeed = _speed;
            _player.isLooping = _loop;
            _list.SelectTag(file);
            Window.SetTitle($"{file.Name} - {App?.Title ?? "Media Player"}");

            if (autoPlay) _player.Play();
            else _player.Prepare();

            Raise("open", file.Path);
            UpdateStatus();
        }

        void TogglePlay()
        {
            if (!_player) return;
            if (_player.isPlaying) _player.Pause();
            else if (Current != null) _player.Play();
            UpdateStatus();
        }

        void Stop()
        {
            if (!_player) return;
            _player.Stop();
            _seek.SetValue(0f, false);
            ClearSurface();
            UpdateStatus();
        }

        void Seek(double seconds)
        {
            if (!_player || Duration <= 0d) return;
            _player.time = Math.Clamp(_player.time + seconds, 0d, Duration - 0.1d);
        }

        void Next()
        {
            if (_clips.Count == 0) return;
            OpenClip((_index + 1) % _clips.Count, autoPlay: true);
        }

        void Previous()
        {
            if (_clips.Count == 0) return;

            // Rewinding part-way through restarts the clip first.
            if (_player && _player.time > 3d) { _player.time = 0d; return; }
            OpenClip((_index - 1 + _clips.Count) % _clips.Count, autoPlay: true);
        }

        void SetSpeed(float speed)
        {
            _speed = speed;
            if (_player) _player.playbackSpeed = speed;
            UpdateStatus();
        }

        void ToggleLoop()
        {
            _loop = !_loop;
            if (_player) _player.isLooping = _loop;
            UpdateStatus();
        }

        void ClipEnded()
        {
            Raise("ended", Current?.Path);
            if (_loop) return;
            if (_index < _clips.Count - 1) Next();
            else UpdateStatus("End of playlist.");
        }

        // --- scrubbing ------------------------------------------------------

        void Scrub(float value)
        {
            if (Duration <= 0d) return;
            _scrubbing = true;
            _shownSecond = -1;
            _status?.Set(1, Time((int)(value * Duration)));
        }

        void CommitScrub(float value)
        {
            if (!_scrubbing) return;
            _scrubbing = false;
            if (Duration <= 0d) return;
            _player.time = Math.Clamp(value * Duration, 0d, Duration - 0.1d);
        }

        // --- display --------------------------------------------------------

        void UpdateStatus(string message = null)
        {
            if (_playButton) _playButton.Caption = _player && _player.isPlaying ? "|| Pause" : "> Play";
            if (_status == null) return;

            _status.Set(0, message ?? (_clips.Count == 0
                ? "No video files on this machine"
                : $"{Current?.Name}  ({_index + 1} of {_clips.Count})"));
            _status.Set(2, _speed == 1f ? (_loop ? "Repeat" : "") : _speed.ToString("0.#") + "x");
        }

        void Update()
        {
            if (!_player) return;

            var duration = Duration;
            if (_player.isPlaying && !_scrubbing && duration > 0d)
                _seek.SetValue(Mathf.Clamp01((float)(_player.time / duration)), false);

            var seconds = (int)_player.time;
            if (_scrubbing || seconds == _shownSecond) return;
            _shownSecond = seconds;
            _status?.Set(1, duration > 0d
                ? $"{Time(seconds)} / {Time((int)duration)}"
                : Time(seconds));
        }

        // Stop leaves the last frame on the texture; a stopped player shows
        // black, the way a stopped player should.
        void ClearSurface()
        {
            if (!_texture) return;
            var previous = RenderTexture.active;
            RenderTexture.active = _texture;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = previous;
        }

        static string Time(int seconds) => $"{seconds / 60:00}:{seconds % 60:00}";

        void OnDestroy()
        {
            if (Os != null) Os.FsChanged -= OnFsChanged;
            if (_texture) Destroy(_texture);
        }
    }
}
