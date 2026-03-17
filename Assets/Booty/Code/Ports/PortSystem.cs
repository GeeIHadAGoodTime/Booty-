using System;
using System.Collections.Generic;
using UnityEngine;
using Booty.Save;

namespace Booty.Ports
{
    /// <summary>
    /// Runtime representation of a port's mutable state.
    /// Static config (name, base_income, position) comes from ConfigService;
    /// this struct holds ownership and any runtime-mutable data.
    /// </summary>
    [Serializable]
    public class PortRuntimeData
    {
        public string portId;
        public string portName;
        public string regionId;
        public string factionOwner;
        public int baseIncome;
        public float defenseRating;
        public int level;
        public Vector3 worldPosition;
    }

    /// <summary>
    /// Central manager for all ports. Owns port state, handles capture flow,
    /// and provides query APIs for other systems (Economy, UI, Save).
    /// Schema owner: port.* fields per PRD C3.
    /// </summary>
    public class PortSystem : MonoBehaviour
    {
        /// <summary>Fired when a port changes ownership. Args: portId, newFactionId.</summary>
        public event Action<string, string> OnPortCaptured;

        private readonly Dictionary<string, PortRuntimeData> _ports = new Dictionary<string, PortRuntimeData>();

        [SerializeField] private float capturePromptDuration = 10f;

        // Reference set by GameRoot during wiring
        private SaveSystem _saveSystem;

        /// <summary>
        /// Initialize the port system with config data and a reference to SaveSystem.
        /// Called by GameRoot during bootstrap.
        /// </summary>
        /// <param name="portConfigs">Port definitions loaded from ports.json via ConfigService.</param>
        /// <param name="saveSystem">Reference to the save system for state persistence.</param>
        public void Initialize(List<PortRuntimeData> portConfigs, SaveSystem saveSystem)
        {
            _saveSystem = saveSystem;
            _ports.Clear();

            foreach (var config in portConfigs)
            {
                _ports[config.portId] = config;
            }

            // Apply saved ownership overrides if a save exists
            ApplySaveData();

            Debug.Log($"[PortSystem] Initialized with {_ports.Count} ports.");
        }

        /// <summary>
        /// Get runtime data for a specific port by ID.
        /// </summary>
        /// <param name="portId">The port identifier.</param>
        /// <returns>The port data, or null if not found.</returns>
        public PortRuntimeData GetPort(string portId)
        {
            _ports.TryGetValue(portId, out var data);
            return data;
        }

        /// <summary>
        /// Get all registered ports.
        /// </summary>
        /// <returns>Read-only collection of all port data.</returns>
        public IReadOnlyDictionary<string, PortRuntimeData> GetAllPorts()
        {
            return _ports;
        }

        /// <summary>
        /// Get all ports owned by a specific faction.
        /// </summary>
        /// <param name="factionId">The faction identifier.</param>
        /// <returns>List of ports belonging to the faction.</returns>
        public List<PortRuntimeData> GetPortsByFaction(string factionId)
        {
            var result = new List<PortRuntimeData>();
            foreach (var kvp in _ports)
            {
                if (kvp.Value.factionOwner == factionId)
                {
                    result.Add(kvp.Value);
                }
            }
            return result;
        }

        /// <summary>
        /// Get the count of ports owned by the player faction.
        /// </summary>
        /// <returns>Number of player-owned ports.</returns>
        public int GetPlayerPortCount()
        {
            return GetPortsByFaction("player_pirates").Count;
        }

