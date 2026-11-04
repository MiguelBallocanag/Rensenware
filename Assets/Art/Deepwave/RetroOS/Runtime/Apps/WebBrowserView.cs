using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Deepwave.RetroOS.Core.FileSystem;
using Deepwave.RetroOS.Core.Storage;
using Deepwave.RetroOS.Data;
using Deepwave.RetroOS.Events;
using Deepwave.RetroOS.UI;
using Deepwave.RetroOS.UI.Components;
using Deepwave.RetroOS.Web;
using Deepwave.RetroOS.Web.Html;

namespace Deepwave.RetroOS.Apps
{
    // The 1995 browser: menu bar, round nav buttons, a Netsite field with a spinning
    // logo, directory buttons, a status bar with a padlock. Pages are plain .html/.css
    // from the Internet zone; non-markup gets its own viewer.
    public class WebBrowserView : AppView
    {
        // At most this many pages open at once, so the strip stays readable.
        const int MaxTabs = 6;

        // One open page. Each tab keeps its own history and its own loaded
        // document, so Back in one window never walks through another's trail.
        class Tab
        {
            public readonly BrowseHistory History = new();
            public WebPage Page;
            public string Title = "New Page";

            public string Url => History.Current;
        }

        readonly List<Tab> _tabs = new();

        // On the program, not this window, so bookmarks and saved pages survive a close.
        readonly List<WebBookmark> _ownMarks = new();
        readonly List<string> _ownFiles = new();
        readonly HashSet<string> _ownVisited = new(System.StringComparer.OrdinalIgnoreCase);
        List<WebBookmark> Bookmarks => Source?.Bookmarks ?? _ownMarks;
        List<string> Downloads => Source?.Downloads ?? _ownFiles;
        HashSet<string> Visited => Source?.Visited ?? _ownVisited;
        Tab _tab;
        RectTransform _tabStrip;

        RetroTextBox _address;
        Text _addressLabel;
        RetroPushButton _back, _forward, _stop;
        RectTransform _page;
        ScrollRect _scroll;
        RetroStatusStrip _status;
        Image _throbber;
        Image _padlock;
        WebPageLoader _loader;
        AudioSource _sound;

        // One player + target texture for the session; a per-page pair leaked a RenderTexture.
        VideoPlayer _video;
        RenderTexture _videoTexture;
        float _spinUntil;

        IWebSource Source => Config<IWebSource>();

        // On the tab, not the view, so switching tabs switches page, history and security together.
        WebPage Loaded
        {
            get => _tab?.Page;
            set { if (_tab != null) _tab.Page = value; }
        }

        // Empty = the browser's own start page. "home" is not a host, so it read as a broken package.
        string HomeUrl => Source != null ? Source.HomeUrl?.Trim() ?? "" : "";

        protected override void Build()
        {
            // What the last account saved is not this one's to see.
            Source?.KeepFor(Os.CurrentUser);

            _loader = new WebPageLoader(Os.Net);

            RetroUi.VLayout(Root.gameObject, 0, 0);
            BuildMenu();
            BuildToolbar();
            BuildLocationBar();
            BuildDirectoryBar();
            BuildTabStrip();

            _page = RetroUi.VScroll(Root, out _scroll);
            RetroUi.Flexible(RetroUi.ScrollFrame(_scroll));

            BuildStatusBar();

            _tab = new Tab();
            _tabs.Add(_tab);
            Navigate(HomeUrl);
        }

        // --- chrome -----------------------------------------------------------

