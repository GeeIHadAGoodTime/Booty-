// ---------------------------------------------------------------------------
// GameRoot.cs — Single composition root / global anchor for all systems
// ---------------------------------------------------------------------------
// Per ImplementationTopology.md section 3.1:
//   - GameRoot is the ONLY class with a public static Instance.
//   - All core systems live as components on GameRoot or direct children.
//   - No gameplay logic belongs here — only references and initialization.
//   - No DontDestroyOnLoad, no additional singletons.
// ---------------------------------------------------------------------------

using UnityEngine;
using Booty.Ships;
using Booty.World;
using Booty.Combat;

namespace Booty.Bootstrap
{
    /// <summary>
    /// Central hub that holds references to every runtime system.
    /// Created and wired by <see cref="BootyBootstrap"/>. Contains no
    /// gameplay logic — only reference storage and singleton enforcement.
    /// </summary>
    public class GameRoot : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Singleton (the ONLY allowed static Instance in the project)
        // ══════════════════════════════════════════════════════════════════

        public static GameRoot Instance { get; private set; }

        // ══════════════════════════════════════════════════════════════════
        //  System References — populated by BootyBootstrap
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Player ship movement controller.</summary>
        public ShipController PlayerShip { get; private set; }

        /// <summary>Isometric follow camera.</summary>
        public IsometricCamera Camera { get; private set; }

        /// <summary>Broadside combat system.</summary>
        public BroadsideSystem BroadsideSystem { get; private set; }

        /// <summary>Hull HP / damage system.</summary>
        public HPSystem HPSystem { get; private set; }

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[GameRoot] Duplicate detected — destroying this instance.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Initialization API — called by BootyBootstrap only
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Wire all system references. Called once by <see cref="BootyBootstrap"/>.
        /// </summary>
        public void Initialize(
            ShipController playerShip,
            IsometricCamera camera,
            BroadsideSystem broadsideSystem,
            HPSystem hpSystem)
        {
            PlayerShip = playerShip;
            Camera = camera;
            BroadsideSystem = broadsideSystem;
            HPSystem = hpSystem;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
