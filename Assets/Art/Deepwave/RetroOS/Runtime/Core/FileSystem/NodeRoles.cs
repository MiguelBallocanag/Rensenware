using System;
using System.Collections.Generic;

namespace Deepwave.RetroOS.Core.FileSystem
{
    /// <summary>
    /// Classifies a folder from its name, its parent's role, and the machine's
    /// account list. The rule that decides file privacy.
    /// </summary>
    public static class NodeRoles
    {
        /// <param name="accounts">Usernames that can sign in here; a folder named after one is that person's home, wherever it sits.</param>
        public static NodeRole Of(NodeRole parent, string name, IReadOnlyList<string> accounts)
        {
            if (parent == NodeRole.UserRoot) return NodeRole.UserHome;

            if (parent == NodeRole.UserHome)
                return Lower(name) switch
                {
                    "documents" or "my documents" => NodeRole.Documents,
                    "pictures" or "my pictures" => NodeRole.Pictures,
                    "desktop" => NodeRole.Desktop,
                    _ => NodeRole.Normal
                };

            if (IsAccount(name, accounts)) return NodeRole.UserHome;

            // A share is the root of the shared folder (usually a drive), not a plain folder.
            if (parent is not (NodeRole.Drive or NodeRole.Share)) return NodeRole.Normal;

            return Lower(name) switch
            {
                "users" or "home" or "people" => NodeRole.UserRoot,
                "windows" or "system" or "dos" => NodeRole.System,
                "program files" or "programs" => NodeRole.Programs,
                "documents" or "my documents" => NodeRole.Documents,
                "pictures" => NodeRole.Pictures,
                _ => NodeRole.Normal
            };
        }

        /// <summary>Who a node belongs to: a home owns itself; everything under one inherits it.</summary>
        public static string OwnerOf(NodeRole role, string name, string parentOwner) =>
            role == NodeRole.UserHome ? name : parentOwner;

        static bool IsAccount(string name, IReadOnlyList<string> accounts)
        {
            if (accounts == null) return false;

            for (var i = 0; i < accounts.Count; i++)
                if (string.Equals(accounts[i], name, StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        static string Lower(string name) => name == null ? "" : name.ToLowerInvariant();
    }
}