        void BuildMenu()
        {
            var menu = RetroUi.Menu(Root);

            menu.AddMenu("File")
                .Command("New Page", () => NewTab(HomeUrl), "Ctrl+T", RetroIcons.Globe)
                .Command("New Web Browser", () => Os.Apps.Launch(App.Id), "Ctrl+N", RetroIcons.Globe)
                .Command("Open Location...", PromptForLocation, "Ctrl+L")
                .Separator()
                .Command("Save As...", SavePage, "Ctrl+S", RetroIcons.Get(BuiltinIcon.Floppy))
                .Command("Downloads...", ShowDownloads, null, RetroIcons.Get(BuiltinIcon.Download))
                .Command("Print...", PrintPage, "Ctrl+P", RetroIcons.Get(BuiltinIcon.Printer))
                .Separator()
                .Command("Close Page", () => CloseTab(_tab), "Ctrl+W")
                .Command("Close", () => Window.Close(), "Alt+F4");

            menu.AddMenu("Edit")
                .Command("Find...", ShowFind, "Ctrl+F", RetroIcons.Search);

            menu.AddMenu("View")
                .Command("Reload", Reload, "Ctrl+R", RetroIcons.Get(BuiltinIcon.Reload))
                .Command("Load Images", Reload, null, RetroIcons.Get(BuiltinIcon.Photos))
                .Separator()
                .Command("Document Source", ViewSource)
                .Command("Document Info", ShowInfo, null, RetroIcons.Info);

            menu.AddMenu("Go")
                .Command("Back", GoBack, "Alt+Left", RetroIcons.Back)
                .Command("Forward", GoForward, "Alt+Right", RetroIcons.Forward)
                .Command("Home", () => Navigate(HomeUrl), null, RetroIcons.Home)
                .Separator()
                .Command("History...", ShowHistoryMenu);

            menu.AddMenu("Bookmarks")
                .Command("Add Bookmark", AddBookmark, "Ctrl+D", RetroIcons.Bookmark)
                .Command("View Bookmarks", ShowBookmarks);

            menu.AddMenu("Options")
                .Check("Auto Load Images", true, () => Note("Images always load."))
                .Separator()
                .Command("Network Preferences", () => Os.Apps.Launch("settings"), null,
                    RetroIcons.Settings);

            var directory = menu.AddMenu("Directory");
            foreach (var (caption, url) in DirectoryLinks())
            {
                var target = url;
                directory.Command(caption, () => Navigate(target), null, RetroIcons.Globe);
            }

            menu.AddMenu("Help")
                .Command("About the Browser", () => RetroDialog.Info(Os.Overlay, "About",
                    "RetroOS Navigator\n\nRenders plain HTML and CSS from the Internet zone.\n" +
                    "No JavaScript, no flexbox - it is 1997."));
        }

        // The navigation buttons, grouped the way Navigator grouped them.
        void BuildToolbar()
        {
            var bar = RetroUi.ToolStrip(Root);
            RetroUi.Size(bar, h: 34f);

            _back = bar.Add(RetroIcons.Back, "Back one page", GoBack, "Back");
            _forward = bar.Add(RetroIcons.Forward, "Forward one page", GoForward, "Forward");
            bar.Add(RetroIcons.Home, "Go to the home page", () => Navigate(HomeUrl), "Home");
            bar.AddSeparator();

            bar.Add(RetroIcons.Get(BuiltinIcon.Reload), "Reload this page", Reload, "Reload");
            bar.Add(RetroIcons.Get(BuiltinIcon.Photos), "Load the images", Reload, "Images");
            bar.Add(RetroIcons.FolderOpen, "Open a location", PromptForLocation, "Open");
            bar.Add(RetroIcons.Get(BuiltinIcon.Printer), "Print", PrintPage, "Print");
            bar.Add(RetroIcons.Search, "Find in this page", ShowFind, "Find");
            bar.AddSeparator();

            _stop = bar.Add(RetroIcons.Stop, "Stop loading", StopLoading, "Stop");
            _stop.Interactable = false;
            bar.AddSpacer();
        }

        // "Netsite:" plus the address and the logo that turns while loading.
        void BuildLocationBar()
        {
            var bar = RetroUi.Panel(Root, PaletteSlot.Face, "Location");
            RetroUi.Size(bar, h: 30f);
            RetroUi.HLayout(bar.gameObject, 4, 4);

            _addressLabel = RetroUi.NoWrap(
                RetroUi.Label(bar.transform, "Netsite:", 13, TextAnchor.MiddleRight));
            _addressLabel.fontStyle = FontStyle.Bold;
            RetroUi.Size(_addressLabel, w: 54f);

            _address = RetroUi.TextBox(bar.transform);
            RetroUi.Flexible(_address);
            _address.Submitted += Navigate;

            // The drop-down of everywhere this window has been.
            var drop = RetroPushButton.Create(bar.transform, "", ShowHistoryMenu,
                RetroTextures.Arrow(1));
            RetroUi.Size(drop, w: 20f);

            // Navigator's logo, doubling as the OS's loading throbber.
            var logo = RetroUi.Panel(bar.transform, PaletteSlot.TitleActive, "Logo");
            RetroUi.Size(logo, w: 30f);
            RetroUi.Bevel(logo.rectTransform, true, 1f);
            _throbber = RetroUi.Icon(logo.transform, RetroIcons.Globe, 22f);
            RetroUi.Stretch(_throbber.rectTransform, 3f);
        }

