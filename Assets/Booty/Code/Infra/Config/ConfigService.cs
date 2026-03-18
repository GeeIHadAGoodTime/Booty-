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
        //  Embedded Default JSON (used when TextAssets are not assigned)
        // ══════════════════════════════════════════════════════════════════

        private const string DEFAULT_PORTS_JSON =
            "{\"ports\":[" +
            "{\"id\":\"port_haven\",\"name\":\"Port Haven\",\"region_id\":\"silver_sea\",\"faction_owner\":\"player_pirates\",\"base_income\":50,\"defense_rating\":2.0,\"level\":1,\"position_x\":-40.0,\"position_z\":30.0}," +
            "{\"id\":\"fort_imperial\",\"name\":\"Fort Imperial\",\"region_id\":\"silver_sea\",\"faction_owner\":\"spanish_crown\",\"base_income\":80,\"defense_rating\":4.0,\"level\":2,\"position_x\":50.0,\"position_z\":40.0}," +
            "{\"id\":\"nassau\",\"name\":\"Nassau\",\"region_id\":\"silver_sea\",\"faction_owner\":\"british_crown\",\"base_income\":70,\"defense_rating\":3.0,\"level\":2,\"position_x\":-80.0,\"position_z\":120.0}," +
            "{\"id\":\"havana\",\"name\":\"Havana\",\"region_id\":\"silver_sea\",\"faction_owner\":\"spanish_crown\",\"base_income\":100,\"defense_rating\":5.0,\"level\":3,\"position_x\":-120.0,\"position_z\":40.0}," +
            "{\"id\":\"tortuga\",\"name\":\"Tortuga\",\"region_id\":\"silver_sea\",\"faction_owner\":\"player_pirates\",\"base_income\":60,\"defense_rating\":2.0,\"level\":1,\"position_x\":-60.0,\"position_z\":20.0}," +
            "{\"id\":\"kingston\",\"name\":\"Kingston\",\"region_id\":\"silver_sea\",\"faction_owner\":\"british_crown\",\"base_income\":80,\"defense_rating\":4.0,\"level\":2,\"position_x\":-100.0,\"position_z\":-60.0}," +
            "{\"id\":\"cartagena\",\"name\":\"Cartagena\",\"region_id\":\"silver_sea\",\"faction_owner\":\"spanish_crown\",\"base_income\":90,\"defense_rating\":4.5,\"level\":2,\"position_x\":60.0,\"position_z\":-160.0}," +
            "{\"id\":\"smugglers_cove\",\"name\":\"Smugglers Cove\",\"region_id\":\"silver_sea\",\"faction_owner\":\"neutral_traders\",\"base_income\":30,\"defense_rating\":1.0,\"level\":1,\"position_x\":10.0,\"position_z\":-50.0}" +
            "]}";

        private const string DEFAULT_SHIPS_JSON =
            "{\"ships\":[" +
            "{\"id\":\"sloop\",\"name\":\"Sloop\",\"ship_class\":\"sloop\",\"hull\":80,\"sail\":60,\"speed\":8.0,\"turn_rate\":90.0,\"broadside_slots\":4,\"crew_min\":10,\"crew_optimal\":20,\"value\":500,\"tier\":1}," +
            "{\"id\":\"brig\",\"name\":\"Brigantine\",\"ship_class\":\"brig\",\"hull\":150,\"sail\":100,\"speed\":6.0,\"turn_rate\":60.0,\"broadside_slots\":8,\"crew_min\":30,\"crew_optimal\":60,\"value\":1500,\"tier\":2}," +
            "{\"id\":\"galleon\",\"name\":\"Galleon\",\"ship_class\":\"galleon\",\"hull\":250,\"sail\":150,\"speed\":4.0,\"turn_rate\":35.0,\"broadside_slots\":14,\"crew_min\":80,\"crew_optimal\":150,\"value\":4000,\"tier\":3}" +
            "]}";

        private const string DEFAULT_FACTIONS_JSON =
            "{\"factions\":[" +
            "{\"id\":\"player_pirates\",\"name\":\"The Free Captains\",\"color_r\":255,\"color_g\":215,\"color_b\":0,\"is_major\":false,\"is_pirate\":true}," +
            "{\"id\":\"british_crown\",\"name\":\"The British Crown\",\"color_r\":1,\"color_g\":52,\"color_b\":154,\"is_major\":true,\"is_pirate\":false}," +
            "{\"id\":\"spanish_crown\",\"name\":\"The Spanish Crown\",\"color_r\":200,\"color_g\":30,\"color_b\":30,\"is_major\":true,\"is_pirate\":false}," +
            "{\"id\":\"french_crown\",\"name\":\"The French Crown\",\"color_r\":1,\"color_g\":52,\"color_b\":128,\"is_major\":true,\"is_pirate\":false}" +
            "]}";

        // ══════════════════════════════════════════════════════════════════
        //  Port Loading
        // ══════════════════════════════════════════════════════════════════

        private List<PortData> LoadPorts()
        {
            if (portsTextAsset == null)
            {
                Debug.LogWarning("[ConfigService] portsTextAsset not assigned — using embedded Caribbean defaults.");
                return ParseDefaultPorts();
            }

            var wrapper = JsonUtility.FromJson<PortListJson>(portsTextAsset.text);
            if (wrapper == null || wrapper.ports == null)
            {
                Debug.LogWarning("[ConfigService] Failed to parse ports.json — using embedded Caribbean defaults.");
                return ParseDefaultPorts();
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

        private List<PortData> ParseDefaultPorts()
        {
            var wrapper = JsonUtility.FromJson<PortListJson>(DEFAULT_PORTS_JSON);
            if (wrapper == null || wrapper.ports == null)
            {
                Debug.LogWarning("[ConfigService] Failed to parse embedded ports JSON.");
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
                Debug.LogWarning("[ConfigService] shipsTextAsset not assigned — using embedded ship defaults.");
                return ParseDefaultShips();
            }

            var wrapper = JsonUtility.FromJson<ShipListJson>(shipsTextAsset.text);
            if (wrapper == null || wrapper.ships == null)
            {
                Debug.LogWarning("[ConfigService] Failed to parse ships.json — using embedded ship defaults.");
                return ParseDefaultShips();
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

        private List<ShipData> ParseDefaultShips()
        {
            var wrapper = JsonUtility.FromJson<ShipListJson>(DEFAULT_SHIPS_JSON);
            if (wrapper == null || wrapper.ships == null)
            {
                Debug.LogWarning("[ConfigService] Failed to parse embedded ships JSON.");
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
                Debug.LogWarning("[ConfigService] factionsTextAsset not assigned — using embedded faction defaults.");
                return ParseDefaultFactions();
            }

            var wrapper = JsonUtility.FromJson<FactionListJson>(factionsTextAsset.text);
            if (wrapper == null || wrapper.factions == null)
            {
                Debug.LogWarning("[ConfigService] Failed to parse factions.json — using embedded faction defaults.");
                return ParseDefaultFactions();
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

        private List<FactionData> ParseDefaultFactions()
        {
            var wrapper = JsonUtility.FromJson<FactionListJson>(DEFAULT_FACTIONS_JSON);
            if (wrapper == null || wrapper.factions == null)
            {
                Debug.LogWarning("[ConfigService] Failed to parse embedded factions JSON.");
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
