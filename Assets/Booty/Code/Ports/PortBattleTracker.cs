// ---------------------------------------------------------------------------
// PortBattleTracker.cs — Tracks enemy defenders near ports; triggers capture
// prompt when all defenders are defeated.
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace Booty.Ports
{
    /// <summary>
    /// Tracks enemy ship counts near each hostile port.
    /// When all defenders near a hostile port are defeated (enemy count drops to 0),
    /// calls ShowCapturePrompt() on that port's PortInteraction component.
    /// Initialize() called by BootyBootstrap after RegionSetup.BuildRegion().
    /// Uses Start() to discover PortInteraction components after scene setup.
    /// </summary>
    public class PortBattleTracker : MonoBehaviour
    {
        [Header("Battle Detection")]
        [SerializeField] private float battleRadius = 50f;

        private PortSystem _portSystem;

        private readonly Dictionary<string, PortInteraction> _portInteractions =
            new Dictionary<string, PortInteraction>();

        // True if the port had at least one enemy defender at some point this check cycle
        private readonly Dictionary<string, bool> _portWasContested =
            new Dictionary<string, bool>();

        /// <summary>
        /// Initialize with system references. Called by BootyBootstrap.
        /// </summary>
        public void Initialize(PortSystem portSystem)
        {
            _portSystem = portSystem;
        }

        private void Start()
        {
            // Discover all PortInteraction components spawned by RegionSetup
            var allInteractions = FindObjectsOfType<PortInteraction>();
            foreach (var pi in allInteractions)
            {
                _portInteractions[pi.PortId] = pi;
                _portWasContested[pi.PortId] = false;
            }

            Debug.Log(string.Format("[PortBattleTracker] Tracking {0} ports.",
                _portInteractions.Count));
        }

        private void Update()
        {
            if (_portSystem == null) return;

            var allPorts = _portSystem.GetAllPorts();
            foreach (var kvp in allPorts)
            {
                string portId       = kvp.Key;
                var    portData     = kvp.Value;

                // Only track hostile ports
                if (!_portSystem.IsPortHostile(portId))
                {
                    if (_portWasContested.ContainsKey(portId))
                        _portWasContested[portId] = false;
                    continue;
                }

                int enemyCount = CountEnemiesNear(portData.worldPosition);

                bool wasContested = _portWasContested.ContainsKey(portId) &&
                                    _portWasContested[portId];

                if (enemyCount > 0)
                {
                    // Defenders present — mark contested
                    _portWasContested[portId] = true;
                }
                else if (wasContested)
                {
                    // Was contested, now cleared — trigger capture prompt
                    _portWasContested[portId] = false;
                    TriggerCapturePrompt(portId);
                }
            }
        }

        private int CountEnemiesNear(Vector3 position)
        {
            Collider[] hits  = Physics.OverlapSphere(position, battleRadius);
            int        count = 0;
            foreach (var col in hits)
            {
                if (col.CompareTag("Enemy"))
                    count++;
            }
            return count;
        }

        private void TriggerCapturePrompt(string portId)
        {
            if (_portInteractions.TryGetValue(portId, out PortInteraction interaction))
            {
                interaction.ShowCapturePrompt();
                Debug.Log(string.Format(
                    "[PortBattleTracker] Defenders cleared at '{0}' — capture prompt shown.",
                    portId));
            }
            else
            {
                Debug.LogWarning(string.Format(
                    "[PortBattleTracker] No PortInteraction found for '{0}'.", portId));
            }
        }
    }
}