        // What's New!, What's Cool!, Net Search ... the strip under the address.
        void BuildDirectoryBar()
        {
            var links = DirectoryLinks();
            if (links.Count == 0) return;

            var bar = RetroUi.Panel(Root, PaletteSlot.Face, "Directory");
            RetroUi.Size(bar, h: 26f);
            RetroUi.HLayout(bar.gameObject, 3, 3);

            foreach (var (caption, url) in links)
            {
                var target = url;
                var button = RetroPushButton.Create(bar.transform, caption, () => Navigate(target));
                if (button.Label) button.Label.fontSize = 12;
                RetroUi.SoftSize(button, RetroUi.TextWidth(caption, 12f) + 16f, 40f);
            }
            RetroUi.Spacer(bar.transform);
        }

        // Folder tabs - the same Win95 property-sheet widget - so a period browser can have tabs.
        void BuildTabStrip()
        {
            var bar = RetroUi.Panel(Root, PaletteSlot.Face, "Tabs");
            RetroUi.Size(bar, h: 24f);
            var layout = RetroUi.HLayout(bar.gameObject, 2, 2);
            layout.childForceExpandWidth = false;
            _tabStrip = bar.rectTransform;
        }

        void RefreshTabStrip()
        {
            if (!_tabStrip) return;
            foreach (Transform child in _tabStrip) Destroy(child.gameObject);

            foreach (var tab in _tabs)
            {
                var owner = tab;
                var active = tab == _tab;
                var caption = Shorten(tab.Title);

                var holder = RetroUi.NewRect("Tab", _tabStrip);

                // Six pages open in a narrow window crowd each other rather
                // than running off the side of it.
                RetroUi.SoftSize(holder,
                    RetroUi.TextWidth(caption, 12f) + (_tabs.Count > 1 ? 40f : 22f), 56f);
                RetroUi.HLayout(holder.gameObject, 0, 0);

                var button = RetroPushButton.Create(holder, caption, () => Select(owner));
                RetroUi.Flexible(button);
                if (button.Label)
                {
                    button.Label.fontSize = 12;
                    button.Label.fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
                }
                button.On = active;
                button.IsToggle = true;
                RetroUi.ToolTip(button, tab.Url ?? caption);

                // The last tab has no close button: closing it would leave the
                // browser with nothing to show.
                if (_tabs.Count <= 1) continue;
                var close = RetroPushButton.Create(holder, "x", () => CloseTab(owner));
                RetroUi.Size(close, w: 18f);
                if (close.Label) close.Label.fontSize = 10;
            }

            if (_tabs.Count < MaxTabs)
            {
                var add = RetroPushButton.Create(_tabStrip, "+", () => NewTab(HomeUrl));
                RetroUi.Size(add, w: 22f);
                RetroUi.ToolTip(add, "Open another page");
            }

            RetroUi.Spacer(_tabStrip);
        }

        static string Shorten(string title)
        {
            if (string.IsNullOrEmpty(title)) return "New Page";
            return title.Length <= 18 ? title : title[..17] + "...";
        }

        void NewTab(string url)
        {
            if (_tabs.Count >= MaxTabs) { Note($"Only {MaxTabs} pages can be open at once."); return; }

            _tab = new Tab();
            _tabs.Add(_tab);
            Navigate(url);
            Raise("newtab", url);
        }

        void CloseTab(Tab tab)
        {
            if (_tabs.Count <= 1) return;

            var index = _tabs.IndexOf(tab);
            _tabs.Remove(tab);
            if (_tab != tab) { RefreshTabStrip(); return; }

            // Closing the page you are looking at lands on its neighbour, the
            // way closing anything with a strip of siblings does.
            Select(_tabs[Mathf.Clamp(index, 0, _tabs.Count - 1)]);
        }

        void Select(Tab tab)
        {
            if (tab == null || tab == _tab) { RefreshTabStrip(); return; }
            _tab = tab;

            var url = tab.Url;
            if (string.IsNullOrEmpty(url)) Navigate(HomeUrl);
            else Show(url);
        }

        void BuildStatusBar()
        {
            _status = RetroUi.StatusStrip(Root, 22f, -1f, 90f);
            RetroUi.Size(_status, h: 22f);

            // The padlock: broken on a plain page, whole on a LAN share.
            _padlock = RetroUi.Icon(_status.transform.GetChild(0), RetroIcons.Get(BuiltinIcon.Key), 16f);
            _padlock.color = new Color(1f, 1f, 1f, 0.4f);
            RetroUi.Stretch(_padlock.rectTransform, 3f);

            _status.Set(1, "Ready");
        }

