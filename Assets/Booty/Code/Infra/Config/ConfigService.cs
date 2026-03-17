// ---------------------------------------------------------------------------
// ConfigService.cs — Loads and deserializes all JSON config TextAssets
// ---------------------------------------------------------------------------
// Per ImplementationTopology.md section 4.2:
//   - Deserializes ports.json, ships.json, factions.json via JsonUtility
//   - Exposes IReadOnlyList<T> for each config type
//   - Uses snake_case intermediate classes to bridge JSON field names to
//     the camelCase C# data classes (JsonUtility has no [JsonProperty] support)
//   - Assign TextAsset fields in the Inspector (Assets/Booty/Config/*.json)
// ---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Booty.Config
{
    /// <summary>
    /// Loads and deserializes all JSON configuration files at startup.
    /// Attach to the Bootstrap GameObject alongside BootyBootstrap.
    /// Assign the three TextAsset fields in the Inspector before entering play mode.
    /// </summary>
    public class ConfigService : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Inspector — assign Assets/Booty/Config/*.json TextAssets
        // ══════════════════════════════════════════════════════════════════

        [Header("Config JSON TextAssets")]
        [SerializeField] private TextAsset portsTextAsset;
        [SerializeField] private TextAsset shipsTextAsset;
        [SerializeField] private TextAsset factionsTextAsset;

        // ══════════════════════════════════════════════════════════════════
        //  Public Accessors
        // ══════════════════════════════════════════════════════════════════

        /// <summary>All port definitions loaded from ports.json.</summary>
        public IReadOnlyList<PortData> Ports { get; private set; }

        /// <summary>All ship archetypes loaded from ships.json.</summary>
        public IReadOnlyList<ShipData> Ships { get; private set; }

        /// <summary>All faction definitions loaded from factions.json.</summary>
        public IReadOnlyList<FactionData> Factions { get; private set; }

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Awake()
        {
            if (portsTextAsset != null && shipsTextAsset != null && factionsTextAsset != null)
            {
                Ports    = LoadPorts();
                Ships    = LoadShips();
                Factions = LoadFactions();
                Debug.Log(string.Format("[ConfigService] Loaded {0} ports, {1} ships, {2} factions.",
                    Ports.Count, Ships.Count, Factions.Count));
            }
            else
            {
                Ports    = new System.Collections.Generic.List<PortData>();
                Ships    = new System.Collections.Generic.List<ShipData>();
                Factions = new System.Collections.Generic.List<FactionData>();
                Debug.Log("[ConfigService] TextAssets not assigned in Inspector. Call Configure() to load.");
            }
        }

        /// <summary>
        /// Inject TextAsset references at runtime (bypasses Inspector requirement)
        /// and immediately reloads all config data.
        /// </summary>
        /// <param name="portsJson">ports.json TextAsset.</param>
        /// <param name="shipsJson">ships.json TextAsset.</param>
        /// <param name="factionsJson">factions.json TextAsset.</param>
        public void Configure(TextAsset portsJson, TextAsset shipsJson, TextAsset factionsJson)
        {
            portsTextAsset    = portsJson;
            shipsTextAsset    = shipsJson;
            factionsTextAsset = factionsJson;

            Ports    = LoadPorts();
            Ships    = LoadShips();
            Factions = LoadFactions();

            Debug.Log("[ConfigService] Configured and reloaded.");
        }

        // ══════════════════════════════════════════════════════════════════
        //  Port Loading
        // ══════════════════════════════════════════════════════════════════

        private List<PortData> LoadPorts()
        {
            if (portsTextAsset == null)
            {
                Debug.LogError("[ConfigService] portsTextAsset is not assigned.");
                return new List<PortData>();
            }

            var wrapper = JsonUtility.FromJson<PortListJson>(portsTextAsset.text);
            if (wrapper == null || wrapper.ports == null)
            {
                Debug.LogError("[ConfigService] Failed to parse ports.json.");
                return new List<PortData>();
            }

            var result = new List<PortData>(wrapper.ports.Count);
            foreach (var j in wrapper.ports)
            {
                result.Add(new PortData
                {
                    id           = j.id,
                    name         = j.name,
                    regionId     = j.region_id,
                    factionOwner = j.faction_owner,
                    baseIncome   = j.base_income,
                    defenseRating = j.defense_rating,
                    level        = j.level,
                    // PortData has no position fields; position is carried by
                    // PortRuntimeData in RegionSetup via the same JSON TextAsset.
                });
            }

            return result;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Ship Loading
        // ══════════════════════════════════════════════════════════════════

        private List<ShipData> LoadShips()
        {
            if (shipsTextAsset == null)
            {
                Debug.LogError("[ConfigService] shipsTextAsset is not assigned.");
                return new List<ShipData>();
            }

            var wrapper = JsonUtility.FromJson<ShipListJson>(shipsTextAsset.text);
            if (wrapper == null || wrapper.ships == null)
            {
                Debug.LogError("[ConfigService] Failed to parse ships.json.");
                return new List<ShipData>();
            }

            var result = new List<ShipData>(wrapper.ships.Count);
            foreach (var j in wrapper.ships)
            {
                result.Add(new ShipData
                {
                    id            = j.id,
                    name          = j.name,
                    shipClass     = j.ship_class,
                    hull          = j.hull,
                    sail          = j.sail,
                    speed         = j.speed,
                    turnRate      = j.turn_rate,
                    broadsideSlots = j.broadside_slots,
                    crewMin       = j.crew_min,
                    crewOptimal   = j.crew_optimal,
                    value         = j.value,
                    tier          = j.tier,
                });
            }

            return result;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Faction Loading
        // ══════════════════════════════════════════════════════════════════

        private List<FactionData> LoadFactions()
        {
            if (factionsTextAsset == null)
            {
                Debug.LogError("[ConfigService] factionsTextAsset is not assigned.");
                return new List<FactionData>();
            }

            var wrapper = JsonUtility.FromJson<FactionListJson>(factionsTextAsset.text);
            if (wrapper == null || wrapper.factions == null)
            {
                Debug.LogError("[ConfigService] Failed to parse factions.json.");
                return new List<FactionData>();
            }

            var result = new List<FactionData>(wrapper.factions.Count);
            foreach (var j in wrapper.factions)
            {
                var faction = new FactionData
                {
                    id    = j.id,
                    name  = j.name,
                    color = new int[3] { j.color_r, j.color_g, j.color_b },
                    diplomacyFlags = new DiplomacyFlags
                    {
                        isMajor  = j.is_major,
                        isPirate = j.is_pirate,
                        recognizesPlayerKingdom = false,
                    },
                };
                result.Add(faction);
            }

            return result;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Intermediate JSON classes — mirror JSON snake_case field names
        //  exactly so JsonUtility can deserialize without attribute support.
        // ══════════════════════════════════════════════════════════════════

        // ---- Ports ----

        [Serializable]
        private class PortJson
        {
            public string id;
            public string name;
            public string region_id;
            public string faction_owner;
            public float  base_income;
            public float  defense_rating;
            public int    level;
            public float  position_x;
            public float  position_z;
        }

        [Serializable]
        private class PortListJson
        {
            public List<PortJson> ports;
        }

        // ---- Ships ----

        [Serializable]
        private class ShipJson
        {
            public string id;
            public string name;
            public string ship_class;
            public int    hull;
            public int    sail;
            public float  speed;
            public float  turn_rate;
            public int    broadside_slots;
            public int    crew_min;
            public int    crew_optimal;
            public int    value;
            public int    tier;
        }

        [Serializable]
        private class ShipListJson
        {
            public List<ShipJson> ships;
        }

        // ---- Factions ----

        [Serializable]
        private class FactionJson
        {
            public string id;
            public string name;
            public int    color_r;
            public int    color_g;
            public int    color_b;
            public bool   is_major;
            public bool   is_pirate;
        }

        [Serializable]
        private class FactionListJson
        {
            public List<FactionJson> factions;
        }
    }
}
