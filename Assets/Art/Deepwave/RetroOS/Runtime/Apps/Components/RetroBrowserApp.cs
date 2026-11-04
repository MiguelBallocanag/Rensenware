using System.Collections.Generic;
using UnityEngine;
using Deepwave.RetroOS.Data;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Apps.Components
{
    [AddComponentMenu("")]
    public class RetroBrowserApp : RetroAppBehaviour, IWebSource
    {
        [Header("Web")]
        [Tooltip("Address opened on launch and by the Home button. Empty = the " +
                 "browser's own start page, which lists the sites this machine " +
                 "can actually reach.")]
        [SerializeField] string homeUrl = "";

        [Tooltip("How long a page takes to come in. 0 is instant; a fraction of " +
                 "a second is a modem, and is what makes the Stop button mean " +
                 "anything.")]
        [Range(0f, 5f)]
        [SerializeField] float loadSeconds = 0.4f;

        [Header("Bookmarks")]
        [Tooltip("What is already in the Bookmarks menu when the game starts. " +
                 "The player's own are added to this list and stay for as long " +
                 "as the machine is running.")]
        [SerializeField] List<WebBookmark> bookmarks = new();

        [Header("Directory Buttons")]
        [Tooltip("The strip under the address bar. Empty = whatever sites are " +
                 "actually reachable.")]
        [SerializeField] List<DirectoryLink> directoryLinks = new();

        public string HomeUrl => homeUrl;
        public float LoadSeconds => loadSeconds;
        public List<DirectoryLink> DirectoryLinks => directoryLinks;
        public List<WebBookmark> Bookmarks => bookmarks;

        // What the player saved onto the disk. Not serialized: it is a record
        // of this run, and the files themselves are the real record.
        readonly List<string> _downloads = new();
        public List<string> Downloads => _downloads;

        readonly HashSet<string> _visited = new(System.StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Visited => _visited;

        string _owner;

        public void KeepFor(string username)
        {
            if (username == _owner) return;
            _owner = username;
            _downloads.Clear();
            _visited.Clear();
        }

        public override void BuildContent(AppContext ctx) => AppView.Attach<WebBrowserView>(ctx);

        public override void ApplyDefaults()
        {
            app.appId = "web";
            app.title = "The Internet";
            app.builtinIcon = BuiltinIcon.Globe;
            app.windowSize = new Vector2(720, 540);
            app.minWindowSize = new Vector2(520, 400);
        }
    }
}
