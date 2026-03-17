using UnityEngine;
using Booty.Audio;
using Booty.Economy;
using Booty.Save;

namespace Booty.Ports
{
    /// <summary>
    /// Handles player-port interaction: docking, repair menu, resupply,
    /// and capture prompts after naval victories.
    /// Attached to each port GameObject in the world.
    /// </summary>
    public class PortInteraction : MonoBehaviour
    {
        [SerializeField] private string portId = "";
        [SerializeField] private float dockRadius = 15f;
        [SerializeField] private float attackRadius = 25f;

        // References set via Configure method from GameRoot or RegionSetup
        private PortSystem _portSystem;
        private EconomySystem _economySystem;
        private RepairShop _repairShop;
        private SaveSystem _saveSystem;

        private bool _playerInDockRange;
        private bool _playerInAttackRange;
        private bool _isDocked;
        private bool _showingCapturePrompt;

        /// <summary>Whether the player is currently docked at this port.</summary>
        public bool IsDocked => _isDocked;

        /// <summary>The port ID this interaction component manages.</summary>
        public string PortId => portId;

        /// <summary>
        /// Configure this interaction with references to game systems.
        /// Called after instantiation by RegionSetup or GameRoot.
        /// </summary>
        /// <param name="portSystem">The central port system.</param>
        /// <param name="economySystem">The economy system for gold transactions.</param>
        /// <param name="repairShop">The repair shop for hull repairs.</param>
        /// <param name="saveSystem">The save system for state persistence.</param>
        public void Configure(PortSystem portSystem, EconomySystem economySystem,
                              RepairShop repairShop, SaveSystem saveSystem)
        {
            _portSystem = portSystem;
            _economySystem = economySystem;
            _repairShop = repairShop;
            _saveSystem = saveSystem;
        }

        /// <summary>
        /// Set the port ID this component manages. Called during world setup.
        /// </summary>
        /// <param name="id">The port identifier from config.</param>
        public void SetPortId(string id)
        {
            portId = id;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player"))
                return;

            float dist = Vector3.Distance(transform.position, other.transform.position);

            if (dist <= dockRadius)
            {
                _playerInDockRange = true;
                OnPlayerEnteredDockRange();
            }
            else if (dist <= attackRadius)
            {
                _playerInAttackRange = true;
            }

            // Switch to port music when player enters port trigger zone
            FindObjectOfType<AmbientAudio>()?.EnterPort();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player"))
                return;

            _playerInDockRange = false;
            _playerInAttackRange = false;
            Undock();

            // Return to sailing music when player leaves port trigger zone
            FindObjectOfType<AmbientAudio>()?.ExitPort();
        }

        private void Update()
        {
            // Keyboard shortcut to dock when in range
            if (_playerInDockRange && !_isDocked && Input.GetKeyDown(KeyCode.F))
            {
                TryDock();
            }

            // Keyboard shortcut to undock
            if (_isDocked && Input.GetKeyDown(KeyCode.F))
            {
                Undock();
            }

            // Capture prompt response
            if (_showingCapturePrompt)
            {
                if (Input.GetKeyDown(KeyCode.Y))
                {
                    AcceptCapture();
                }
                else if (Input.GetKeyDown(KeyCode.N))
                {
                    DeclineCapture();
                }
            }
        }

        /// <summary>
        /// Called when the player enters dock range. Displays context-appropriate prompt.
        /// </summary>
        private void OnPlayerEnteredDockRange()
        {
            if (_portSystem == null)
                return;

            if (_portSystem.IsPortFriendly(portId))
            {
                Debug.Log($"[PortInteraction] Near friendly port '{portId}'. Press F to dock.");
            }
            else
            {
                Debug.Log($"[PortInteraction] Near enemy port '{portId}'. Defeat defenders to capture.");
            }
        }

        /// <summary>
        /// Attempt to dock at this port. Only succeeds at friendly or player-owned ports.
        /// </summary>
        /// <returns>True if docking succeeded.</returns>
        public bool TryDock()
        {
            if (_portSystem == null)
                return false;

            if (!_portSystem.IsPortFriendly(portId))
            {
                Debug.Log($"[PortInteraction] Cannot dock at hostile port '{portId}'.");
                return false;
            }

            _isDocked = true;
            Debug.Log($"[PortInteraction] Docked at '{portId}'. Repair / Resupply available.");
            return true;
        }

        /// <summary>
        /// Undock from the current port.
        /// </summary>
        public void Undock()
        {
            if (_isDocked)
            {
                _isDocked = false;
                _showingCapturePrompt = false;
                Debug.Log($"[PortInteraction] Undocked from '{portId}'.");
            }
        }

        /// <summary>
        /// Request hull repair at this port. Delegates to RepairShop.
        /// Only available when docked at a friendly port.
        /// </summary>
        /// <returns>True if repair was performed.</returns>
        public bool RequestRepair()
        {
            if (!_isDocked)
            {
                Debug.Log("[PortInteraction] Must be docked to repair.");
                return false;
            }

            if (_repairShop == null)
            {
                Debug.LogWarning("[PortInteraction] RepairShop not configured.");
                return false;
            }

            return _repairShop.RepairShip();
        }

        /// <summary>
        /// Show the capture prompt after defeating a port's naval defenders.
        /// Called by the combat system when a port battle is won.
        /// </summary>
        public void ShowCapturePrompt()
        {
            if (_portSystem == null)
                return;

            if (_portSystem.IsPortFriendly(portId))
            {
                Debug.Log($"[PortInteraction] Port '{portId}' is already friendly. No capture needed.");
                return;
            }

            _showingCapturePrompt = true;
            Debug.Log($"[PortInteraction] Port defenders defeated! Capture '{portId}'? [Y]es / [N]o");
        }

        /// <summary>
        /// Accept the capture of this port.
        /// </summary>
        private void AcceptCapture()
        {
            _showingCapturePrompt = false;

            if (_portSystem != null)
            {
                _portSystem.CapturePort(portId);
                Debug.Log($"[PortInteraction] Captured port '{portId}'!");
            }
        }

        /// <summary>
        /// Decline the capture of this port.
        /// </summary>
        private void DeclineCapture()
        {
            _showingCapturePrompt = false;
            Debug.Log($"[PortInteraction] Declined to capture '{portId}'.");
        }

        /// <summary>
        /// Check whether the capture prompt is currently displayed.
        /// Used by UI to render the prompt overlay.
        /// </summary>
        /// <returns>True if waiting for capture decision.</returns>
        public bool IsShowingCapturePrompt()
        {
            return _showingCapturePrompt;
        }

        private void OnDrawGizmosSelected()
        {
            // Visualize dock and attack radii in the editor
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, dockRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRadius);
        }
    }
}
