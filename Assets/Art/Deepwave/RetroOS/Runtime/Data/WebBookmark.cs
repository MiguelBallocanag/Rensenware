using System;
using UnityEngine;

namespace Deepwave.RetroOS.Data
{
    /// <summary>A saved page, or one the game seeds the browser with.</summary>
    [Serializable]
    public class WebBookmark
    {
        [Tooltip("What the bookmarks menu shows. Empty falls back to the address.")]
        public string title = "";

        [Tooltip("Where it goes, e.g. northpoint.local/index.html")]
        public string url = "";

        public WebBookmark() { }

        public WebBookmark(string url, string title)
        {
            this.url = url ?? "";
            this.title = title ?? "";
        }

        /// <summary>What to show in a list. Never empty if the address is not.</summary>
        public string Caption => string.IsNullOrWhiteSpace(title) ? url : title;
    }
}
