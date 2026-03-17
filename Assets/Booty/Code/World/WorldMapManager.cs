// ---------------------------------------------------------------------------
// WorldMapManager.cs — tracks discovered ports and world exploration state
// ---------------------------------------------------------------------------
// Holds all PortData definitions for the world. Fires OnPortDiscovered when
// the player enters a port's discovery radius for the first time, implementing
// fog-of-war: undiscovered ports are invisible on NavigationUI until visited.
//
// Falls back to 8 built-in Caribbean ports if no assets are assigned in the
// Inspector, so the system works in dev bootstraps with no .asset files.
// ---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Booty.World
{
    /// <summary>
    /// Manages the world map: all port definitions, discovery state, and
    /// fog-of-war. Other systems (NavigationUI, minimap) query this manager
    /// for port locations and discovery status.
    /// </summary>
    public class WorldMapManager : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Inspector
        // ══════════════════════════════════════════════════════════════════

        [Header("Port Assets")]
        [Tooltip("All PortData ScriptableObject assets for this world. " +
                 "Leave empty to use the 8 built-in Caribbean ports.")]
        public List<PortData> portDataAssets = new List<PortData>();

        [Header("Discovery")]
        [Tooltip("World-unit radius within which a port is auto-discovered " +
                 "when the player ship enters it.")]
        [SerializeField] private float discoveryRadius = 80f;

        [Tooltip("Seconds between discovery checks (lower = more responsive).")]
        [SerializeField] private float discoveryCheckInterval = 1.0f;

        // ══════════════════════════════════════════════════════════════════
        //  Events
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Fired when a previously-undiscovered port enters the player's
        /// discovery radius for the first time. Arg: the PortData just found.
        /// </summary>
        public event Action<PortData> OnPortDiscovered;

        // ══════════════════════════════════════════════════════════════════
        //  Singleton (lightweight)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Active WorldMapManager. Set in Initialize().</summary>
        public static WorldMapManager Instance { get; private set; }

        // ══════════════════════════════════════════════════════════════════
        //  Runtime State
        // ══════════════════════════════════════════════════════════════════

        private readonly HashSet<string> _discoveredPortIds = new HashSet<string>();
        private Transform _playerTransform;
        private float _checkTimer;

        // ══════════════════════════════════════════════════════════════════
        //  Public API
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Initialize the WorldMapManager with the player's transform for
        /// proximity-based discovery checks. Falls back to built-in Caribbean
        /// ports when <see cref="portDataAssets"/> is empty.
        /// </summary>
        public void Initialize(Transform playerTransform)
        {
            Instance         = this;
            _playerTransform = playerTransform;

            if (portDataAssets == null || portDataAssets.Count == 0)
                InitDefaultPorts();

            Debug.Log($"[WorldMapManager] Initialized with {portDataAssets.Count} ports.");
        }

        /// <summary>
        /// Returns all known ports (discovered and undiscovered).
        /// </summary>
        public IReadOnlyList<PortData> GetAllPorts() => portDataAssets;

        /// <summary>
        /// Returns only the ports the player has already discovered.
        /// </summary>
        public List<PortData> GetDiscoveredPorts()
        {
            var result = new List<PortData>();
            foreach (var pd in portDataAssets)
                if (_discoveredPortIds.Contains(pd.portId))
                    result.Add(pd);
            return result;
        }

        /// <summary>
        /// Returns discovered ports within <paramref name="radius"/> world
        /// units of <paramref name="pos"/>, sorted nearest-first.
        /// </summary>
        public List<PortData> GetNearbyPorts(Vector3 pos, float radius)
        {
            var result = new List<PortData>();
            float r2 = radius * radius;

            foreach (var pd in portDataAssets)
            {
                if (!_discoveredPortIds.Contains(pd.portId)) continue;

                Vector3 delta = pd.worldPosition - pos;
                delta.y = 0f;
                if (delta.sqrMagnitude <= r2)
                    result.Add(pd);
            }

            result.Sort((a, b) =>
            {
                float da = ((a.worldPosition - pos)).sqrMagnitude;
                float db = ((b.worldPosition - pos)).sqrMagnitude;
                return da.CompareTo(db);
            });

            return result;
        }

        /// <summary>True if the port with the given ID has been discovered.</summary>
        public bool IsDiscovered(string portId) => _discoveredPortIds.Contains(portId);

        /// <summary>
        /// Force-mark a port as discovered (e.g. from save data on game load
        /// or via the debug console). Fires <see cref="OnPortDiscovered"/> if
        /// the port was previously unknown.
        /// </summary>
        public void ForceDiscover(string portId)
        {
            if (!_discoveredPortIds.Add(portId)) return; // already known

            var pd = portDataAssets?.Find(p => p != null && p.portId == portId);
            if (pd != null) OnPortDiscovered?.Invoke(pd);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Update()
        {
            if (_playerTransform == null) return;

            _checkTimer -= Time.deltaTime;
            if (_checkTimer > 0f) return;

            _checkTimer = discoveryCheckInterval;
            CheckDiscovery();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Private — Discovery
        // ══════════════════════════════════════════════════════════════════

        private void CheckDiscovery()
        {
            if (portDataAssets == null) return;

            Vector3 playerPos = _playerTransform.position;
            float r2 = discoveryRadius * discoveryRadius;

            foreach (var pd in portDataAssets)
            {
                if (pd == null) continue;
                if (_discoveredPortIds.Contains(pd.portId)) continue;

                Vector3 delta = pd.worldPosition - playerPos;
                delta.y = 0f;

                if (delta.sqrMagnitude <= r2)
                {
                    _discoveredPortIds.Add(pd.portId);
                    OnPortDiscovered?.Invoke(pd);
                    Debug.Log($"[WorldMapManager] Port discovered: {pd.portName}");
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Private — Default Port Data (8 Caribbean ports)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates the 8 canonical Caribbean ports as runtime PortData instances
        /// when no ScriptableObject assets are assigned in the Inspector.
        /// Positions are on the XZ plane (Y = 0) at a scale that matches the
        /// game's default 300-unit world.
        /// </summary>
        private void InitDefaultPorts()
        {
            portDataAssets = new List<PortData>
            {
                MakePort("nassau",     "Nassau",
                    new Vector3(-80f,  0f,  120f), "england",
                    PortService.Trade | PortService.Crew | PortService.Quest,
                    "The jewel of the Bahamas — a free port where pirates and merchants mingle freely."),

                MakePort("havana",     "Havana",
                    new Vector3(-120f, 0f,   40f), "spain",
                    PortService.Trade | PortService.Upgrade | PortService.Crew,
                    "Spain's mightiest Caribbean fortress. Approach with caution."),

                MakePort("tortuga",    "Tortuga",
                    new Vector3( -60f, 0f,   20f), "player_pirates",
                    PortService.Trade | PortService.Crew | PortService.Quest,
                    "The pirate republic — every wanted man finds shelter here."),

                MakePort("kingston",   "Kingston",
                    new Vector3(-100f, 0f,  -60f), "england",
                    PortService.Trade | PortService.Upgrade,
                    "A prosperous English colony on Jamaica's southern coast."),

                MakePort("port_royal", "Port Royal",
                    new Vector3( -90f, 0f,  -70f), "england",
                    PortService.Trade | PortService.Upgrade | PortService.Crew,
                    "Once the wickedest city in the world — now a Royal Navy stronghold."),

                MakePort("barbados",   "Barbados",
                    new Vector3( 140f, 0f,  -80f), "england",
                    PortService.Trade | PortService.Crew,
                    "Rich sugar island at the edge of the known sea lanes."),

                MakePort("cartagena",  "Cartagena",
                    new Vector3(  60f, 0f, -160f), "spain",
                    PortService.Trade | PortService.Upgrade | PortService.Quest,
                    "Spain's treasure fleet assembles here. A fortune awaits the bold."),

                MakePort("panama",     "Panama",
                    new Vector3( -20f, 0f, -180f), "spain",
                    PortService.Trade | PortService.Upgrade,
                    "Gateway to the Pacific — all New World silver passes through this port."),
            };
        }

        private static PortData MakePort(
            string id, string name, Vector3 pos, string faction,
            PortService services, string description)
        {
            var pd              = ScriptableObject.CreateInstance<PortData>();
            pd.portId           = id;
            pd.portName         = name;
            pd.worldPosition    = pos;
            pd.faction          = faction;
            pd.availableServices = services;
            pd.description      = description;
            return pd;
        }
    }
}
