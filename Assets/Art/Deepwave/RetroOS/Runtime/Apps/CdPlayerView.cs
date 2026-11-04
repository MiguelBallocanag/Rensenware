using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Deepwave.RetroOS.Core.FileSystem;
using Deepwave.RetroOS.Events;
using Deepwave.RetroOS.UI;
using Deepwave.RetroOS.UI.Components;

namespace Deepwave.RetroOS.Apps
{
    // CD Player with a green LCD: every audio file beside the opened one is a track,
    // so a folder of WAVs is an album. Plays through the OS music channel (one disc
    // at a time, driven by the Control Panel's music slider).
    public class CdPlayerView : AppView
    {
        // How the player picks the next track when one ends.
        enum PlayOrder { Normal, Random, Continuous }

        readonly List<VfsFile> _tracks = new();
        readonly System.Random _shuffle = new();

        Text _lcd;
        Text _title;
        RetroTrackBar _seek;
        RetroListView _list;
        RetroStatusStrip _status;
        RetroPushButton _playButton;
        RetroPanelHost _pages;
        RectTransform _lyricPage;
        Text _lyricText;
        ScrollRect _lyricScroll;
        readonly List<LrcLine> _lyrics = new();
        int _lyricLine = -2;

        int _index = -1;
        int _shownSecond = -1;
        bool _scrubbing;
        bool _wasPlaying;
        PlayOrder _order = PlayOrder.Normal;
        bool _repeat = true;

        AudioClip Clip => Read(Current) as AudioClip;

        // Every track read goes through the access gate, even the ones that only want its length.
        UnityEngine.Object Read(VfsFile file) =>
            file != null && file.TryLoad(Os.CurrentUser, out var asset) ? asset : null;
        VfsFile Current => _index >= 0 && _index < _tracks.Count ? _tracks[_index] : null;
        AudioSource Source => Os.Audio ? Os.Audio.MusicSource : null;
        bool Mine => Source && Clip && Source.clip == Clip;

        protected override void Build()
        {
            RetroUi.VLayout(Root.gameObject, 4, 4);
            BuildMenu();
            BuildDisplay();
            BuildTransport();
            BuildPlaylist();

            _status = RetroUi.StatusStrip(Root, -1f, 90f);

            // Only when something moved on this player's own disc.
            Os.FsChanged += OnFsChanged;
            Rescan();
        }

        void BuildMenu()
        {
            var menu = RetroUi.Menu(Root);

            menu.AddMenu("Disc")
                .Command("Play", Play)
                .Command("Pause", Pause)
                .Command("Stop", Stop)
                .Separator()
                .Command("Previous Track", Previous, "Ctrl+B")
                .Command("Next Track", Next, "Ctrl+F")
                .Separator()
                .Command("Eject", Eject);

            menu.AddMenu("View")
                .Command("Track List", () => _pages.Show(0), null,
                    RetroIcons.Get(BuiltinIcon.Music))
                .Command("Lyrics", () => _pages.Show(1), null, RetroIcons.Note)
                .Separator()
                .Command("Refresh Disc", Rescan, "F5");

            menu.AddMenu("Options")
                .Check("Random Order", _order == PlayOrder.Random,
                    () => SetOrder(_order == PlayOrder.Random ? PlayOrder.Normal : PlayOrder.Random))
                .Check("Continuous Play", _order == PlayOrder.Continuous,
                    () => SetOrder(_order == PlayOrder.Continuous ? PlayOrder.Normal : PlayOrder.Continuous))
                .Separator()
                .Check("Repeat Track", _repeat, () => _repeat = !_repeat);

            menu.AddMenu("Help").Command("About", () => RetroDialog.Info(Os.Overlay, "CD Player",
                "Every sound file in this folder is a track on the disc.\n" +
                "Double-click a track to play it.\n\n" +
                "A track with a .lrc file of the same name beside it has words: " +
                "View > Lyrics follows them as it plays."));
        }