        /// <summary>
        /// Attempt to capture a port after a naval victory. Changes faction_owner
        /// to the player faction, fires the OnPortCaptured event, and triggers auto-save.
        /// </summary>
        /// <param name="portId">The port to capture.</param>
        /// <returns>True if capture succeeded, false if the port was not found or already player-owned.</returns>
        public bool CapturePort(string portId)
        {
            if (!_ports.TryGetValue(portId, out var port))
            {
                Debug.LogWarning($"[PortSystem] CapturePort: port '{portId}' not found.");
                return false;
            }

            if (port.factionOwner == "player_pirates")
            {
                Debug.Log($"[PortSystem] Port '{portId}' is already player-owned.");
                return false;
            }

            string previousOwner = port.factionOwner;
            port.factionOwner = "player_pirates";

            Debug.Log($"[PortSystem] Port '{port.portName}' captured! " +
                      $"Ownership: {previousOwner} -> player_pirates");

            // Persist to save state
            WriteSaveData();

            // Notify listeners (Economy, UI, Renown, etc.)
            OnPortCaptured?.Invoke(portId, "player_pirates");

            // Auto-save on capture (key beat per SubPRD 2.8)
            if (_saveSystem != null)
            {
                _saveSystem.AutoSave();
            }

            return true;
        }

        /// <summary>
        /// Set a port's faction owner directly. Used by debug console and initial setup.
        /// Does not trigger capture events or auto-save.
        /// </summary>
        /// <param name="portId">The port to modify.</param>
        /// <param name="factionId">The new faction owner.</param>
        public void SetPortOwner(string portId, string factionId)
        {
            if (_ports.TryGetValue(portId, out var port))
            {
                port.factionOwner = factionId;
                Debug.Log($"[PortSystem] Port '{portId}' owner set to '{factionId}'.");
            }
            else
            {
                Debug.LogWarning($"[PortSystem] SetPortOwner: port '{portId}' not found.");
            }
        }

        /// <summary>
        /// Check if a port is friendly (player-owned or neutral) to the player.
        /// </summary>
        /// <param name="portId">The port to check.</param>
        /// <returns>True if the port is player-owned or neutral.</returns>
        public bool IsPortFriendly(string portId)
        {
            if (!_ports.TryGetValue(portId, out var port))
                return false;

            return port.factionOwner == "player_pirates" || port.factionOwner == "neutral_traders";
        }

        /// <summary>
        /// Check if a port is hostile (enemy-owned) to the player.
        /// </summary>
        /// <param name="portId">The port to check.</param>
        /// <returns>True if the port is enemy-owned.</returns>
        public bool IsPortHostile(string portId)
        {
            if (!_ports.TryGetValue(portId, out var port))
                return false;

            return port.factionOwner != "player_pirates" && port.factionOwner != "neutral_traders";
        }

        /// <summary>
        /// Get the number of defender ships to spawn for a port battle,
        /// based on defense_rating.
        /// </summary>
        /// <param name="portId">The port being attacked.</param>
        /// <returns>Number of defender ships to spawn.</returns>
        public int GetDefenderCount(string portId)
        {
            if (!_ports.TryGetValue(portId, out var port))
                return 1;

            // Simple: defense_rating 1-2 = 1 ship, 3-4 = 2 ships, 5+ = 3 ships
            if (port.defenseRating >= 5f) return 3;
            if (port.defenseRating >= 3f) return 2;
            return 1;
        }

        /// <summary>
        /// Apply saved port ownership data from the SaveSystem.
        /// Called during initialization to restore saved state.
        /// </summary>
        private void ApplySaveData()
        {
            if (_saveSystem == null || _saveSystem.CurrentState == null)
                return;

            var savedPorts = _saveSystem.CurrentState.ports;
            if (savedPorts == null || savedPorts.Count == 0)
                return;

            foreach (var saved in savedPorts)
            {
                if (_ports.TryGetValue(saved.portId, out var port))
                {
                    port.factionOwner = saved.factionOwner;
                }
            }

            Debug.Log($"[PortSystem] Applied {savedPorts.Count} saved port ownership entries.");
        }

        /// <summary>
        /// Write current port ownership state to the save system's in-memory state.
        /// </summary>
        private void WriteSaveData()
        {
            if (_saveSystem == null || _saveSystem.CurrentState == null)
                return;

            _saveSystem.CurrentState.ports.Clear();
            foreach (var kvp in _ports)
            {
                _saveSystem.CurrentState.ports.Add(new PortSaveData
                {
                    portId = kvp.Key,
                    factionOwner = kvp.Value.factionOwner
                });
            }
        }
    }
}
