using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;
using Deepwave.RetroOS.Cctv;
using Deepwave.RetroOS.Data;
using Deepwave.RetroOS.Events;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Apps
{
    // 90s security console. Channels come from the CCTV app's CameraEntry list. A channel
    // can be watched LIVE (a scene camera, still or clip) and reviewed in PLAYBACK
    // (a VideoClip recording or the in-engine DVR that buffers the feed).
    public class CctvView : AppView
    {
        // One CCTV channel: any of live device / recorded tape / still.
        class Channel
        {
            public string Label = "CAM --";
            public CctvCameraDevice Device;   // live scene camera (or null)
            public VideoClip Recording;       // pre-made tape (or null)
            public Texture Still;             // still image (or null)
            public bool AllowDvr = true;      // buffer the live feed for rewind

            public bool HasLiveSignal => Device || Still;
        }

        enum PlaySource { None, Clip, Dvr }

        const int DvrFrames = 48;             // ~8 seconds at DvrFps
        const float DvrFps = 6f;
        const float DvrInterval = 1f / DvrFps;
        const int DvrWidth = 320, DvrHeight = 240;

        // --- state ------------------------------------------------------

        readonly List<Channel> _channels = new();
        readonly List<(Button button, Text label)> _channelButtons = new();

        RawImage _feed;
        RawImage _scanlines;
        RectTransform _screenRect;
        Text _camLabel, _mode, _time, _zoom;
        Image _recDot;
        GameObject _noSignalGroup;
        RawImage _noise;
        Text _noSignalText;

        Button _modeButton;
        Text _modeButtonLabel;
        GameObject _transport;
        Button _playButton;
        Text _playButtonLabel;
        ScrubBar _scrub;

        VideoPlayer _player;
        // The camera's own picture while this monitor is watching it. The
        // device owns it; this is a borrowed reference, never destroyed here.
        RenderTexture _live;
        RenderTexture _clipTexture;
        Texture2D _noiseTexture;

        // DVR ring buffer for the currently watched live channel.
        RenderTexture[] _dvr;
        int _dvrHead, _dvrCount;
        float _dvrNextCapture;

        int _index = -1;
        bool _playback;                       // false = LIVE, true = PLAYBACK
        PlaySource _source;
        float _dvrPlayIndex;                  // fractional frame while playing
        bool _dvrPlaying;
        bool _syncingScrub;                   // guard slider<->progress feedback

        float _digitalZoom = 1f;
        float _digitalPanX;
        float _staticUntil;
        float _noiseNext;

        Channel Current => _index >= 0 && _index < _channels.Count ? _channels[_index] : null;

        List<CameraEntry> Cameras => Config<ICameraSource>()?.Cameras;

        // --- build ------------------------------------------------------

        protected override void Build()
        {
            var console = RetroUi.Panel(Root, Color.black, "Console");
            RetroUi.Stretch(console.rectTransform);
            RetroUi.VLayout(console.gameObject, 4, 6);

            BuildScreen(console.transform);
            BuildControls(console.transform);
            BuildTransport(console.transform);
            BuildChannels(console.transform);

            _player = Root.gameObject.AddComponent<VideoPlayer>();
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.isLooping = true;
            _player.playOnAwake = false;
            _player.audioOutputMode = VideoAudioOutputMode.None;

            BuildChannelData();
            Select(0);

            Window.Closed += Cleanup;
        }

        void BuildScreen(Transform parent)
        {
            var screen = RetroUi.Panel(parent, PaletteSlot.Screen, "Monitor");
            RetroUi.Flexible(screen);
            RetroUi.Bevel(screen.rectTransform, false, 1);
            _screenRect = screen.rectTransform;

            _feed = RetroUi.Raw(screen.transform);
            RetroUi.Stretch(_feed.rectTransform, 3);
            _feed.raycastTarget = false;

            _scanlines = RetroUi.Raw(screen.transform);
            RetroUi.Stretch(_scanlines.rectTransform, 3);
            _scanlines.texture = ScanlineTexture();
            _scanlines.raycastTarget = false;

            _camLabel = Overlay(screen.transform, "CAM --", TextAnchor.UpperLeft, new Vector2(8, -6));
            _mode = Overlay(screen.transform, "LIVE", TextAnchor.UpperRight, new Vector2(-24, -6));
            _time = Overlay(screen.transform, "", TextAnchor.LowerLeft, new Vector2(8, 6));
            _zoom = Overlay(screen.transform, "", TextAnchor.LowerRight, new Vector2(-8, 6));

            _recDot = RetroUi.Panel(screen.transform, new Color(0.9f, 0.1f, 0.1f), "Rec");
            var recRt = _recDot.rectTransform;
            recRt.anchorMin = recRt.anchorMax = new Vector2(1, 1);
            recRt.pivot = new Vector2(1, 1);
            recRt.sizeDelta = new Vector2(9, 9);
            recRt.anchoredPosition = new Vector2(-9, -9);
            _recDot.raycastTarget = false;

            _noSignalGroup = RetroUi.NewRect("NoSignal", screen.transform).gameObject;
            RetroUi.Stretch((RectTransform)_noSignalGroup.transform, 3);
            _noise = RetroUi.Raw(_noSignalGroup.transform);
            RetroUi.Stretch(_noise.rectTransform);
            _noise.raycastTarget = false;
            _noiseTexture = new Texture2D(96, 96, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            _noise.texture = _noiseTexture;
            _noSignalText = RetroUi.Label(_noSignalGroup.transform, "NO SIGNAL", 22, TextAnchor.MiddleCenter, RetroSkin.Mono);
            RetroPalette.Paint(_noSignalText, PaletteSlot.ScreenText);
            RetroUi.Stretch(_noSignalText.rectTransform);
            _noSignalGroup.SetActive(false);
        }

        Text Overlay(Transform parent, string text, TextAnchor corner, Vector2 offset)
        {
            var label = RetroUi.Label(parent, text, 14, corner, RetroSkin.Mono);
            RetroPalette.Paint(label, PaletteSlot.ScreenText);
            RetroUi.Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(offset.x > 0 ? offset.x : 8, offset.y > 0 ? offset.y : 6);
            label.rectTransform.offsetMax = new Vector2(offset.x < 0 ? offset.x : -8, offset.y < 0 ? offset.y : -6);
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = Color.black;
            shadow.effectDistance = new Vector2(1, -1);
            return label;
        }

        void BuildControls(Transform parent)
        {
            // On the console's own face: two bare spacers came out as black holes in the grey toolbar.
            var row = (RectTransform)RetroUi.Panel(parent, PaletteSlot.Face, "Controls").transform;
            RetroUi.Size(row, h: 30);
            RetroUi.HLayout(row.gameObject, 4, 0);

            RetroUi.SoftSize(RetroUi.Button(row, "PREV", () => Select(_index - 1)), 58, 40);
            RetroUi.SoftSize(RetroUi.Button(row, "NEXT", () => Select(_index + 1)), 58, 40);
            RetroUi.Spacer(row);
            _modeButton = RetroUi.Button(row, "PLAYBACK", ToggleMode);
            _modeButtonLabel = _modeButton.GetComponentInChildren<Text>();
            RetroUi.SoftSize(_modeButton, 96, 70);
            RetroUi.Spacer(row);
            RetroUi.SoftSize(RetroUi.HoldButton(row, "< PAN", dt => Pan(-1f, dt)), 66, 44);
            RetroUi.SoftSize(RetroUi.HoldButton(row, "PAN >", dt => Pan(1f, dt)), 66, 44);
            RetroUi.SoftSize(RetroUi.HoldButton(row, "ZOOM -", dt => Zoom(-1f, dt)), 72, 48);
            RetroUi.SoftSize(RetroUi.HoldButton(row, "ZOOM +", dt => Zoom(1f, dt)), 72, 48);
        }

        // Transport bar for PLAYBACK: rewind, play/pause and a scrub track.
        void BuildTransport(Transform parent)
        {
            var row = (RectTransform)RetroUi.Panel(parent, PaletteSlot.Face, "Transport").transform;
            RetroUi.Size(row, h: 26);
            RetroUi.HLayout(row.gameObject, 4, 0);

            RetroUi.SoftSize(RetroUi.Button(row, "|<< REW", Rewind), 78, 54);
            _playButton = RetroUi.Button(row, "> PLAY", TogglePlay);
            _playButtonLabel = _playButton.GetComponentInChildren<Text>();
            RetroUi.SoftSize(_playButton, 78, 54);
            _scrub = ScrubBar.Build(row);
            _scrub.OnScrub = OnScrub;

            _transport = row.gameObject;
            _transport.SetActive(false);
        }

        void BuildChannels(Transform parent)
        {
            var row = RetroUi.NewRect("Channels", parent);
            RetroUi.Size(row, h: 26);
            RetroUi.HLayout(row.gameObject, 4, 0, true);
            _channelRow = row;
        }

        RectTransform _channelRow;

        void BuildChannelData()
        {
            var used = new HashSet<CctvCameraDevice>();

            var cameras = Cameras;
            if (cameras != null)
                foreach (var cam in cameras)
                {
                    if (cam == null) continue;
                    var device = CctvCameraDevice.Find(cam.deviceId);

                    // A channel with no device is a deliberate dead camera; one that names a missing device is a typo - both read NO SIGNAL, so warn only for the typo.
                    if (device == null && !cam.staticImage &&
                        !string.IsNullOrWhiteSpace(cam.deviceId))
                        Debug.LogWarning($"RetroOS: CCTV channel '{cam.label}' is pointed at " +
                                         $"device '{cam.deviceId}', and no CctvCameraDevice in " +
                                         "the scene answers to it - so the channel will read NO " +
                                         "SIGNAL. Check the Device Id against the camera.");
                    _channels.Add(new Channel
                    {
                        Label = cam.label,
                        Device = device,
                        Recording = cam.recording,
                        Still = cam.staticImage,
                        AllowDvr = cam.allowDvr
                    });
                    if (device) used.Add(device);
                }

            // Scene devices not referenced by the app appear as extra channels.
            foreach (var device in CctvCameraDevice.All)
                if (!used.Contains(device))
                    _channels.Add(new Channel { Label = device.label, Device = device });

            if (_channels.Count == 0) _channels.Add(new Channel { Label = "CAM --" });

            for (var i = 0; i < _channels.Count; i++)
            {
                var index = i;
                var button = RetroUi.Button(_channelRow, _channels[i].Label, () => Select(index));
                _channelButtons.Add((button, button.GetComponentInChildren<Text>()));
            }
        }

        // --- channel / mode switching ----------------------------------

        void Select(int index)
        {
            if (_channels.Count == 0) return;
            StopFeeds();

            _index = (index % _channels.Count + _channels.Count) % _channels.Count;
            _digitalZoom = 1f;
            _digitalPanX = 0f;
            _playback = false;
            ResetDvr();
            ApplyUv();

            var channel = Current;
            _camLabel.text = channel.Label;
            StartLive();
            _staticUntil = Time.unscaledTime + 0.2f;

            for (var i = 0; i < _channelButtons.Count; i++)
                _channelButtons[i].label.fontStyle = i == _index ? FontStyle.Bold : FontStyle.Normal;

            RefreshModeUi();
            Os.Events.Raise(OsEvents.CctvSelected, channel.Label);
        }

        void ToggleMode()
        {
            if (_playback) { _playback = false; StartLive(); }
            else if (CanPlayback()) { _playback = true; StartPlayback(); }
            RefreshModeUi();
        }

        bool CanPlayback()
        {
            var c = Current;
            if (c == null) return false;
            return c.Recording || (c.Device && c.AllowDvr && _dvrCount > 0);
        }

        void StartLive()
        {
            _player.Stop();
            _dvrPlaying = false;
            _source = PlaySource.None;
            _digitalZoom = 1f;
            _digitalPanX = 0f;
            ApplyUv();

            var c = Current;
            if (c.Device)
            {
                _live = c.Device.Watch(this);
                SetFeedTexture(_live);
            }
            else if (c.Still) SetFeedTexture(c.Still);
            else SetFeedTexture(null);
        }

        void StartPlayback()
        {
            var c = Current;
            if (c.Device) { c.Device.StopWatching(this); _live = null; }
            _digitalZoom = 1f;
            _digitalPanX = 0f;
            ApplyUv();

            if (c.Recording)
            {
                _source = PlaySource.Clip;
                _player.clip = c.Recording;
                _player.targetTexture = ClipTexture();
                SetFeedTexture(ClipTexture());
                _player.Play();
                _dvrPlaying = true;
            }
            else
            {
                _source = PlaySource.Dvr;
                _dvrPlayIndex = Mathf.Max(0, _dvrCount - 1);
                _dvrPlaying = false;
                ShowDvrFrame();
            }
        }

        void RefreshModeUi()
        {
            var canPlay = CanPlayback();
            _modeButton.interactable = _playback || canPlay;
            _modeButtonLabel.text = _playback ? "« LIVE" : "PLAYBACK »";

            _transport.SetActive(_playback);
            RefreshPlayButton();
        }

        void RefreshPlayButton()
        {
            if (!_playButtonLabel) return;
            var playing = _source == PlaySource.Clip ? _player.isPlaying : _dvrPlaying;
            _playButtonLabel.text = playing ? "|| PAUSE" : "> PLAY";
        }

        // --- transport --------------------------------------------------

        void Rewind()
        {
            if (_source == PlaySource.Clip) { _player.time = 0; }
            else if (_source == PlaySource.Dvr) { _dvrPlayIndex = 0; ShowDvrFrame(); }
            SetScrub(0f);
        }

        void TogglePlay()
        {
            if (_source == PlaySource.Clip)
            {
                if (_player.isPlaying) _player.Pause(); else _player.Play();
            }
            else if (_source == PlaySource.Dvr)
            {
                _dvrPlaying = !_dvrPlaying;
                if (_dvrPlaying && _dvrCount > 0 && _dvrPlayIndex >= _dvrCount - 1) _dvrPlayIndex = 0;
            }
            RefreshPlayButton();
        }

        void OnScrub(float value)
        {
            if (_source == PlaySource.Clip)
            {
                if (_player.length > 0) _player.time = value * _player.length;
            }
            else if (_source == PlaySource.Dvr && _dvrCount > 0)
            {
                _dvrPlayIndex = value * (_dvrCount - 1);
                ShowDvrFrame();
            }
        }

        void SetScrub(float value)
        {
            _syncingScrub = true;
            _scrub.SetValue(value);
            _syncingScrub = false;
        }

        // --- pan / zoom -------------------------------------------------

        void Pan(float dir, float dt)
        {
            var c = Current;
            // The mount if this monitor steers it, else its own picture (a second monitor on one camera, or a recording).
            if (c != null && c.Device && !_playback && c.Device.Pan(this, dir, dt)) return;
            DigitalPan(dir, dt);
        }

        void Zoom(float dir, float dt)
        {
            var c = Current;
            if (c != null && c.Device && !_playback && c.Device.Zoom(this, dir, dt)) return;
            DigitalZoom(dir, dt);
        }

        // --- feed plumbing ---------------------------------------------

        internal void SetFeedTexture(Texture texture)
        {
            _feed.texture = texture;
            _feed.color = texture ? Color.white : Color.black;
        }

        RenderTexture ClipTexture()
        {
            if (!_clipTexture) _clipTexture = new RenderTexture(640, 480, 0);
            return _clipTexture;
        }

        void StopFeeds()
        {
            var c = Current;
            if (c != null && c.Device) c.Device.StopWatching(this);
            _live = null;
            if (_player) _player.Stop();
            _dvrPlaying = false;
            _source = PlaySource.None;
        }

        // --- DVR --------------------------------------------------------

        void ResetDvr()
        {
            _dvrHead = 0;
            _dvrCount = 0;
            _dvrPlayIndex = 0;
            _dvrNextCapture = 0;
        }

        void CaptureDvr()
        {
            if (_dvr == null)
            {
                _dvr = new RenderTexture[DvrFrames];
                for (var i = 0; i < DvrFrames; i++)
                    _dvr[i] = new RenderTexture(DvrWidth, DvrHeight, 0);
            }
            if (!_live) return;
            Graphics.Blit(_live, _dvr[_dvrHead]);
            _dvrHead = (_dvrHead + 1) % DvrFrames;
            _dvrCount = Mathf.Min(_dvrCount + 1, DvrFrames);
        }

        int DvrSlot(int oldestOffset)
        {
            var oldest = (_dvrHead - _dvrCount + DvrFrames) % DvrFrames;
            return (oldest + Mathf.Clamp(oldestOffset, 0, DvrFrames - 1)) % DvrFrames;
        }

        void ShowDvrFrame()
        {
            if (_dvrCount == 0 || _dvr == null) { SetFeedTexture(null); return; }
            var i = Mathf.Clamp(Mathf.RoundToInt(_dvrPlayIndex), 0, _dvrCount - 1);
            SetFeedTexture(_dvr[DvrSlot(i)]);
        }

        // --- digital pan/zoom (recorded / still feeds) -----------------

        void DigitalZoom(float dir, float dt)
        {
            _digitalZoom = Mathf.Clamp(_digitalZoom + dir * 1.1f * dt, 1f, 3f);
            ApplyUv();
        }

        void DigitalPan(float dir, float dt)
        {
            var width = 1f / _digitalZoom;
            var limit = (1f - width) / 2f;
            _digitalPanX = Mathf.Clamp(_digitalPanX + dir * 0.35f * dt, -limit, limit);
            ApplyUv();
        }

        void ApplyUv()
        {
            var size = 1f / _digitalZoom;
            var limit = (1f - size) / 2f;
            _digitalPanX = Mathf.Clamp(_digitalPanX, -limit, limit);
            _feed.uvRect = new Rect(0.5f - size / 2f + _digitalPanX, 0.5f - size / 2f, size, size);
        }

        // --- per-frame --------------------------------------------------

        int _shownStamp = -1;
        float _shownZoom = -1f;


        void Update()
        {
            var c = Current;
            if (c == null) return;

            // The readouts change once a second and on a zoom step, so the
            // strings are only rebuilt when the value behind them moves.
            var now = Os.Clock.Now;
            var stamp = now.Hour * 3600 + now.Minute * 60 + now.Second;
            if (stamp != _shownStamp)
            {
                _shownStamp = stamp;
                _time.text = now.ToString("yyyy-MM-dd HH:mm:ss");
            }

            var zoom = ZoomReadout();
            if (!Mathf.Approximately(zoom, _shownZoom))
            {
                _shownZoom = zoom;
                _zoom.text = $"ZOOM x{zoom:0.0}";
            }

            _mode.text = _playback ? "PLAYBACK" : "LIVE";

            if (!_playback && c.Device && c.AllowDvr && Time.unscaledTime >= _dvrNextCapture)
            {
                CaptureDvr();
                _dvrNextCapture = Time.unscaledTime + DvrInterval;
                // A live channel becomes reviewable as soon as it has frames.
                if (_dvrCount == 1) RefreshModeUi();
            }

            if (_playback) UpdatePlayback();

            var signal = HasSignal();
            var blink = Time.unscaledTime % 0.9f < 0.55f;
            _recDot.enabled = !_playback && signal && blink;
            _mode.enabled = signal;

            var lines = Mathf.Max(1f, _screenRect.rect.height / 2f);
            _scanlines.uvRect = new Rect(0, 0, 1, lines);

            var showNoise = !signal || Time.unscaledTime < _staticUntil;
            if (_noSignalGroup.activeSelf != showNoise) _noSignalGroup.SetActive(showNoise);
            if (showNoise)
            {
                _noSignalText.enabled = !signal;
                if (Time.unscaledTime >= _noiseNext)
                {
                    RegenerateNoise();
                    _noiseNext = Time.unscaledTime + 0.08f;
                }
            }
        }

        void UpdatePlayback()
        {
            if (_source == PlaySource.Clip)
            {
                if (!_syncingScrub && _player.length > 0)
                    SetScrub((float)(_player.time / _player.length));
                RefreshPlayButton();
            }
            else if (_source == PlaySource.Dvr)
            {
                if (_dvrPlaying && _dvrCount > 0)
                {
                    _dvrPlayIndex += Time.unscaledDeltaTime * DvrFps;
                    if (_dvrPlayIndex >= _dvrCount - 1) { _dvrPlayIndex = _dvrCount - 1; _dvrPlaying = false; RefreshPlayButton(); }
                    ShowDvrFrame();
                }
                if (_dvrCount > 1) SetScrub(_dvrPlayIndex / (_dvrCount - 1));
            }
        }

        float ZoomReadout()
        {
            var c = Current;
            if (!_playback && c != null && c.Device) return c.Device.ZoomFactor;
            return _digitalZoom;
        }

        bool HasSignal()
        {
            var c = Current;
            if (c == null) return false;
            if (_playback) return _source == PlaySource.Clip || (_source == PlaySource.Dvr && _dvrCount > 0);
            return c.HasLiveSignal;
        }

        void RegenerateNoise()
        {
            var pixels = new Color32[96 * 96];
            for (var i = 0; i < pixels.Length; i++)
            {
                var v = (byte)Random.Range(10, 190);
                pixels[i] = new Color32(v, v, v, 255);
            }
            _noiseTexture.SetPixels32(pixels);
            _noiseTexture.Apply();
        }

        static Texture2D _scanlineTexture;

        static Texture2D ScanlineTexture()
        {
            if (_scanlineTexture) return _scanlineTexture;
            _scanlineTexture = new Texture2D(1, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };
            _scanlineTexture.SetPixels32(new[] { new Color32(0, 0, 0, 70), new Color32(0, 0, 0, 0) });
            _scanlineTexture.Apply();
            return _scanlineTexture;
        }

        bool _cleaned;

        // Runs on window close and hard teardown, releasing this monitor's textures and
        // letting go of the camera (the device puts its picture away when the last monitor stops).
        void Cleanup()
        {
            if (_cleaned) return;
            _cleaned = true;
            StopFeeds();
            if (_clipTexture) Destroy(_clipTexture);
            if (_noiseTexture) Destroy(_noiseTexture);
            if (_dvr != null)
                foreach (var rt in _dvr)
                    if (rt) Destroy(rt);
        }

        void OnDestroy() => Cleanup();
    }

    // Flat retro scrub track: click or drag to seek. Reports 0..1 and can be
    // driven back from playback progress. Works on overlay and world canvases.
    public class ScrubBar : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public System.Action<float> OnScrub;
        RectTransform _rt;
        RectTransform _fill;

        public static ScrubBar Build(Transform parent)
        {
            var track = RetroUi.Panel(parent, PaletteSlot.Field, "Scrub");
            RetroUi.Flexible(track);
            RetroUi.Bevel(track.rectTransform, false, 1);

            var fill = RetroUi.Panel(track.transform, PaletteSlot.TitleActive, "Fill").rectTransform;
            fill.anchorMin = new Vector2(0, 0);
            fill.anchorMax = new Vector2(0, 1);
            fill.offsetMin = new Vector2(2, 2);
            fill.offsetMax = new Vector2(0, -2);
            ((Image)fill.GetComponent<Image>()).raycastTarget = false;

            var bar = track.gameObject.AddComponent<ScrubBar>();
            bar._rt = track.rectTransform;
            bar._fill = fill;
            return bar;
        }

        public void SetValue(float value)
        {
            value = Mathf.Clamp01(value);

            // Between the insets, not across the whole track: the fill starts two pixels in, so measure the far edge from that.
            const float inset = 2f;
            var max = inset + Mathf.Max(0f, _rt.rect.width - inset * 2f) * value;
            _fill.anchorMax = new Vector2(0, 1);
            _fill.offsetMax = new Vector2(max, -inset);
        }

        void Seek(PointerEventData e)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, e.position, e.pressEventCamera, out var lp))
                return;
            var v = Mathf.Clamp01((lp.x - _rt.rect.xMin) / Mathf.Max(1f, _rt.rect.width));
            SetValue(v);
            OnScrub?.Invoke(v);
        }

        public void OnPointerDown(PointerEventData e) { if (RetroUi.IsPrimary(e)) Seek(e); }
        public void OnDrag(PointerEventData e) { if (RetroUi.IsPrimary(e)) Seek(e); }
    }
}