        void BuildDisplay()
        {
            var lcdPanel = RetroUi.Panel(Root, PaletteSlot.Screen, "Lcd");
            RetroUi.Size(lcdPanel, h: 48);
            RetroUi.Bevel(lcdPanel.rectTransform, false, 1);

            _lcd = RetroUi.Label(lcdPanel.transform, "[--] 00:00", 22, TextAnchor.MiddleLeft, RetroSkin.Mono);
            RetroPalette.Paint(_lcd, PaletteSlot.ScreenText);
            RetroUi.NoWrap(_lcd);
            var lrt = _lcd.rectTransform;
            RetroUi.Stretch(lrt, 8);
            lrt.offsetMax = new Vector2(-8, -4);

            _title = RetroUi.Label(lcdPanel.transform, "No disc", 12, TextAnchor.LowerLeft, RetroSkin.Mono);
            RetroPalette.Paint(_title, PaletteSlot.ScreenDim);
            RetroUi.NoWrap(_title);
            var trt = _title.rectTransform;
            RetroUi.Stretch(trt, 8);
            trt.offsetMin = new Vector2(8, 3);

            // Scrubbing has to pause the clock, or the readout fights the drag.
            _seek = RetroUi.TrackBar(Root, 0f, null);
            RetroUi.Size(_seek, h: 20f);
            _seek.Changed += Scrub;
            _seek.Committed += CommitScrub;
        }

        void BuildTransport()
        {
            var row = RetroUi.NewRect("Transport", Root);
            RetroUi.Size(row, h: 28);
            RetroUi.HLayout(row.gameObject, 3, 0);

            RetroUi.Size(RetroUi.PushButton(row, "|<<", Previous), w: 42f);
            _playButton = RetroUi.PushButton(row, "> Play", TogglePlay);
            RetroUi.Size(_playButton, w: 70f);
            RetroUi.Size(RetroUi.PushButton(row, "[] Stop", Stop), w: 70f);
            RetroUi.Size(RetroUi.PushButton(row, ">>|", Next), w: 42f);
            RetroUi.Spacer(row);

            RetroUi.Size(RetroUi.NoWrap(RetroUi.Label(row, "Vol", 12, TextAnchor.MiddleRight)), w: 26f);
            var volume = RetroUi.TrackBar(row, Os.Audio ? Os.Audio.MusicVolume : 1f,
                v => { if (Os.Audio) Os.Audio.MusicVolume = v; });
            RetroUi.Size(volume, w: 90f);
        }

        void BuildPlaylist()
        {
            // The disc's tracks and its words share the space, because a
            // player only ever wants to look at one of them.
            _pages = RetroPanelHost.Create(Root);
            RetroUi.Flexible(_pages.Rect);

            var listPage = _pages.AddPanel("Playlist");
            RetroUi.VLayout(listPage.gameObject, 0, 0);

            _lyricPage = _pages.AddPanel("Lyrics");
            RetroUi.VLayout(_lyricPage.gameObject, 0, 0);
            var words = RetroUi.VScroll(_lyricPage, out _lyricScroll);
            RetroUi.Flexible(RetroUi.ScrollFrame(_lyricScroll));
            _lyricText = RetroUi.Label(words, "", 13, TextAnchor.UpperCenter, RetroSkin.Mono);

            _pages.Show(0);

            _list = RetroUi.ListView(listPage, ListViewMode.Details);
            RetroUi.Flexible(_list);
            _list.SetColumns(new[] { "Track", "Name", "Length" }, new[] { 52f, -1f, 70f });
            _list.Activated += entry =>
            {
                if (entry?.Tag is VfsFile file) PlayTrack(_tracks.IndexOf(file));
            };
        }

        // Another audio file was double-clicked while open: the disc changes to that folder and plays.
        public override void OnFileOpened(VfsFile file)
        {
            if (file == null || Context == null) return;

            Context.File = file;
            Rescan();

            var index = _tracks.IndexOf(file);
            if (index >= 0) PlayTrack(index);
        }

        // Only this player's disc: a save three drives away is not its concern; a disc in/out always is.
        void OnFsChanged(FsChangedArgs change)
        {
            if (!this || _list == null) return;

            var folder = Context.File?.Parent;
            if (folder == null || change.Touches(folder.Path)) Rescan();
        }

        // The disc is the folder the opened file came from; with nothing
        // opened, whatever audio the machine can see.
        void Rescan()
        {
            if (!this || _list == null) return;

            var wasPlaying = Current;
            _tracks.Clear();

            var folder = Context.File?.Parent;
            if (folder != null) CollectHere(folder);
            else Collect(Os.Fs.Root);

            _list.Clear();
            for (var i = 0; i < _tracks.Count; i++)
            {
                var file = _tracks[i];
                _list.Add(new RetroListEntry
                {
                    Text = (i + 1).ToString("00"),
                    Icon = RetroIcons.Get(BuiltinIcon.Music),
                    Columns = new[] { file.Name, LengthOf(file) },
                    Tag = file
                });
            }

            // Keep playing whatever was playing across a refresh.
            var keep = wasPlaying != null ? _tracks.IndexOf(wasPlaying) : -1;
            if (keep >= 0) _index = keep;
            else if (Context.File != null) _index = _tracks.IndexOf(Context.File);
            if (_index < 0 && _tracks.Count > 0) _index = 0;

            Highlight();
            LoadLyrics();
            UpdateStatus();
        }

