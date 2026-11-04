using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Deepwave.RetroOS.Core.Storage;

namespace Deepwave.RetroOS.Core.Network
{
    // One site on "the internet": a domain plus the folder of html/css/images
    // behind it. Add the component when the site must behave like a real one
    // (go down mid-story, answer a second domain). A bare folder under
    // <data>/internet/ is published anyway.
    [AddComponentMenu("Deepwave/RetroOS/Internet Host")]
    [DisallowMultipleComponent]
    public class RetroInternetHost : MonoBehaviour
    {
        [Tooltip("Address the player types, e.g. northpoint.local or 203.0.113.7.")]
        [SerializeField] string domain = "example.net";

        [Tooltip("Folder under <data>/internet/. Empty = the domain name.")]
        [SerializeField] string folderName = "";

        [Tooltip("Other addresses that reach this same site (an IP, a www. prefix).")]
        [SerializeField] List<string> aliases = new();

        [Tooltip("The server is up. Off = 'Host not found', the way a dead site reads.")]
        [SerializeField] bool online = true;

        [Tooltip("Document served when the player asks for the site with no page.")]
        [SerializeField] string indexPage = "index.html";

        public string Domain => string.IsNullOrWhiteSpace(domain) ? name : domain.Trim().ToLowerInvariant();

        public string Folder => RetroStorage.SiteFolder(
            string.IsNullOrWhiteSpace(folderName) ? Domain : folderName);

        public string IndexPage => string.IsNullOrWhiteSpace(indexPage) ? "index.html" : indexPage.Trim();

        public IReadOnlyList<string> Aliases => aliases;

        public bool Online
        {
            get => online && isActiveAndEnabled;
            set
            {
                if (online == value) return;
                online = value;
                RetroNetwork.NotifyChanged();
            }
        }

        public bool Exists => Directory.Exists(Folder);

        /// <summary>Whether the site shows in listings (still reachable if not). Read from the folder's sidecar (<c>hidden: true</c>), not a field here.</summary>
        public bool Listed => !RetroMeta.Read(Folder).Hidden;

        void OnEnable()
        {
            RetroNetwork.Register(this);
            RetroNetwork.NotifyChanged();
        }

        void OnDisable()
        {
            RetroNetwork.Unregister(this);
            RetroNetwork.NotifyChanged();
        }

        public bool Answers(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return false;
            host = host.Trim().ToLowerInvariant();
            return host == Domain ||
                   aliases.Any(a => !string.IsNullOrWhiteSpace(a) &&
                                    a.Trim().ToLowerInvariant() == host);
        }

        void Reset() => domain = name.ToLowerInvariant();
    }
}
