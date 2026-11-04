using System;
using UnityEngine;

namespace Deepwave.RetroOS.Data
{
    // Where the whole game's files live (one folder holding machines/ and internet/).
    // A world fact, not per-machine: set it only on the RetroWorld component.
    [Serializable]
    public class StorageLocation
    {
        [Tooltip("Folder under StreamingAssets holding machines/ and internet/. " +
                 "Also the name used in the player's Documents folder.")]
        public string folderName = "RetroOS";

        [Tooltip("Optional. Absolute path to use instead of the default location. " +
                 "Handy for a shared content drive during production.")]
        public string overrideRootPath = "";

        [Tooltip("Optional. Subfolder of the player's Documents folder " +
                 "(default: <ProductName>/<folderName>).")]
        public string documentsSubfolder = "";

        public bool SameAs(StorageLocation other) =>
            other != null &&
            Same(folderName, other.folderName) &&
            Same(overrideRootPath, other.overrideRootPath) &&
            Same(documentsSubfolder, other.documentsSubfolder);

        // Blank and whitespace are equivalent to RetroStorage, so compare them as equal.
        static bool Same(string a, string b) =>
            string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

        public string Describe() =>
            string.IsNullOrWhiteSpace(overrideRootPath)
                ? $"'{(string.IsNullOrWhiteSpace(folderName) ? "RetroOS" : folderName.Trim())}'"
                : $"'{overrideRootPath.Trim()}'";
    }

    // Whether the player sees a logon dialog before the desktop.
    public enum LoginMode
    {
        // No users = straight to the desktop; otherwise ask.
        Automatic = 0,
        // Never show the logon screen, even with users configured.
        SkipAlways = 1,
        // Always show it (nobody can sign in with no users; the inspector warns).
        AlwaysAsk = 2
    }

    [Serializable]
    public class LoginSettings
    {
        [Tooltip("Automatic: skip the logon screen when no users are configured. " +
                 "Skip Always: boot straight to the desktop. Always Ask: show it.")]
        public LoginMode mode = LoginMode.Automatic;

        [Tooltip("Username logged in automatically when the screen is skipped. " +
                 "Empty = the first configured user, or a guest session.")]
        public string autoLoginUser = "";

        [Tooltip("Message under the logon prompt.")]
        public string banner = "Type a user name and password to log on to RetroOS.";
    }
}