        void CollectHere(VfsDirectory dir)
        {
            foreach (var node in Os.List(dir))
                if (node is VfsFile file && file.Kind == FileKind.Audio && !file.Locked)
                    _tracks.Add(file);
        }

        void Collect(VfsDirectory dir)
        {
            foreach (var node in Os.List(dir))
            {
                if (node.Locked) continue;
                if (node is VfsDirectory sub) Collect(sub);
                else if (node is VfsFile file && file.Kind == FileKind.Audio) _tracks.Add(file);
            }
        }

        // Reading a clip's length means decoding it, so an unplayed track
        // reports nothing rather than stalling the window while it loads.
        string LengthOf(VfsFile file) =>
            Read(file) is AudioClip clip ? Time(Mathf.RoundToInt(clip.length)) : "--:--";

        // --- transport ------------------------------------------------------

        void TogglePlay()
        {
            if (Mine && Source.isPlaying) Pause();
            else Play();
        }

        void Play()
        {
            if (Current == null) { UpdateStatus("No disc in the drive."); return; }

            var clip = Clip;
            if (!clip) { UpdateStatus($"'{Current.Name}' could not be read."); return; }

            // Resume where it was paused rather than starting the track again.
            if (Mine && !Source.isPlaying && Source.time > 0f) Os.Audio.ResumeMusic();
            else Os.Audio.PlayMusic(clip);

            Raise("play", Current.Path);
            UpdateStatus();
        }

        void Pause()
        {
            Os.Audio?.PauseMusic();
            UpdateStatus();
        }

        void Stop()
        {
            Os.Audio?.StopMusic();
            _seek.SetValue(0f, false);
            UpdateStatus();
        }

        void PlayTrack(int index)
        {
            if (index < 0 || index >= _tracks.Count) return;
            _index = index;
            Highlight();
            LoadLyrics();
            Os.Audio?.StopMusic();
            Play();
        }

        void Next() => Step(1);
        void Previous() => Step(-1);

        void Step(int direction)
        {
            if (_tracks.Count == 0) return;

            // Rewinding mid-track restarts it, the way a real deck does.
            if (direction < 0 && Mine && Source.time > 3f) { PlayTrack(_index); return; }

            var next = _order == PlayOrder.Random && _tracks.Count > 1
                ? RandomOther()
                : (_index + direction + _tracks.Count) % _tracks.Count;
            PlayTrack(next);
        }

        int RandomOther()
        {
            var pick = _index;
            while (pick == _index) pick = _shuffle.Next(_tracks.Count);
            return pick;
        }

        void Eject()
        {
            Stop();
            _index = -1;
            _tracks.Clear();
            _list.Clear();
            Highlight();
            UpdateStatus("Disc ejected.");
            Raise("eject");
        }

        void SetOrder(PlayOrder order)
        {
            _order = order;
            UpdateStatus();
        }

        // --- scrubbing ------------------------------------------------------

        void Scrub(float value)
        {
            if (!Mine) return;
            if (!_scrubbing)
            {
                _scrubbing = true;
                _wasPlaying = Source.isPlaying;
            }
            _shownSecond = -1;
            WriteLcd(Mathf.RoundToInt(value * Clip.length));
        }

        void CommitScrub(float value)
        {
            if (!_scrubbing || !Mine) { _scrubbing = false; return; }
            _scrubbing = false;
            Source.time = Mathf.Clamp(value * Clip.length, 0f, Clip.length - 0.05f);
            if (_wasPlaying && !Source.isPlaying) Source.Play();
        }

        // --- display --------------------------------------------------------

        void Highlight()
        {
            if (Current != null) _list.SelectTag(Current);
            _title.text = Current?.Name ?? "No disc";
        }

        void UpdateStatus(string message = null)
        {
            if (_playButton) _playButton.Caption = Mine && Source.isPlaying ? "|| Pause" : "> Play";
            if (_status == null) return;

            var mode = _order switch
            {
                PlayOrder.Random => "Random",
                PlayOrder.Continuous => "Continuous",
                _ => "Normal"
            };
            _status.Set(0, message ?? (_tracks.Count == 0
                ? "No disc"
                : $"Track {_index + 1} of {_tracks.Count}"));
            _status.Set(1, mode);
        }

