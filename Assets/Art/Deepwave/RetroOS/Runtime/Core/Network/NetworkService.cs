using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Deepwave.RetroOS.Core.FileSystem;
using Deepwave.RetroOS.Core.Storage;
using Deepwave.RetroOS.Events;

namespace Deepwave.RetroOS.Core.Network
{
    // What a browser request resolved to.
    public class NetResponse
    {
        public bool Ok;
        public string Url;            // normalised, e.g. "news.gov/index.html"
        public string Host;
        public string RelativePath;   // "news/index.html"
        public string FilePath;       // real path on disk
        public FileKind Kind;
        public string Status = "";    // "200 OK", "404 Not Found", "Host not found"

        public string Directory => string.IsNullOrEmpty(FilePath) ? "" :
            Path.GetDirectoryName(FilePath)?.Replace('\\', '/') ?? "";
    }

    // One machine's view of the network - the network card, not the network.
    // It sees whatever RetroNetwork exposes, minus this machine. The LAN is the
    // other powered, connected, sharing RetroMachine objects; the internet is the
    // scene's RetroInternet plus any folder under <data>/internet/.
    public class NetworkService
    {
        static readonly string[] FallbackIndex = WebAddress.IndexNames;

        readonly Action<string, string> _raise;

        // The machine doing the browsing, so it never lists itself.
        public RetroMachine Self { get; internal set; }

        // This machine's card. False = unplugged modem: this machine's requests fail, others carry on.
        public bool Online { get; set; } = true;

        /// <summary>The scene's internet, or <c>null</c> - then it is always up.</summary>
        public RetroInternet Internet => RetroNetwork.Internet;

        /// <summary>Whether this machine can reach past the LAN: its card is in and the line is up.</summary>
        public bool CanReachInternet => Online && RetroNetwork.InternetUp;

        // Raised when the network changes shape, so Explorer can refresh.
        public event Action Changed;

        public NetworkService(Action<string, string> raise = null, RetroMachine self = null)
        {
            _raise = raise;
            Self = self;
            RetroNetwork.Changed += OnNetworkChanged;
        }

        public void Dispose() => RetroNetwork.Changed -= OnNetworkChanged;

        void OnNetworkChanged() => Changed?.Invoke();

        // --- the LAN ---

        public IEnumerable<RetroMachine> Neighbours =>
            Online ? RetroNetwork.Servers(Self) : Enumerable.Empty<RetroMachine>();

        // Reachable machines as a browsable branch: a folder per machine, a folder per share.
        public VfsDirectory MountNeighborhood(string label = "Network Neighborhood")
        {
            var root = new VfsDirectory(label) { Role = NodeRole.NetworkRoot };
            if (!Online) return root;

            foreach (var machine in RetroNetwork.Servers(Self))
            {
                // Two machines on one address would merge into one folder and
                // lose the second's shares, so disambiguate with the machine name.
                var nodeName = machine.DisplayName;
                if (root.Find(nodeName) != null) nodeName += $" ({machine.MachineName})";

                var node = root.Add(new VfsDirectory(nodeName))
                    .WithNote($"{machine.MachineName} · {machine.Workgroup}");
                node.Role = NodeRole.NetworkHost;

                // The far machine's accounts, so browsing its share honours the
                // same private folders its own OS does (a published C: with no
                // owner would leak every account's documents).
                var accounts = machine.Users
                    .Where(u => u != null && !string.IsNullOrWhiteSpace(u.username))
                    .Select(u => u.username.Trim())
                    .ToList();

                foreach (var share in machine.VisibleShares)
                {
                    if (!share.Available) continue;

                    var mounted = DiskMount.Mount(share.Folder, share.ShareName, NodeRole.Share,
                        null, accounts);
                    mounted.Note = share.Comment;
                    mounted.Encrypted = share.RequiresPassword;
                    mounted.Password = share.Password;
                    node.Add(mounted);
                }
            }
            return root;
        }

