using System;
using Deepwave.RetroOS.Core.FileSystem;
using Deepwave.RetroOS.Core.Network;
using Deepwave.RetroOS.Events;

namespace Deepwave.RetroOS.Core.Storage
{
    /// <summary>The non-drive branches of a machine's file system: My Documents and Network Neighborhood.</summary>
    public static class MachineMounts
    {
        /// <summary>
        /// Adds "My Documents" at the top of My Computer, pointing at the
        /// signed-in user's Documents folder (not a second mount) and rebuilt
        /// on login/logout.
        /// </summary>
        /// <param name="os">Reaches the running OS; a function because the mounts are set up while it is still being built.</param>
        public static void UserFolders(DriveBay drives, OsEventBus events,
            Func<RetroOsContext> os, string label = "My Documents")
        {
            if (drives == null || events == null || os == null) return;

            VfsDirectory Build()
            {
                var user = os()?.CurrentUser;
                if (string.IsNullOrEmpty(user)) return null;

                var documents = drives.DocumentsOf(user, out var drive);
                if (documents == null) return null;

                var branch = new VfsDirectory(label)
                {
                    Role = NodeRole.MyDocuments,
                    Owner = user,
                    Note = $"{user}'s documents on {drive.NodeName}"
                };
                branch.PointAt(documents);
                return branch;
            }

            drives.AddBranch(label, Build);

            void Rebuild(string _ = null)
            {
                drives.Rebuild(label);
                os()?.NotifyFsChanged("/" + label, FsChange.Mounted);
            }

            events.Subscribe(OsEvents.Login, Rebuild);
            events.Subscribe(OsEvents.Logout, Rebuild);
        }

        /// <summary>Adds the LAN branch, rebuilt whenever a machine or share changes.</summary>
        /// <returns>The handler to unsubscribe on teardown, or <c>null</c> if there is no branch.</returns>
        public static Action Neighborhood(DriveBay drives, NetworkService network,
            OsEventBus events, Func<RetroOsContext> os, string label = "Network Neighborhood")
        {
            if (drives == null || network == null || os == null) return null;

            // Rebuilt from scene and disk on demand, so a share's new files show up.
            drives.AddBranch(label, () =>
            {
                var branch = network.MountNeighborhood(label);
                return branch.Children.Count > 0 ? branch : null;
            });

            void RebuildNow()
            {
                drives.Rebuild(label);
                os()?.NotifyFsChanged("/" + label, FsChange.Mounted);
            }

            // Fired by another machine's network change, so the skin in hand is
            // that machine's. Claim this machine's for the rebuild, then hand it back.
            void Rebuild()
            {
                var skin = os()?.Skin;
                if (skin == null) RebuildNow();
                else skin.While(RebuildNow);
            }

            network.Changed += Rebuild;
            return Rebuild;
        }
    }
}
