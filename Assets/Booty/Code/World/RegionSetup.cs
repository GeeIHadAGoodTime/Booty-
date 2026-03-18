using System.Collections.Generic;
using UnityEngine;
using Booty.Ports;
using Booty.Economy;
using Booty.Save;

namespace Booty.World
{
    /// <summary>
    /// Defines and instantiates the Silver Sea region with 3-4 ports for the P1 vertical slice.
    /// Reads port config from a JSON TextAsset, creates port GameObjects in the scene,
    /// and wires them with PortInteraction, PortVisual, and collider triggers.
    /// </summary>
    public class RegionSetup : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private TextAsset portsConfigJson;

        [Header("Port Prefab (optional)")]
        [SerializeField] private GameObject portPrefab;

        [Header("Port Visual Defaults")]
        [SerializeField] private float portColliderRadius = 20f;
        [SerializeField] private float portIslandScale = 5f;

        // System references set by GameRoot
        private PortSystem _portSystem;
        private EconomySystem _economySystem;
        private RepairShop _repairShop;
        private SaveSystem _saveSystem;

        private readonly List<GameObject> _portObjects = new List<GameObject>();

        /// <summary>Sets the ports config JSON for runtime injection (bypasses Inspector requirement).</summary>
        public void SetPortsConfig(TextAsset portsJson)
        {
            portsConfigJson = portsJson;
        }

        /// <summary>
        /// Initialize the region setup with system references.
        /// Called by GameRoot during bootstrap.
        /// </summary>
        /// <param name="portSystem">The port system.</param>
        /// <param name="economySystem">The economy system.</param>
        /// <param name="repairShop">The repair shop.</param>
        /// <param name="saveSystem">The save system.</param>
        public void Initialize(PortSystem portSystem, EconomySystem economySystem,
                               RepairShop repairShop, SaveSystem saveSystem)
        {
            _portSystem = portSystem;
            _economySystem = economySystem;
            _repairShop = repairShop;
            _saveSystem = saveSystem;
        }

        /// <summary>
        /// Build the region: parse config, create port data, initialize the PortSystem,
        /// and spawn port GameObjects in the scene.
        /// </summary>
        public void BuildRegion()
        {
            var portConfigs = ParsePortConfigs();
            if (portConfigs == null || portConfigs.Count == 0)
            {
                Debug.LogWarning("[RegionSetup] No port configs parsed — using built-in Caribbean defaults.");
                portConfigs = GetDefaultPortConfigs();
            }

            // Initialize the port system with parsed configs
            _portSystem.Initialize(portConfigs, _saveSystem);

            // Spawn port objects in the scene
            foreach (var config in portConfigs)
            {
                SpawnPort(config);
            }

            Debug.Log($"[RegionSetup] Silver Sea region built with {portConfigs.Count} ports.");
        }

        /// <summary>
        /// Parse port definitions from the JSON TextAsset.
        /// Falls back to built-in Caribbean defaults when no TextAsset is assigned.
        /// </summary>
        /// <returns>List of port runtime data from config.</returns>
        private List<PortRuntimeData> ParsePortConfigs()
        {
            if (portsConfigJson == null)
            {
                Debug.LogWarning("[RegionSetup] Ports config JSON not assigned — using built-in Caribbean defaults.");
                return GetDefaultPortConfigs();
            }

            var wrapper = JsonUtility.FromJson<PortConfigWrapper>(portsConfigJson.text);
            if (wrapper == null || wrapper.ports == null)
            {
                Debug.LogWarning("[RegionSetup] Failed to parse ports config JSON — using built-in Caribbean defaults.");
                return GetDefaultPortConfigs();
            }

            var result = new List<PortRuntimeData>();
            foreach (var entry in wrapper.ports)
            {
                result.Add(new PortRuntimeData
                {
                    portId = entry.id,
                    portName = entry.name,
                    regionId = entry.region_id,
                    factionOwner = entry.faction_owner,
                    baseIncome = entry.base_income,
                    defenseRating = entry.defense_rating,
                    level = entry.level,
                    worldPosition = new Vector3(entry.position_x, 0f, entry.position_z)
                });
            }

            return result;
        }

        /// <summary>
        /// Spawn a port GameObject at the configured world position.
        /// Creates a placeholder island with collider, interaction, and visual components.
        /// </summary>
        /// <param name="portData">The port configuration to spawn.</param>
        private void SpawnPort(PortRuntimeData portData)
        {
            GameObject portObj;

            if (portPrefab != null)
            {
                portObj = Instantiate(portPrefab, portData.worldPosition, Quaternion.identity);
            }
            else
            {
                // Create placeholder port: a small island with a flag
                portObj = CreatePlaceholderPort(portData);
            }

            portObj.name = $"Port_{portData.portId}";
            portObj.transform.SetParent(transform);

            // Build settlement structures (warehouse, dock, flag, tower)
            var structures = portObj.AddComponent<PortStructures>();
            structures.Build(portObj.transform);

            // Add PortInteraction component
            var interaction = portObj.GetComponent<PortInteraction>();
            if (interaction == null)
            {
                interaction = portObj.AddComponent<PortInteraction>();
            }
            interaction.SetPortId(portData.portId);
            interaction.Configure(_portSystem, _economySystem, _repairShop, _saveSystem);

            // Add PortVisual component
            var visual = portObj.GetComponent<PortVisual>();
            if (visual == null)
            {
                visual = portObj.AddComponent<PortVisual>();
            }
            visual.Configure(portData.portId, _portSystem);

            // Add a sphere collider as trigger for dock/attack ranges
            var existingCollider = portObj.GetComponent<SphereCollider>();
            if (existingCollider == null)
            {
                var col = portObj.AddComponent<SphereCollider>();
                col.isTrigger = true;
                col.radius = portColliderRadius;
            }

            _portObjects.Add(portObj);
        }