        // The directory buttons, from the app's own settings when it has them.
        List<(string caption, string url)> DirectoryLinks()
        {
            var configured = Source?.DirectoryLinks;
            if (configured is { Count: > 0 })
                return configured
                    .Where(l => !string.IsNullOrWhiteSpace(l.caption))
                    .Select(l => (l.caption, string.IsNullOrWhiteSpace(l.url) ? l.caption : l.url))
                    .ToList();

            // Nothing configured: offer whatever is actually reachable, which
            // beats six buttons that all fail to resolve.
            return Os.Net.KnownSites.Take(6).Select(s => (s, s)).ToList();
        }

        // --- navigation ---------------------------------------------------------

        string Current => _tab?.Url ?? HomeUrl;

        void GoBack()
        {
            if (_tab != null && _tab.History.Back()) Show(_tab.Url);
        }

        void GoForward()
        {
            if (_tab != null && _tab.History.Forward()) Show(_tab.Url);
        }

        void Reload() => Show(Current);

        void StopLoading()
        {
            // Stop means stop: the page that was on its way never arrives.
            if (_loading != null) { StopCoroutine(_loading); _loading = null; }

            _spinUntil = 0f;
            if (_throbber) _throbber.rectTransform.localEulerAngles = Vector3.zero;
            _stop.Interactable = false;
            _status.Set(1, "Stopped");
        }

        public void Navigate(string url)
        {
            url = (url ?? "").Trim();
            if (_tab == null) return;
            if (url.Length == 0) { ShowStartPage(); return; }

            // Go reports whether that was somewhere new; re-asking for the current page is a reload.
            _tab.History.Go(url);
            Show(url);
        }

        // A page does not arrive instantly; the wait is what makes the throbber, status line and Stop button mean anything.
        void Show(string url)
        {
            StopMedia();
            foreach (Transform child in _page) Destroy(child.gameObject);
            ResetPageChrome(url);

            if (_loading != null) StopCoroutine(_loading);

            var wait = Source?.LoadSeconds ?? 0f;
            if (wait <= 0f || !isActiveAndEnabled) { _loading = null; Fetch(url); return; }

            _loading = StartCoroutine(FetchAfter(url, wait));
        }

        IEnumerator FetchAfter(string url, float seconds)
        {
            _spinUntil = Time.unscaledTime + seconds;
            yield return new WaitForSecondsRealtime(seconds);
            _loading = null;
            Fetch(url);
        }

        Coroutine _loading;

        void Fetch(string url)
        {
            var page = _loader.Load(url, Loaded?.Host, WebPageLoader.RelativeDir(Loaded));

            if (page is { Ok: true })
            {
                Loaded = page;
                RenderPage(page);
                Finish(page.Title, page.Url, page.Status);
                return;
            }

            NotFound(url, page?.Status ?? "Host not found");
        }

        void ResetPageChrome(string url)
        {
            _address.Value = url;
            _back.Interactable = _tab != null && _tab.History.CanGoBack;
            _forward.Interactable = _tab != null && _tab.History.CanGoForward;
            _stop.Interactable = true;

            // The scroll view's own background is the page's; reset it before
            // the next page paints its own colour or wallpaper.
            var backdrop = RetroUi.ScrollBackdrop(_scroll);
            if (backdrop)
            {
                backdrop.sprite = null;
                backdrop.type = Image.Type.Simple;
                RetroPalette.Paint(backdrop, PaletteSlot.Field);
            }

            _status.Set(1, "Connecting to " + url + "...");
            _spinUntil = Time.unscaledTime + 0.6f;
        }

        void Finish(string title, string url, string status)
        {
            _scroll.verticalNormalizedPosition = 1f;
            _stop.Interactable = false;
            _spinUntil = 0f;
            if (_throbber) _throbber.rectTransform.localEulerAngles = Vector3.zero;

            Window.SetTitle($"{App.Title} - [{title}]");

            // Netscape said "Netsite:" for a server it recognised and
            // "Location:" for everything else.
            _addressLabel.text = url != null &&
                                 Os.Net.KnownSites.Any(s =>
                                     url.StartsWith(s, StringComparison.OrdinalIgnoreCase))
                ? "Netsite:" : "Location:";

            if (_tab != null)
            {
                _tab.History.Retitle(title);
                _tab.Title = title;
            }
            RefreshTabStrip();

            _status.Set(1, "Document: Done");
            _status.Set(2, status);
            SetSecure(Loaded is { Host: not null } && Loaded.Host.StartsWith("\\"));

            Visited.Add(url);
            Os.Events.Raise(OsEvents.WebVisited, url);
            Raise("visited", url);
        }

