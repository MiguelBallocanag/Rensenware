using System;
using System.Collections.Generic;
using System.Linq;

namespace Deepwave.RetroOS.Core.Network
{
    /// <summary>
    /// Resolves an address in a page to a host and a file under it. Pure string
    /// work - the browser's whole notion of "where does this link go".
    /// </summary>
    public static class WebAddress
    {
        /// <summary>Documents tried, in order, when an address names a folder.</summary>
        public static readonly string[] IndexNames =
            { "index.html", "index.htm", "default.html", "home.html" };

        /// <summary>
        /// Splits an address into host and path, resolving relative, absolute
        /// and <c>../</c> links against the page they were found on. Fragments
        /// and query strings are dropped.
        /// </summary>
        public static (string host, string path) Split(string url, string currentHost,
            string currentDir)
        {
            url = (url ?? "").Trim();
            if (url.Length == 0) return (currentHost, "");

            url = url.Replace('\\', '/');
            foreach (var scheme in new[] { "http://", "https://", "ftp://", "//" })
                if (url.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
                {
                    url = url[scheme.Length..];
                    break;
                }

            var cut = url.IndexOfAny(new[] { '#', '?' });
            if (cut >= 0) url = url[..cut];

            var absolute = url.StartsWith("/");
            if (absolute) url = url.TrimStart('/');

            var slash = url.IndexOf('/');
            var first = slash < 0 ? url : url[..slash];
            var rest = slash < 0 ? "" : url[(slash + 1)..];

            // Relative links stay on this host. "." / ".." and any dot-prefixed
            // or dot-suffixed segment are path steps, never host names.
            var couldBeName = first.Length > 0 &&
                              !first.StartsWith(".") && !first.EndsWith(".");

            var looksLikeHost = couldBeName &&
                                ((first.Contains('.') && !HasFileExtension(first)) ||
                                 IsIpAddress(first) ||
                                 first.Equals("localhost", StringComparison.OrdinalIgnoreCase));

            if (!looksLikeHost && !string.IsNullOrEmpty(currentHost))
            {
                var relative = url;
                if (!absolute && !string.IsNullOrEmpty(currentDir))
                    relative = Normalize(currentDir.TrimEnd('/') + "/" + url);
                return (currentHost, relative.TrimStart('/'));
            }

            return (first, rest);
        }

        /// <summary>Collapses "a/b/../c" into "a/c".</summary>
        public static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";

            var stack = new List<string>();
            foreach (var part in path.Split('/'))
            {
                if (part is "" or ".") continue;
                if (part == "..")
                {
                    if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
                    continue;
                }
                stack.Add(part);
            }
            return string.Join("/", stack);
        }

        /// <summary>Whether an address's first segment is a filename, not a host ("style.css" vs "swapmeet.com").</summary>
        public static bool HasFileExtension(string segment)
        {
            var dot = segment.LastIndexOf('.');
            if (dot < 0 || dot == segment.Length - 1) return false;
            var ext = segment[(dot + 1)..].ToLowerInvariant();
            return ext is "html" or "htm" or "txt" or "png" or "jpg" or "jpeg" or "gif" or "css"
                or "wav" or "mp3" or "ogg" or "mp4" or "webm" or "bmp";
        }

        public static bool IsIpAddress(string value)
        {
            var parts = value.Split('.');
            return parts.Length == 4 && parts.All(p => byte.TryParse(p, out _));
        }
    }
}
