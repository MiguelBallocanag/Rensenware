using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using Deepwave.RetroOS.Core.Network;
using Deepwave.RetroOS.Core.Storage;

namespace Deepwave.RetroOS.Web.Html
{
    // A page fetched from the internet/LAN folders, parsed and ready to draw.
    public class WebPage
    {
        public bool Ok;
        public string Url;
        public string Host;
        public string Directory;
        public string Title;
        public string Status;
        public HtmlNode Document;
        public CssStylesheet Style = new();

        // What the address turned out to be: a browser renders a picture, a sound and a page differently.
        public Core.FileSystem.FileKind Kind = Core.FileSystem.FileKind.Web;

        // The real file, for the media viewers.
        public string FilePath;
        public long Size;

        // Plain-text pages (a README on a LAN share) come through as text.
        public string PlainText;

        public bool IsHtml => Kind == Core.FileSystem.FileKind.Web;
        public bool IsPlainText => Kind == Core.FileSystem.FileKind.Text;
        public bool IsMedia => Kind is Core.FileSystem.FileKind.Image
            or Core.FileSystem.FileKind.Audio or Core.FileSystem.FileKind.Video;
    }

    // Fetches a document, pulls in its <link rel=stylesheet>/<style> blocks and resolves relative urls.
    public class WebPageLoader
    {
        readonly NetworkService _net;
        readonly Dictionary<string, Sprite> _images = new();

        public WebPageLoader(NetworkService net) => _net = net;

        public WebPage Load(string url, string currentHost = null, string currentDir = null)
        {
            var response = _net.Request(url, currentHost, currentDir);
            var page = new WebPage
            {
                Ok = response.Ok,
                Url = response.Url,
                Host = response.Host,
                Directory = response.Directory,
                Status = response.Status
            };
            if (!response.Ok) return page;

            page.Kind = response.Kind;
            page.FilePath = response.FilePath;
            page.Title = Path.GetFileName(response.FilePath);

            try
            {
                var info = new FileInfo(response.FilePath);
                if (info.Exists) page.Size = info.Length;
            }
            catch (Exception e)
            {
                // The size is only for the status bar, but a file that cannot be measured is usually one about to fail to open.
                Debug.LogWarning($"RetroOS: could not measure '{response.FilePath}'. {e.Message}");
            }

            // Only text and markup are safe to read as a string. Anything else
            // is handed to the browser as a file for its media viewers.
            if (page.IsMedia || page.Kind is Core.FileSystem.FileKind.Binary
                or Core.FileSystem.FileKind.Executable)
                return page;

            var text = DiskMount.SafeReadText(response.FilePath) ?? "";

            if (!page.IsHtml)
            {
                page.PlainText = text;
                return page;
            }

            page.Document = HtmlParser.Parse(text);
            var title = page.Document.Find("title")?.InnerText;
            if (!string.IsNullOrWhiteSpace(title)) page.Title = title;

            LoadStyles(page);
            return page;
        }

        void LoadStyles(WebPage page)
        {
            foreach (var link in page.Document.FindAll("link"))
            {
                var rel = link.Attr("rel");
                if (rel != null && rel.IndexOf("stylesheet", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                var href = link.Attr("href");
                if (string.IsNullOrWhiteSpace(href)) continue;

                var response = _net.Request(href, page.Host, RelativeDir(page));
                if (!response.Ok) continue;
                page.Style.Parse(DiskMount.SafeReadText(response.FilePath));
            }

            foreach (var block in page.Document.FindAll("style"))
                page.Style.Parse(block.InnerText);
        }

        // The page's own folder, relative to its host, for resolving links.
        public static string RelativeDir(WebPage page)
        {
            var url = page?.Url ?? "";
            var slash = url.LastIndexOf('/');
            if (slash < 0) return "";
            var path = url[..slash];
            var hostCut = path.IndexOf('/');
            return hostCut < 0 ? "" : path[(hostCut + 1)..];
        }

        // Absolute address for an href/src on this page.
        public string Resolve(WebPage page, string reference)
        {
            if (string.IsNullOrWhiteSpace(reference)) return "";
            var (host, path) = NetworkService.Split(reference, page?.Host, RelativeDir(page));
            return string.IsNullOrEmpty(path) ? host : host + "/" + path;
        }

        public Sprite LoadImage(string url, string currentHost, string currentDir)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            if (_images.TryGetValue(url, out var cached) && cached) return cached;

            var response = _net.Request(url, currentHost, currentDir);
            if (!response.Ok) return null;

            var sprite = MediaLoader.LoadSprite(response.FilePath);
            if (sprite) _images[url] = sprite;
            return sprite;
        }

        // Animated pictures without a GIF decoder: name frames "banner.1.png", "banner.2.png"... and reference the first; the rest are picked up automatically.
        public List<Sprite> LoadFrames(string url, string currentHost, string currentDir)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            var match = Regex.Match(url, @"^(?<stem>.+)\.(?<n>\d+)\.(?<ext>[A-Za-z0-9]+)$");
            if (!match.Success) return null;

            var frames = new List<Sprite>();
            var stem = match.Groups["stem"].Value;
            var ext = match.Groups["ext"].Value;

            for (var i = 1; i <= 64; i++)
            {
                var frame = LoadImage($"{stem}.{i}.{ext}", currentHost, currentDir);
                if (!frame) break;
                frames.Add(frame);
            }
            return frames.Count > 1 ? frames : null;
        }

        public void ClearCache() => _images.Clear();
    }
}
