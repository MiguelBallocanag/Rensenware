using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Deepwave.RetroOS.Core.Network
{
    // The internet as one thing in the scene: a switch for the whole outside
    // world, with RetroInternetHost components parked under it. Optional - a
    // scene without one has an internet that is always up.
    [AddComponentMenu("Deepwave/RetroOS/Internet")]
    [DisallowMultipleComponent]
    public class RetroInternet : MonoBehaviour
    {
        [Tooltip("The line to the outside world is up. Off = every site is " +
                 "unreachable at once, while the LAN carries on.")]
        [SerializeField] bool online = true;

        [Tooltip("Name for the connection, shown by the browser when it fails. " +
                 "Empty = 'the internet'.")]
        [SerializeField] string carrier = "";

        public string Carrier => string.IsNullOrWhiteSpace(carrier) ? "the internet" : carrier.Trim();

        /// <summary>Whether anything outside the building answers. Set it to cut the line.</summary>
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

        /// <summary>The sites on this connection. Hosts anywhere in the scene count; nesting is only for tidiness.</summary>
        public IEnumerable<RetroInternetHost> Hosts =>
            RetroNetwork.Hosts.Where(h => h);

        /// <summary>Every address that answers right now.</summary>
        public IEnumerable<string> Domains => RetroNetwork.KnownDomains();

        /// <summary>Takes one site off the air, or brings it back.</summary>
        public void SetOnline(string domain, bool up)
        {
            var host = RetroNetwork.Host(domain);
            if (host) host.Online = up;
        }

        /// <summary>Cuts the line to the outside world, or restores it.</summary>
        public void SetOnline(bool up) => Online = up;

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
    }
}