        /// <summary>Resolves a UNC path to a real folder, if the caller supplies the share's password.</summary>
        /// <param name="password">Checked only by shares that ask for one.</param>
        /// <returns>The folder on disk, or <c>null</c>.</returns>
        public string ResolveUnc(string uncPath, string password = null)
        {
            if (string.IsNullOrWhiteSpace(uncPath)) return null;

            var parts = uncPath.Replace('\\', '/').Trim('/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return null;

            var machine = RetroNetwork.FindMachine(parts[0]);
            var share = parts.Length > 1 ? machine?.FindShare(parts[1]) : null;
            if (share is not { Available: true }) return null;

            if (share.RequiresPassword &&
                !string.Equals(share.Password, password, StringComparison.Ordinal))
                return null;

            var rest = string.Join("/", parts.Skip(2));
            var full = string.IsNullOrEmpty(rest)
                ? share.Folder
                : Path.Combine(share.Folder, rest).Replace('\\', '/');

            return RetroStorage.IsInside(share.Folder, full) ? full : null;
        }

        // --- the internet ---

        public IEnumerable<string> KnownSites =>
            Online ? RetroNetwork.KnownDomains() : Enumerable.Empty<string>();

        /// <summary>
        /// Cuts or restores the line to the outside world for every machine.
        /// No-op without a <see cref="RetroInternet"/>. Not <see cref="Online"/>,
        /// which is this machine's own modem.
        /// </summary>
        public void SetInternetOnline(bool up)
        {
            if (RetroNetwork.Internet) RetroNetwork.Internet.Online = up;
        }

        // Takes a site off the air or back, from a rule/code. Works even while the line is down.
        public void SetOnline(string domain, bool online)
        {
            var host = RetroNetwork.Host(domain);
            if (host) host.Online = online;
        }

        // Resolves what the player typed into a real file on disk.
        public NetResponse Request(string url, string currentHost = null, string currentDir = null)
        {
            var response = new NetResponse { Url = url };

            if (!Online)
            {
                response.Status = "No carrier";
                _raise?.Invoke(OsEvents.WebFailed, url);
                return response;
            }

            // The outside line is a world fact: down means every site gone, LAN untouched.
            if (!RetroNetwork.InternetUp)
            {
                response.Status = $"No route to {RetroNetwork.Internet.Carrier}";
                _raise?.Invoke(OsEvents.WebFailed, url);
                return response;
            }

            var (host, path) = Resolve(url, currentHost, currentDir);
            if (string.IsNullOrEmpty(host))
            {
                response.Status = "400 Bad Address";
                _raise?.Invoke(OsEvents.WebFailed, url);
                return response;
            }

            response.Host = host;

            var served = RetroNetwork.FindHost(host);
            string folder;
            var index = FallbackIndex;

            if (served != null)
            {
                if (!served.Online)
                {
                    response.Status = "Host not found";
                    _raise?.Invoke(OsEvents.HostUnreachable, host);
                    _raise?.Invoke(OsEvents.WebFailed, url);
                    return response;
                }
                folder = served.Folder;
                index = new[] { served.IndexPage }.Concat(FallbackIndex).ToArray();
                response.Host = served.Domain;

                // A host component with no folder is a wiring mistake, not a dead
                // site: still a 404 to the player, but warn so the author sees it.
                if (!Directory.Exists(folder))
                    Debug.LogWarning($"RetroOS: '{served.Domain}' is served by a " +
                                     $"RetroInternetHost in the scene, but its folder is not " +
                                     $"there: {folder}. Put the site's files in it, or point " +
                                     "the host at the folder that has them.", served);
            }
            else
            {
                // No component: a folder under internet/ is published as-is.
                folder = RetroStorage.SiteFolder(host);
                if (!Directory.Exists(folder))
                {
                    response.Status = "Host not found";
                    _raise?.Invoke(OsEvents.HostUnreachable, host);
                    _raise?.Invoke(OsEvents.WebFailed, url);
                    return response;
                }
            }

            return Serve(response, folder, path, index, url);
        }

        /// <summary>
        /// Which host an address is on and which file under it. Falls back to
        /// <see cref="WebAddress.Split"/>, but a bare dotless name that actually
        /// answers is treated as that site.
        /// </summary>
        (string host, string path) Resolve(string url, string currentHost, string currentDir)
        {
            var typed = (url ?? "").Trim().Trim('/');
            if (typed.Length > 0 && typed.IndexOf('/') < 0 && Answers(typed))
                return (typed, "");

            return Split(url, currentHost, currentDir);
        }

        // Whether anything serves this name: a host component or a folder under internet/.
        static bool Answers(string host) =>
            RetroNetwork.Host(host) != null || Directory.Exists(RetroStorage.SiteFolder(host));

        NetResponse Serve(NetResponse response, string folder, string path, string[] index, string url)
        {
            var full = string.IsNullOrEmpty(path)
                ? folder
                : Path.Combine(folder, path.Replace('/', Path.DirectorySeparatorChar));

            // Never let a link walk out of the site's folder.
            if (!RetroStorage.IsInside(folder, full))
            {
                response.Status = "403 Forbidden";
                _raise?.Invoke(OsEvents.WebFailed, url);
                return response;
            }

            if (Directory.Exists(full))
            {
                var document = index.Select(n => Path.Combine(full, n)).FirstOrDefault(File.Exists);
                if (document == null)
                {
                    response.Status = "403 No index";
                    _raise?.Invoke(OsEvents.WebFailed, url);
                    return response;
                }
                full = document;
            }

            if (!File.Exists(full))
            {
                response.Status = "404 Not Found";
                _raise?.Invoke(OsEvents.WebFailed, url);
                return response;
            }

            response.Ok = true;
            response.FilePath = full.Replace('\\', '/');
            response.RelativePath = Relative(folder, response.FilePath);
            response.Url = response.Host + (string.IsNullOrEmpty(response.RelativePath)
                ? "" : "/" + response.RelativePath);
            response.Kind = DiskMount.KindOf(Path.GetExtension(full).TrimStart('.').ToLowerInvariant());
            response.Status = "200 OK";
            _raise?.Invoke(OsEvents.HostReached, response.Host);
            return response;
        }

        static string Relative(string root, string full)
        {
            var r = Path.GetFullPath(root).TrimEnd('/', '\\');
            var f = Path.GetFullPath(full);
            return f.Length > r.Length ? f[(r.Length + 1)..].Replace('\\', '/') : "";
        }

        // Delegates to WebAddress.Split; kept here because half the browser calls it here.
        public static (string host, string path) Split(string url, string currentHost,
            string currentDir) => WebAddress.Split(url, currentHost, currentDir);

        public static string Normalize(string path) => WebAddress.Normalize(path);
    }
}
