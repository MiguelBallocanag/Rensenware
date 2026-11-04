using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Deepwave.RetroOS.Core.Storage;

namespace Deepwave.RetroOS.Core.Network
{
    /// <summary>
    /// A folder one machine publishes to the LAN as <c>\\address\name</c>.
    /// Declared in the folder's own <c>.rmeta</c> sidecar, not a component.
    /// </summary>
    public class MachineShare
    {
        /// <summary>The machine publishing this folder.</summary>
        public RetroMachine Machine { get; }

        /// <summary>The real folder on disk being published.</summary>
        public string Folder { get; }

        /// <summary>Name the folder is published under, e.g. ARCHIVE.</summary>
        public string ShareName { get; }

        /// <summary>Shown beside the share in Network Neighborhood.</summary>
        public string Comment { get; }

        /// <summary>Browsing the share asks for this first. Empty = open to all.</summary>
        public string Password { get; }

        /// <summary>Whether the share is listed on the network at all.</summary>
        public bool Visible { get; }

        public bool RequiresPassword => !string.IsNullOrEmpty(Password);

        /// <summary>Whether another machine can reach it now: owner powered and connected, share listed, folder present.</summary>
        public bool Available =>
            Machine && Machine.Online && Visible && Directory.Exists(Folder);

        MachineShare(RetroMachine machine, string folder, RetroMeta meta)
        {
            Machine = machine;
            Folder = folder;
            ShareName = meta.Share.Trim();
            Comment = meta.ShareComment ?? "";
            Password = meta.SharePassword ?? "";
            Visible = !meta.ShareHidden;
        }

        /// <summary>
        /// Every share one machine publishes, found by walking its folder (max
        /// <see cref="MaxDepth"/> deep) for sidecars. Cached by <see cref="RetroMachine"/>.
        /// </summary>
        public static List<MachineShare> Discover(RetroMachine machine)
        {
            var shares = new List<MachineShare>();
            if (!machine) return shares;

            var root = machine.Folder;
            if (!Directory.Exists(root))
            {
                // Not an error: no folder yet means nothing to share.
                return shares;
            }

            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Walk(machine, root, 0, shares, taken);
            return shares;
        }

        /// <summary>How many folders below a machine's own folder the share scan
        /// looks. A folder marked shared deeper than this is written but never found.</summary>
        public const int MaxDepth = 3;

        static void Walk(RetroMachine machine, string folder, int depth,
            List<MachineShare> shares, HashSet<string> taken)
        {
            IEnumerable<string> children;
            try
            {
                children = Directory.EnumerateDirectories(folder);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"RetroOS: could not look for shares in '{folder}', so " +
                                 $"anything published below it is missing from the network. " +
                                 $"{e.Message}", machine);
                return;
            }

            foreach (var child in children)
            {
                var meta = RetroMeta.Read(child);
                if (meta.IsShared)
                {
                    var share = new MachineShare(machine, child.Replace('\\', '/'), meta);

                    // A duplicate share name would be ambiguous at the same UNC path.
                    if (!taken.Add(share.ShareName))
                        Debug.LogWarning(
                            $"RetroOS: '{machine.MachineName}' publishes more than one folder " +
                            $"as '{share.ShareName}'. Only the first is on the network; rename " +
                            $"the share in '{child}.rmeta'.", machine);
                    else
                        shares.Add(share);
                }

                if (depth + 1 < MaxDepth)
                    Walk(machine, child, depth + 1, shares, taken);
            }
        }
    }
}