        /// <summary>
        /// Create a placeholder port visual when no prefab is assigned.
        /// Uses primitive shapes as stand-in geometry.
        /// </summary>
        /// <param name="portData">Port configuration data.</param>
        /// <returns>The created port GameObject.</returns>
        private GameObject CreatePlaceholderPort(PortRuntimeData portData)
        {
            // Island base (flattened sphere)
            var portObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            portObj.transform.position = portData.worldPosition;
            portObj.transform.localScale = new Vector3(portIslandScale, 0.5f, portIslandScale);

            // Set island color to sandy brown
            var renderer = portObj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.76f, 0.70f, 0.50f);
            }

            // Remove default collider (we add our own trigger collider)
            var defaultCollider = portObj.GetComponent<Collider>();
            if (defaultCollider != null)
            {
                Destroy(defaultCollider);
            }

            // Add a flag pole indicator
            var flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            flag.name = "Flag";
            flag.transform.SetParent(portObj.transform);
            flag.transform.localPosition = new Vector3(0f, 3f, 0f);
            flag.transform.localScale = new Vector3(0.1f, 4f, 0.1f);

            // Remove flag collider
            var flagCollider = flag.GetComponent<Collider>();
            if (flagCollider != null)
            {
                Destroy(flagCollider);
            }

            // Add a label (visible in editor; at runtime could be replaced with world-space UI)
            portObj.name = portData.portName;

            return portObj;
        }

        /// <summary>
        /// Get all spawned port GameObjects.
        /// </summary>
        /// <returns>List of port GameObjects in the scene.</returns>
        public List<GameObject> GetPortObjects()
        {
            return _portObjects;
        }

        /// <summary>
        /// Returns hardcoded Caribbean port configs used when no JSON TextAsset is assigned.
        /// Positions are on the XZ plane at a scale matching the default 300-unit world.
        /// </summary>
        private static List<PortRuntimeData> GetDefaultPortConfigs()
        {
            return new List<PortRuntimeData>
            {
                new PortRuntimeData { portId = "nassau",        portName = "Nassau",        regionId = "silver_sea", factionOwner = "british_crown",   baseIncome = 70,  defenseRating = 3.0f, level = 2, worldPosition = new Vector3(-80f,  0f,  120f) },
                new PortRuntimeData { portId = "havana",        portName = "Havana",        regionId = "silver_sea", factionOwner = "spanish_crown",   baseIncome = 100, defenseRating = 5.0f, level = 3, worldPosition = new Vector3(-120f, 0f,   40f) },
                new PortRuntimeData { portId = "tortuga",       portName = "Tortuga",       regionId = "silver_sea", factionOwner = "player_pirates",  baseIncome = 60,  defenseRating = 2.0f, level = 1, worldPosition = new Vector3( -60f, 0f,   20f) },
                new PortRuntimeData { portId = "kingston",      portName = "Kingston",      regionId = "silver_sea", factionOwner = "british_crown",   baseIncome = 80,  defenseRating = 4.0f, level = 2, worldPosition = new Vector3(-100f, 0f,  -60f) },
                new PortRuntimeData { portId = "port_royal",    portName = "Port Royal",    regionId = "silver_sea", factionOwner = "british_crown",   baseIncome = 75,  defenseRating = 4.5f, level = 2, worldPosition = new Vector3( -90f, 0f,  -70f) },
                new PortRuntimeData { portId = "cartagena",     portName = "Cartagena",     regionId = "silver_sea", factionOwner = "spanish_crown",   baseIncome = 90,  defenseRating = 4.5f, level = 2, worldPosition = new Vector3(  60f, 0f, -160f) },
                new PortRuntimeData { portId = "smugglers_cove",portName = "Smugglers Cove",regionId = "silver_sea", factionOwner = "neutral_traders", baseIncome = 30,  defenseRating = 1.0f, level = 1, worldPosition = new Vector3(  10f, 0f,  -50f) },
                new PortRuntimeData { portId = "port_haven",    portName = "Port Haven",    regionId = "silver_sea", factionOwner = "player_pirates",  baseIncome = 50,  defenseRating = 2.0f, level = 1, worldPosition = new Vector3( -40f, 0f,   30f) },
            };
        }
    }

    // ---- JSON deserialization helpers ----
    // These match the ports.json structure for JsonUtility parsing.

    [System.Serializable]
    internal class PortConfigWrapper
    {
        public PortConfigEntry[] ports;
    }

    [System.Serializable]
    internal class PortConfigEntry
    {
        public string id;
        public string name;
        public string region_id;
        public string faction_owner;
        public int base_income;
        public float defense_rating;
        public int level;
        public float position_x;
        public float position_z;
    }
}
