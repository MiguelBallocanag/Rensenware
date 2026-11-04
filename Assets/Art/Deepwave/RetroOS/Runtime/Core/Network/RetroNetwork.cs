using System;
using System.Collections.Generic;
using System.Linq;

namespace Deepwave.RetroOS.Core.Network
{
    // The world's network: every machine, the internet, and every web host in
    // the scene. Components register on wake and drop on disable, so the list is
    // always the truth about the current scene. The only place the three kinds meet.
    public static class RetroNetwork
    {
        static readonly List<RetroMachine> s_machines = new();
        static readonly List<RetroInternetHost> s_hosts = new();
        static RetroInternet s_internet;

        // Statics outlive a play session when domain reload is off; clear them
        // as the scene comes up so the network means the scene in front of you.
        [UnityEngine.RuntimeInitializeOnLoadMethod(
            UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_machines.Clear();
            s_hosts.Clear();
            s_internet = null;
            Changed = null;
        }

        // Raised when the network's shape changes; a browsing OS refreshes.
        public static event Action Changed;

        public static IReadOnlyList<RetroMachine> Machines => s_machines;
        public static IReadOnlyList<RetroInternetHost> Hosts => s_hosts;

        /// <summary>The scene's internet, or <c>null</c> - then the internet is always up.</summary>
        public static RetroInternet Internet => s_internet;

        /// <summary>Whether anything outside the LAN answers at all.</summary>
        public static bool InternetUp => !s_internet || s_internet.Online;

        // --- registration ---

        public static void Register(RetroMachine machine)
        {
            if (!machine || s_machines.Contains(machine)) return;

            Sweep();
            s_machines.Add(machine);
            WarnOnClash(machine);
            NotifyChanged();
        }

        // Drops entries whose objects were destroyed, so the list cannot grow unbounded.
        static void Sweep()
        {
            s_machines.RemoveAll(m => !m);
            s_hosts.RemoveAll(h => !h);
        }

        // Two machines on one address usually means a copied, un-re-addressed scene.
        static void WarnOnClash(RetroMachine machine)
        {
            var address = machine.Address;
            if (string.IsNullOrEmpty(address)) return;

            foreach (var other in s_machines)
            {
                if (!other || other == machine) continue;
                if (!string.Equals(other.Address, address, StringComparison.OrdinalIgnoreCase))
                    continue;

                UnityEngine.Debug.LogWarning(
                    $"RetroOS: '{machine.MachineName}' and '{other.MachineName}' are both at " +
                    $"{address}. Whichever registered first answers for the address, so the " +
                    "other one is only reachable by name. Give one of them a different " +
                    "address, or set it to Automatic.");
                return;
            }
        }

        public static void Unregister(RetroMachine machine)
        {
            if (s_machines.Remove(machine)) NotifyChanged();
        }

        public static void Register(RetroInternetHost host)
        {
            if (!host || s_hosts.Contains(host)) return;

            Sweep();
            s_hosts.Add(host);
        }

        public static void Unregister(RetroInternetHost host) => s_hosts.Remove(host);

        public static void Register(RetroInternet internet)
        {
            if (!internet || s_internet == internet) return;

            if (s_internet)
            {
                UnityEngine.Debug.LogWarning(
                    $"RetroOS: '{internet.name}' is a second RetroInternet; there is already " +
                    $"one on '{s_internet.name}'. A world has one outside world, so the first " +
                    "one stands and this one is ignored.");
                return;
            }

            s_internet = internet;
        }

        public static void Unregister(RetroInternet internet)
        {
            if (s_internet == internet) s_internet = null;
        }

        public static void NotifyChanged() => Changed?.Invoke();

        // --- the LAN ---

        /// <summary>Every machine reachable right now, except the one asking.</summary>
        public static IEnumerable<RetroMachine> Neighbours(RetroMachine self) =>
            s_machines.Where(m => m && m != self && m.Online)
                .OrderBy(m => m.Address, StringComparer.OrdinalIgnoreCase);

        // Machines a browser lists: online and serving at least one visible share.
        public static IEnumerable<RetroMachine> Servers(RetroMachine self) =>
            Neighbours(self).Where(m => m.VisibleShares.Any());

        public static RetroMachine FindMachine(string hostOrAddress) =>
            s_machines.FirstOrDefault(m => m && m.Online && m.Answers(hostOrAddress));

        // Next free address on the existing static machines' subnet - this world's DHCP.
        public static string AssignAddress(RetroMachine machine)
        {
            var taken = s_machines
                .Where(m => m && m != machine)
                .Select(m => m.Address)
                .Where(a => !string.IsNullOrEmpty(a))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var subnet = taken.Select(Subnet).FirstOrDefault(s => s != null) ?? "192.168.1";

            for (var host = 100; host < 255; host++)
            {
                var candidate = $"{subnet}.{host}";
                if (!taken.Contains(candidate)) return candidate;
            }
            return $"{subnet}.254";
        }

        static string Subnet(string address)
        {
            var parts = (address ?? "").Split('.');
            return parts.Length == 4 ? string.Join(".", parts.Take(3)) : null;
        }

        // --- the internet ---

        // Who answers a domain, only while the line to the outside world is up.
        public static RetroInternetHost FindHost(string domain) =>
            InternetUp ? Host(domain) : null;

        /// <summary>The component for a domain, reachable or not - so a rule can revive a site while the line is down.</summary>
        public static RetroInternetHost Host(string domain) =>
            s_hosts.FirstOrDefault(h => h && h.Answers(domain));

        // Sites a machine is told about: live listed hosts plus published folders.
        // A hidden site is reachable by address but in no list.
        public static IEnumerable<string> KnownDomains()
        {
            if (!InternetUp) return Enumerable.Empty<string>();

            var live = s_hosts.Where(h => h && h.Online && h.Listed).Select(h => h.Domain);
            var folders = Storage.RetroStorage.ListedSiteNames();
            return live.Concat(folders).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(d => d, StringComparer.OrdinalIgnoreCase);
        }

        // Test/editor hook: empty the network. The OS uses NetworkService.
        public static void Clear()
        {
            s_machines.Clear();
            s_hosts.Clear();
            s_internet = null;
            NotifyChanged();
        }
    }
}
