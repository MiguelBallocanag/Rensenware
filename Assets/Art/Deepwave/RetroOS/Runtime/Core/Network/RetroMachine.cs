using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Deepwave.RetroOS.Core.Storage;
using Deepwave.RetroOS.Data;

namespace Deepwave.RetroOS.Core.Network
{
    // How a machine gets its address on the LAN.
    public enum AddressMode
    {
        Static,     // typed, fixed
        Automatic   // network assigns one at boot (for runtime-spawned machines)
    }

    // One computer in the game world: a folder of drives on disk, a LAN address,
    // and whatever it shares. Put it on the object that *is* the computer.
    // Power it off and its files leave the network, like unplugging a server.
    [AddComponentMenu("Deepwave/RetroOS/Machine")]
    [DisallowMultipleComponent]
    public class RetroMachine : MonoBehaviour
    {
        [Tooltip("Name shown in Network Neighborhood, e.g. RECORDS-01.")]
        [SerializeField] string machineName = "MACHINE";

        [Tooltip("Folder under <data>/machines/ holding this machine's drives. " +
                 "Empty = the machine name, lower case.")]
        [SerializeField] string folderName = "";

        [SerializeField] AddressMode addressMode = AddressMode.Static;

        [Tooltip("Address on the LAN. With Automatic the network assigns one at boot.")]
        [SerializeField] string address = "192.168.1.10";

        [Tooltip("The machine is switched on. Off = invisible on the network.")]
        [SerializeField] bool poweredOn = true;

        [Tooltip("The network cable is in. Off = powered but unreachable.")]
        [SerializeField] bool connected = true;

        [Tooltip("Workgroup shown beside the machine in Network Neighborhood.")]
        [SerializeField] string workgroup = "WORKGROUP";

        [Tooltip("Who can sign in at this machine. Empty = no logon screen " +
                 "and no privacy, which is right for a single-user computer.")]
        [SerializeField] List<UserEntry> users = new();

        [SerializeField] LoginSettings login = new();

        // Stays with the machine even if the disk is wiped and a new OS installed.
        [Tooltip("What this machine prints on its way up, before the OS loads.")]
        [SerializeField] BootSettings boot = new();

        string _assigned;
        List<MachineShare> _shares;

        // Raised when reachability or shares change, so a browsing OS can refresh.
        public event Action Changed;

        public string MachineName => string.IsNullOrWhiteSpace(machineName) ? name : machineName.Trim();

        public string Workgroup => workgroup;

        /// <summary>Who can sign in at this machine.</summary>
        public List<UserEntry> Users => users ??= new List<UserEntry>();

        /// <summary>Whether this machine shows a logon screen, and to whom.</summary>
        public LoginSettings Login => login ??= new LoginSettings();

        /// <summary>What the firmware prints before the OS loads.</summary>
        public BootSettings Boot => boot ??= new BootSettings();

        // The folder on disk holding this machine's drives.
        public string FolderName => string.IsNullOrWhiteSpace(folderName)
            ? RetroStorage.Sanitize(MachineName)
            : RetroStorage.Sanitize(folderName);

        public string Folder => RetroStorage.MachineFolder(FolderName);

        public string Address => addressMode == AddressMode.Automatic
            ? _assigned ?? address
            : address?.Trim() ?? "";

        public bool PoweredOn
        {
            get => poweredOn;
            set { if (poweredOn == value) return; poweredOn = value; Notify(); }
        }

        public bool Connected
        {
            get => connected;
            set { if (connected == value) return; connected = value; Notify(); }
        }

        // Reachable from another machine on the LAN.
        public bool Online => poweredOn && connected;

        // Shares are folders (a `share: NAME` sidecar line), not components.
        // Cached because Network Neighborhood asks on every draw; Notify() drops it.
        public IEnumerable<MachineShare> Shares => _shares ??= MachineShare.Discover(this);

        public IEnumerable<MachineShare> VisibleShares => Shares.Where(s => s.Visible);

        // e.g. "\\192.168.1.20"
        public string DisplayName => $"\\\\{Address}";

        void OnEnable()
        {
            // Addressed before it registers: registering first would briefly put
            // every automatic machine on the inspector's default address and clash.
            if (addressMode == AddressMode.Automatic && string.IsNullOrEmpty(_assigned))
                _assigned = RetroNetwork.AssignAddress(this);

            RetroNetwork.Register(this);
        }

        void OnDisable() => RetroNetwork.Unregister(this);

        /// <summary>Re-reads this machine's shares and tells the network. Call after editing a sidecar by hand.</summary>
        public void Notify()
        {
            _shares = null;
            Changed?.Invoke();
            RetroNetwork.NotifyChanged();
        }

        // True when this machine answers to the given name or address.
        public bool Answers(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return false;
            host = host.Trim().TrimStart('\\', '/');
            return string.Equals(host, Address, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(host, MachineName, StringComparison.OrdinalIgnoreCase);
        }

        public MachineShare FindShare(string shareName) =>
            VisibleShares.FirstOrDefault(s =>
                string.Equals(s.ShareName, shareName?.Trim(), StringComparison.OrdinalIgnoreCase));

        // Editor: seed the machine name from the object name.
        void Reset() => machineName = name.ToUpperInvariant();
    }
}