        void SetSecure(bool secure)
        {
            if (!_padlock) return;
            _padlock.sprite = RetroIcons.AtSize(
                secure ? RetroIcons.Lock : RetroIcons.Get(BuiltinIcon.Key), 16);
            _padlock.color = secure ? Color.white : new Color(1f, 1f, 1f, 0.4f);
        }

        void Update()
        {
            if (_spinUntil <= 0f || !_throbber) return;

            // The logo turns while the page is coming in.
            _throbber.rectTransform.localEulerAngles =
                new Vector3(0f, 0f, -Time.unscaledTime * 220f % 360f);

            if (Time.unscaledTime <= _spinUntil) return;
            _spinUntil = 0f;
            _throbber.rectTransform.localEulerAngles = Vector3.zero;
        }

        // --- rendering ------------------------------------------------------------

        void RenderPage(WebPage page)
        {
            if (page.IsMedia) { RenderMedia(page); return; }

            if (!page.IsHtml || page.Document == null)
            {
                var pre = RetroUi.Label(_page, page.PlainText ?? "(no content)", 13,
                    TextAnchor.UpperLeft, RetroSkin.Mono);
                pre.supportRichText = false;
                return;
            }

            var ctx = new HtmlRenderContext
            {
                Navigate = Navigate,
                Resolve = href => _loader.Resolve(page, href),
                LoadImage = src => _loader.LoadImage(src, page.Host, WebPageLoader.RelativeDir(page)),
                LoadFrames = src => _loader.LoadFrames(src, page.Host, WebPageLoader.RelativeDir(page)),
                Hover = href => _status.Set(1, string.IsNullOrEmpty(href) ? "Document: Done" : href),
                Visited = Visited.Contains,
                ContentWidth = _page.rect.width
            };

            HtmlRenderer.Render(_page, page.Document, page.Style, ctx);
        }

        // A picture, a sound or a video opened directly - what a browser does
        // with a media file, rather than showing its bytes.
        void RenderMedia(WebPage page)
        {
            var frame = RetroUi.Panel(_page, PaletteSlot.Field, "Media");
            RetroUi.Size(frame, h: 320f);
            RetroUi.VLayout(frame.gameObject, 6, 10);

            switch (page.Kind)
            {
                case FileKind.Image: ShowImage(frame.transform, page); break;
                case FileKind.Audio: ShowAudio(frame.transform, page); break;
                default: ShowVideo(frame.transform, page); break;
            }

            var caption = RetroUi.Label(frame.transform,
                $"{page.Title}   {page.Kind}   {SizeText(page.Size)}", 12, TextAnchor.MiddleCenter);
            RetroPalette.Paint(caption, PaletteSlot.TextDisabled);
            RetroUi.Size(caption, h: 18f);
        }

        void ShowImage(Transform parent, WebPage page)
        {
            var sprite = MediaLoader.LoadSprite(page.FilePath);
            if (!sprite)
            {
                Broken(parent, "The picture could not be decoded.");
                return;
            }

            var holder = RetroUi.NewRect("Picture", parent);
            RetroUi.Flexible(holder);
            var image = RetroUi.Picture(holder, sprite);
            RetroUi.Stretch(image.rectTransform, 4f);
            Raise("image", page.Url);
        }

        void ShowAudio(Transform parent, WebPage page)
        {
            var clip = MediaLoader.LoadAudio(page.FilePath);

            RetroUi.Size(RetroUi.Icon(parent, RetroIcons.Get(BuiltinIcon.Music), 64f), h: 72f);

            if (!clip)
            {
                Broken(parent, "This sound format needs a player app.");
                return;
            }

            if (!_sound)
            {
                _sound = gameObject.AddComponent<AudioSource>();
                _sound.playOnAwake = false;
            }
            _sound.clip = clip;

            var row = RetroUi.NewRect("Transport", parent);
            RetroUi.Size(row, h: 28f);
            RetroUi.HLayout(row.gameObject, 6, 0);
            RetroUi.Spacer(row);
            RetroUi.Size(RetroUi.PushButton(row, "Play", () =>
            {
                _sound.Play();
                Raise("audio", page.Url);
            }), w: 80f);
            RetroUi.Size(RetroUi.PushButton(row, "Stop", () => _sound.Stop()), w: 80f);
            RetroUi.Spacer(row);
        }

