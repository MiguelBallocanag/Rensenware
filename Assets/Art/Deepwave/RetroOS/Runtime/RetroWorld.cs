using UnityEngine;
using Deepwave.RetroOS.Core.Storage;
using Deepwave.RetroOS.Data;

namespace Deepwave.RetroOS
{
    /// <summary>The world the machines live in: where every machine's files are kept, what year the fiction thinks it is, and whether the computers in it can see each other.</summary>
    [AddComponentMenu("Deepwave/RetroOS/World")]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public class RetroWorld : MonoBehaviour
    {
        [Tooltip("Where the machines/ and internet/ folders live. This is the " +
                 "whole game's storage, shared by every machine in the scene.")]
        [SerializeField] StorageLocation storage = new();

        [Tooltip("In-fiction date and time the game starts at, e.g. " +
                 "'1997-08-12 22:00'. Every machine reads the same one - they " +
                 "are in the same room. Empty = the player's real clock.")]
        [SerializeField] string systemDate = "";

        [Tooltip("Show the other machines on the LAN as a browsable branch of " +
                 "This Computer.")]
        [SerializeField] bool showNetworkNeighborhood = true;

        [Tooltip("What that branch is called. Empty = 'Network Neighborhood'.")]
        [SerializeField] string neighborhoodLabel = "Network Neighborhood";

        static RetroWorld s_current;
        static StorageLocation s_prepared;
        static string s_preparedBy;

        // Statics outlive a play session when domain reload is off; clear so the second run does not believe the first's folder is ready.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_current = null;
            s_prepared = null;
            s_preparedBy = null;
        }

        /// <summary>The world in the scene, or <c>null</c> when there is none.</summary>
        public static RetroWorld Current => s_current;

        /// <summary>Where the world's files are, once somebody has said.</summary>
        public static StorageLocation Location => s_prepared;

        /// <summary>The machine's folder layout this world hands out.</summary>
        public StorageLocation Storage => storage ??= new StorageLocation();

        /// <summary>The in-fiction clock every machine in this world starts from. Empty means the player's own clock.</summary>
        public string SystemDate => systemDate;

        /// <summary>Whether machines here list each other in This Computer.</summary>
        public bool ShowNetworkNeighborhood => showNetworkNeighborhood;

        /// <summary>What that branch is called.</summary>
        public string NeighborhoodLabel => string.IsNullOrWhiteSpace(neighborhoodLabel)
            ? "Network Neighborhood"
            : neighborhoodLabel.Trim();

        /// <summary>The world every machine reads from, made on the spot if the scene has none. A game with a single computer never has to think about this; a game with two gets one place to answer for both.</summary>
        public static RetroWorld Require()
        {
            if (s_current) return s_current;

            // Inactive included: an authored world switched off is still the one; a silent second would move the data folder.
            var existing = FindFirstObjectByType<RetroWorld>(FindObjectsInactive.Include);
            if (existing)
            {
                existing.Claim();
                return existing;
            }

            var go = new GameObject("Retro OS World");
            return go.AddComponent<RetroWorld>();
        }

        void Awake() => Claim();

        void OnEnable() => Claim();

        void OnDisable()
        {
            if (s_current == this) s_current = null;
        }

        void Claim()
        {
            if (s_current && s_current != this)
            {
                Debug.LogWarning(
                    $"RetroOS: '{name}' is a second RetroWorld; '{s_current.name}' already " +
                    "says where this game's files are. Two worlds cannot both be right, so " +
                    "this one is ignored. Delete it, or move the machines into its scene.");
                return;
            }

            s_current = this;
            Prepare(Storage, name);
        }

        /// <summary>Points <see cref="RetroStorage"/> at a folder, once. The first caller wins and every later one is measured against it, so a machine can go on asking without moving the world.</summary>
        /// <param name="where">The folder layout being asked for.</param>
        /// <param name="owner">Who is asking, for the warning.</param>
        public static void Prepare(StorageLocation where, string owner)
        {
            where ??= new StorageLocation();

            if (s_prepared != null)
            {
                if (!s_prepared.SameAs(where))
                    Debug.LogWarning(
                        $"RetroOS: '{owner}' wants its files at {where.Describe()}, but " +
                        $"'{s_preparedBy}' already put this game's files at " +
                        $"{s_prepared.Describe()}. There is one data folder for the whole " +
                        "scene, so the first one stands. Put a RetroWorld in the scene and " +
                        "set the location there.");
                return;
            }

            s_prepared = where;
            s_preparedBy = owner;

            RetroStorage.FolderName = string.IsNullOrWhiteSpace(where.folderName)
                ? "RetroOS"
                : where.folderName.Trim();
            RetroStorage.OverrideRoot = where.overrideRootPath;
            RetroStorage.DocumentsFolder = where.documentsSubfolder;
            RetroStorage.EnsureReady();
        }
    }
}