        void Update()
        {
            var playing = Mine && Source.isPlaying;
            var seconds = Mine ? (int)Source.time : 0;

            if (playing && !_scrubbing && Clip && Clip.length > 0f)
                _seek.SetValue(Mathf.Clamp01(Source.time / Clip.length), false);

            if (Mine && _lyrics.Count > 0) TrackLyrics(Source.time);

            // A track that stopped on its own is back at zero; a paused one is part-way through.
            if (_playingLastFrame && !playing && !_scrubbing && Mine && Source.time <= 0f)
            {
                _playingLastFrame = false;
                TrackEnded();
                return;
            }
            _playingLastFrame = playing;

            // The display only shows whole seconds; rebuilding the string every
            // frame would allocate for nothing.
            if (_scrubbing || seconds == _shownSecond) return;
            _shownSecond = seconds;
            WriteLcd(seconds);
            UpdateStatus();
        }

        bool _playingLastFrame;

        void TrackEnded()
        {
            Raise("trackended", Current?.Path);

            if (_repeat) { PlayTrack(_index); return; }

            // Normal order stops at the end of the disc; the other two wrap.
            var last = _index >= _tracks.Count - 1;
            if (_order == PlayOrder.Normal && last) { UpdateStatus("End of disc."); return; }
            Next();
        }

        // A track's words are the .lrc file next to it: same folder, same name.
        void LoadLyrics()
        {
            _lyrics.Clear();
            _lyricLine = -2;

            var track = Current;
            var folder = track?.Parent as VfsDirectory;

            if (folder != null)
            {
                var stem = System.IO.Path.GetFileNameWithoutExtension(track.Name);
                // Through Os.List, not the folder directly - a transcript can belong to somebody else.
                foreach (var node in Os.List(folder))
                {
                    if (node is not VfsFile file || file.Extension != "lrc") continue;
                    if (!string.Equals(System.IO.Path.GetFileNameWithoutExtension(file.Name),
                            stem, StringComparison.OrdinalIgnoreCase)) continue;

                    // Checked at the leaf too: the listing respects who is signed in, this read did not.
                    if (file.TryRead(Os.CurrentUser, out var lrc))
                        _lyrics.AddRange(LrcLyrics.Parse(lrc));
                    break;
                }
            }

            DrawLyrics(_lyrics.Count > 0 ? -1 : -2);
        }

        void DrawLyrics(int current)
        {
            if (!_lyricText) return;

            _lyricLine = current;

            if (_lyrics.Count == 0)
            {
                RetroPalette.Paint(_lyricText, PaletteSlot.TextDisabled);
                _lyricText.text = Current == null
                    ? "\nNo disc."
                    : "\nNo words for this track.\n\n" +
                      "Drop a .lrc file next to it and they show up here.";
                return;
            }

            RetroPalette.Paint(_lyricText, PaletteSlot.Text);

            var dim = ColorUtility.ToHtmlStringRGB(RetroSkin.TextDisabled);
            var builder = new System.Text.StringBuilder();

            for (var i = 0; i < _lyrics.Count; i++)
            {
                var words = _lyrics[i].Text;
                if (words.Length == 0) words = "~";

                // Everything but the line being sung is turned down, so the
                // eye lands on the right one without having to read.
                builder.AppendLine(i == current ? words : $"<color=#{dim}>{words}</color>");
            }

            _lyricText.text = builder.ToString();
        }

        // Follows the playhead and a scrub: the line is derived from the time, not counted forward.
        void TrackLyrics(float seconds)
        {
            if (_lyrics.Count == 0) return;

            var line = LrcLyrics.LineAt(_lyrics, seconds);
            if (line == _lyricLine) return;

            DrawLyrics(line);

            if (!_lyricScroll || _lyrics.Count < 2) return;
            Canvas.ForceUpdateCanvases();
            _lyricScroll.verticalNormalizedPosition =
                1f - Mathf.Clamp01(line / (float)(_lyrics.Count - 1));
        }

        void WriteLcd(int seconds)
        {
            var number = _index >= 0 ? (_index + 1).ToString("00") : "--";
            _lcd.text = $"[{number}] {Time(seconds)}";
        }

        static string Time(int seconds) => $"{seconds / 60:00}:{seconds % 60:00}";

        // Closing takes the disc out (like CD Player's "Stop CD playing on exit"). For music
        // behind the desktop, call os.Audio.PlayMusic instead.
        void OnDestroy()
        {
            if (Os != null) Os.FsChanged -= OnFsChanged;

            // Logging off tears the whole window layer down, and the audio
            // service can already be gone by the time this runs.
            if (Os?.Audio == null) return;
            if (Mine) Os.Audio.StopMusic();
        }
    }
}