        void ShowVideo(Transform parent, WebPage page)
        {
            var holder = RetroUi.NewRect("Screen", parent);
            RetroUi.Flexible(holder);

            var raw = RetroUi.Raw(holder);
            RetroUi.Stretch(raw.rectTransform, 4f);

            if (!_videoTexture) _videoTexture = new RenderTexture(480, 360, 0);
            raw.texture = _videoTexture;

            // VideoClip cannot be built at runtime, so the player streams the
            // file straight off disk.
            if (!_video)
            {
                _video = gameObject.AddComponent<VideoPlayer>();
                _video.source = VideoSource.Url;
                _video.renderMode = VideoRenderMode.RenderTexture;
                _video.isLooping = true;
            }

            _video.targetTexture = _videoTexture;
            _video.url = MediaLoader.FileUrl(page.FilePath);
            _video.Play();
            Raise("video", page.Url);
        }

        // Silences the previous page's audio: the transport controls go with the page.
        void StopMedia()
        {
            if (_sound) _sound.Stop();
            if (_video) _video.Stop();
        }

        void OnDestroy()
        {
            StopMedia();
            if (_videoTexture) Destroy(_videoTexture);
        }

        static void Broken(Transform parent, string message)
        {
            var row = RetroUi.NewRect("Broken", parent);
            RetroUi.Size(row, h: 40f);
            RetroUi.HLayout(row.gameObject, 6, 4);
            RetroUi.Size(RetroUi.Icon(row, RetroIcons.BrokenImage, 32f), w: 36f);
            RetroUi.Label(row, message, 13, TextAnchor.MiddleLeft);
        }

        /// <summary>
        /// The page a browser with no home address opens on - built from what the
        /// network is serving at the moment it is drawn.
        /// </summary>
        void ShowStartPage()
        {
            StopMedia();
            foreach (Transform child in _page) Destroy(child.gameObject);

            Loaded = null;
            _stop.Interactable = false;
            _spinUntil = 0f;
            _address.Value = "";

            var heading = RetroUi.Label(_page, "Welcome", 18);
            heading.fontStyle = FontStyle.Bold;

            var reachable = Os.Net.KnownSites.ToList();
            if (reachable.Count == 0)
            {
                RetroUi.Label(_page,
                    "There is nothing on the line yet.\n\n" +
                    "A site is a folder under internet/ named by its address, or a " +
                    "RetroInternetHost component in the scene. Drop a folder with an " +
                    "index.html in it and type the folder's name above.", 14);
            }
            else
            {
                RetroUi.Label(_page, "This machine can reach:", 14);
                foreach (var site in reachable.Take(12))
                {
                    var target = site;
                    // A link is a panel; a sized panel has no height of its own, so match the markup renderer's 20.
                    RetroUi.Size(RetroUi.Link(_page, target, () => Navigate(target)), h: 20f);
                }
            }

            Window.SetTitle($"{App.Title} - [Welcome]");
            if (_tab != null) _tab.Title = "Welcome";
            RefreshTabStrip();

            _status.Set(1, "Done");
            _status.Set(2, "");
            SetSecure(false);
        }

        void NotFound(string url, string status)
        {
            Loaded = null;
            _stop.Interactable = false;
            _spinUntil = 0f;

            var head = RetroUi.Label(_page, "The page cannot be displayed", 18);
            head.fontStyle = FontStyle.Bold;
            RetroUi.Label(_page,
                $"Cannot find server at '{url}'.\n\nCheck the address and try again, or pick a " +
                "site from the Directory menu.", 14);

            Window.SetTitle($"{App.Title} - [Not Found]");
            if (_tab != null) _tab.Title = "Not Found";
            RefreshTabStrip();

            _status.Set(1, "Cannot open page");
            _status.Set(2, status);
            SetSecure(false);
            Raise("failed", url);
        }

        // --- commands ---------------------------------------------------------------

        void PromptForLocation() =>
            RetroDialog.Prompt(Os.Overlay, "Open Location", "Type an address:", Navigate);

        void ShowFind() =>
            RetroDialog.Prompt(Os.Overlay, "Find", "Find what:", term =>
            {
                if (string.IsNullOrWhiteSpace(term)) return;
                var found = _page.GetComponentsInChildren<Text>(true)
                    .Any(t => t.text != null &&
                              t.text.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
                _status.Set(1, found ? $"Found '{term}'" : $"'{term}' is not on this page");
                Raise("find", term);
            });

        void AddBookmark()
        {
            if (Loaded is not { Ok: true }) { Note("Nothing to bookmark."); return; }
            if (Bookmarks.Any(b => b.url == Loaded.Url)) { Note("Already bookmarked."); return; }

            Bookmarks.Add(new WebBookmark(Loaded.Url, Loaded.Title));
            Note($"Added '{Loaded.Title}'.");
            Raise("bookmark", Loaded.Url);
        }

        void ShowHistoryMenu()
        {
            var menu = new RetroMenu();
            var history = _tab?.History.Visits;

            if (history != null)
                foreach (var visit in Enumerable.Reverse(history).Take(10))
                {
                    var target = visit.Url;
                    menu.Command(visit.Title ?? target, () => Navigate(target), null, RetroIcons.Globe);
                }

            if (Bookmarks.Count > 0)
            {
                menu.Separator();
                foreach (var mark in Bookmarks)
                {
                    var target = mark.url;
                    menu.Command(mark.Caption, () => Navigate(target), null,
                        RetroIcons.Bookmark);
                }
            }

            if ((history == null || history.Count == 0) && Bookmarks.Count == 0)
                menu.Command("(nowhere yet)", null, null, null, false);

            RetroMenuPopup.Show(menu.Items, _address.Rect, RetroMenuPopup.Placement.Below, null);
        }

        // The bookmark file, as its own window - the era's Bookmarks window
        // was a real window, not a menu.
        void ShowBookmarks()
        {
            var window = Os.Windows.Open("Bookmarks", new Vector2(420, 320), RetroIcons.Bookmark);
            RetroUi.VLayout(window.Content.gameObject, 4, 4);

            var list = RetroUi.ListView(window.Content, ListViewMode.Details);
            RetroUi.Flexible(list);
            list.SetColumns(new[] { "Name", "Address" }, new[] { 160f, -1f });

            void Fill()
            {
                list.Clear();
                foreach (var mark in Bookmarks)
                    list.Add(new RetroListEntry
                    {
                        Text = mark.Caption,
                        Icon = RetroIcons.Bookmark,
                        Columns = new[] { mark.url },
                        Tag = mark.url
                    });

                if (Bookmarks.Count == 0)
                    list.Add(new RetroListEntry { Text = "(no bookmarks yet)", Columns = new[] { "" } });
            }

            list.Activated += entry =>
            {
                if (entry?.Tag is string url) Navigate(url);
            };

            var row = RetroUi.NewRect("Row", window.Content);
            RetroUi.Size(row, h: 26f);
            RetroUi.HLayout(row.gameObject, 6, 0);
            RetroUi.Size(RetroUi.PushButton(row, "Go", () =>
            {
                if (list.Selection?.Tag is string url) Navigate(url);
            }), w: 70f);
            RetroUi.Size(RetroUi.PushButton(row, "Open in New Page", () =>
            {
                if (list.Selection?.Tag is string url) NewTab(url);
            }), w: 140f);
            RetroUi.Spacer(row);
            RetroUi.Size(RetroUi.PushButton(row, "Remove", () =>
            {
                if (list.Selection?.Tag is not string url) return;
                Bookmarks.RemoveAll(b => b.url == url);
                Fill();
            }), w: 80f);

            Fill();
        }

        // What the browser has pulled off the network and written to disk.
        void ShowDownloads()
        {
            var window = Os.Windows.Open("Downloads", new Vector2(460, 260),
                RetroIcons.Get(BuiltinIcon.Download));
            RetroUi.VLayout(window.Content.gameObject, 4, 4);

            var list = RetroUi.ListView(window.Content, ListViewMode.Details);
            RetroUi.Flexible(list);
            list.SetColumns(new[] { "File", "Saved to" }, new[] { 160f, -1f });

            foreach (var path in Downloads)
                list.Add(new RetroListEntry
                {
                    Text = System.IO.Path.GetFileName(path),
                    Icon = RetroIcons.Get(BuiltinIcon.File),
                    Columns = new[] { path },
                    Tag = path
                });

            if (Downloads.Count == 0)
                list.Add(new RetroListEntry
                {
                    Text = "(nothing downloaded yet)",
                    Columns = new[] { "" }
                });

            list.Activated += entry =>
            {
                if (entry?.Tag is string path && Os.Fs.Resolve(path) is VfsFile file)
                    Os.OpenFile(file);
            };
        }

        void ViewSource()
        {
            if (Loaded is not { Ok: true }) { Note("No page loaded."); return; }

            var window = Os.Windows.Open("Source of " + Loaded.Url, new Vector2(560, 420),
                RetroIcons.Note);
            var content = RetroUi.VScroll(window.Content, out _);

            var source = Loaded.IsHtml
                ? DiskMount.SafeReadText(Loaded.FilePath) ?? ""
                : Loaded.PlainText ?? $"({Loaded.Kind} file, {SizeText(Loaded.Size)})";

            RetroUi.Label(content, source, 12, TextAnchor.UpperLeft, RetroSkin.Mono)
                .supportRichText = false;
            Raise("source", Loaded.Url);
        }

        void ShowInfo()
        {
            if (Loaded is not { Ok: true }) { Note("No page loaded."); return; }
            RetroDialog.Info(Os.Overlay, "Document Info",
                $"Location:  {Loaded.Url}\n" +
                $"Server:    {Loaded.Host}\n" +
                $"Type:      {Loaded.Kind}\n" +
                $"Size:      {SizeText(Loaded.Size)}\n" +
                "Security:  none (plain connection)");
        }

        // The printer files the page as a document in the player's Printouts folder.
        void PrintPage()
        {
            if (Loaded is not { Ok: true }) { Note("Nothing to print."); return; }

            var page = Loaded;
            var body = page.IsHtml
                ? page.Document?.InnerText ?? page.PlainText
                : page.PlainText;

            if (string.IsNullOrWhiteSpace(body))
            {
                Note($"'{page.Url}' is a {page.Kind} file and cannot be printed.");
                return;
            }

            var job = Core.Storage.RetroPrinter.Print(Os,
                string.IsNullOrWhiteSpace(page.Title) ? page.Url : page.Title, body);

            Note(job.Message);
            if (job.Ok) Raise("print", job.Path);
            else RetroDialog.Alert(Os.Overlay, "Print", job.Message);
        }

        // Downloading writes the page to disk, where any program can then open it.
        void SavePage()
        {
            if (Loaded is not { Ok: true }) { Note("Nothing to save."); return; }

            var page = Loaded;
            var suggested = System.IO.Path.GetFileName(page.FilePath);
            if (string.IsNullOrWhiteSpace(suggested)) suggested = "page.html";

            RetroFileDialog.Save(Os, "Save Page As", suggested, (folder, name) =>
            {
                if (folder == null || string.IsNullOrWhiteSpace(folder.SourcePath))
                {
                    RetroDialog.Alert(Os.Overlay, "Save As",
                        "Files cannot be written here.\n\nChoose a folder on a drive.");
                    return;
                }

                var body = page.IsHtml
                    ? DiskMount.SafeReadText(page.FilePath) ?? ""
                    : page.PlainText;

                if (body == null)
                {
                    RetroDialog.Alert(Os.Overlay, "Save As",
                        $"'{page.Url}' is a {page.Kind} file and cannot be saved as text.");
                    return;
                }

                var target = System.IO.Path.Combine(folder.SourcePath, name);
                if (!DiskMount.SafeWriteText(target, body))
                {
                    RetroDialog.Alert(Os.Overlay, "Save As", "The file could not be written.");
                    return;
                }

                Os.Drives.Refresh();
                var path = folder.Path + "/" + name;
                Downloads.Add(path);

                Note("Saved to " + path);
                Os.Events.Raise(OsEvents.WebDownload, page.Url);
                Os.NotifyFsChanged(path, FsChange.Created);
                Raise("download", path);
            }, DownloadFolder());
        }

        // Somewhere sensible to land: the signed-in user's documents when the
        // machine has them, otherwise wherever the player last was.
        VfsDirectory DownloadFolder()
        {
            return Os.Drives.DocumentsOf(Os.CurrentUser);
        }

        void Note(string message) => _status.Set(1, message);

        static string SizeText(long bytes) => bytes <= 0 ? ""
            : bytes < 1024 ? bytes + " bytes"
            : bytes < 1024 * 1024 ? (bytes / 1024f).ToString("0.#") + " KB"
            : (bytes / 1048576f).ToString("0.#") + " MB";

    }
}
