// ---------------------------------------------------------------------------
// BroadsideSystem.cs — 2D cone firing arcs (port / starboard), volley spawning
// ---------------------------------------------------------------------------
// Per PRD A3: broadside arcs as 2D cones/sectors off port & starboard.
// Per SubPRD 4.1: left/right click (or Q/E) for left/right broadside.
//
// Attached to every ship with cannons. Reads player input when the owning
// ShipController is player-controlled; otherwise exposes FirePort/FireStarboard
// for AI use.
// ---------------------------------------------------------------------------

using UnityEngine;

namespace Booty.Combat
{
    /// <summary>
    /// Handles broadside cannon volleys for a single ship.
    /// Manages firing arcs, cooldowns, and projectile spawning.
    /// </summary>
    public class BroadsideSystem : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Inspector
        // ══════════════════════════════════════════════════════════════════

        [Header("Broadside Tuning")]
        [SerializeField] private int   damage          = CombatConfig.BaseDamage;
        [SerializeField] private float firingRange     = CombatConfig.FiringRange;
        [SerializeField] private float halfAngle       = CombatConfig.BroadsideHalfAngle;
        [SerializeField] private float cooldown        = CombatConfig.FireCooldown;
        [SerializeField] private int   projectileCount = CombatConfig.ProjectilesPerVolley;
        [SerializeField] private float spreadAngle     = CombatConfig.VolleySpreadAngle;

        // ══════════════════════════════════════════════════════════════════
        //  Runtime State
        // ══════════════════════════════════════════════════════════════════

        private Ships.ShipController _ship;
        private float _portCooldownTimer;
        private float _starboardCooldownTimer;

        // Upgrade multiplier applied to broadside damage (default 1.0 = no bonus)
        private float _damageBonusMultiplier = 1f;

        /// <summary>True if the port (left) broadside is ready to fire.</summary>
        public bool PortReady => _portCooldownTimer <= 0f;

        /// <summary>True if the starboard (right) broadside is ready to fire.</summary>
        public bool StarboardReady => _starboardCooldownTimer <= 0f;

        /// <summary>Firing range used for arc checks.</summary>
        public float FiringRange => firingRange;

        /// <summary>Broadside half-angle in degrees.</summary>
        public float HalfAngle => halfAngle;

        // ════════════════════════════════════════════════════════════════
        //  Events
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Raised whenever a broadside volley is fired.
        /// Arg: world-space position of the firing ship.
        /// </summary>
        public static event System.Action<Vector3> OnBroadsideFired;

        // ══════════════════════════════════════════════════════════════════
        //  Initialization
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Apply a cannon-upgrade damage multiplier.
        /// Called by ShipUpgradeManager when a cannon upgrade is purchased.
        /// </summary>
        /// <param name="multiplier">e.g. 1.25 = +25% damage.</param>
        public void SetDamageBonusMultiplier(float multiplier)
        {
            _damageBonusMultiplier = Mathf.Max(1f, multiplier);
        }

        /// <summary>
        /// Wire the owning ShipController. Called by BootyBootstrap.
        /// </summary>
        public void Initialize(Ships.ShipController ship)
        {
            _ship = ship;
        }

        private void Awake()
        {
            // Try self-wire if not explicitly initialised
            if (_ship == null)
            {
                _ship = GetComponent<Ships.ShipController>();
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Update
        // ══════════════════════════════════════════════════════════════════

        private void Update()
        {
            // Tick cooldowns
            if (_portCooldownTimer > 0f)
                _portCooldownTimer -= Time.deltaTime;
            if (_starboardCooldownTimer > 0f)
                _starboardCooldownTimer -= Time.deltaTime;

            // Player input: Q = port broadside, E = starboard broadside
            if (_ship != null && _ship.IsPlayerControlled)
            {
                if (Input.GetKeyDown(KeyCode.Q))
                {
                    FirePort();
                }
                if (Input.GetKeyDown(KeyCode.E))
                {
                    FireStarboard();
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Public Firing API
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Fire the port (left) broadside volley if off cooldown.
        /// </summary>
        /// <returns>True if the volley was fired.</returns>
        public bool FirePort()
        {
            if (!PortReady) return false;
            SpawnVolley(_ship.Port);
            _portCooldownTimer = cooldown;
            return true;
        }

        /// <summary>
        /// Fire the starboard (right) broadside volley if off cooldown.
        /// </summary>
        /// <returns>True if the volley was fired.</returns>
        public bool FireStarboard()
        {
            if (!StarboardReady) return false;
            SpawnVolley(_ship.Starboard);
            _starboardCooldownTimer = cooldown;
            return true;
        }

        /// <summary>
        /// Check whether a world position is within the port broadside arc.
        /// </summary>
        public bool IsInPortArc(Vector3 targetPosition)
        {
            return IsInArc(targetPosition, _ship.Port);
        }

        /// <summary>
        /// Check whether a world position is within the starboard broadside arc.
        /// </summary>
        public bool IsInStarboardArc(Vector3 targetPosition)
        {
            return IsInArc(targetPosition, _ship.Starboard);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Internal
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Check if a target is within a given broadside cone (direction + halfAngle)
        /// and within firing range.
        /// </summary>
        private bool IsInArc(Vector3 targetPosition, Vector3 arcDirection)
        {
            if (_ship == null) return false;

            Vector3 toTarget = targetPosition - transform.position;
            toTarget.y = 0f; // XZ plane

            float distance = toTarget.magnitude;
            if (distance > firingRange) return false;
            if (distance < 0.1f) return false;

            float angle = Vector3.Angle(arcDirection, toTarget);
            return angle <= halfAngle;
        }

        /// <summary>
        /// Spawn a volley of projectiles along the given broadside direction,
        /// with a small angular spread.
        /// </summary>
        private void SpawnVolley(Vector3 baseDirection)
        {
            baseDirection.y = 0f;
            baseDirection.Normalize();

            // Offset spawn position slightly to the side of the ship
            Vector3 spawnOrigin = transform.position + baseDirection * 1.5f;
            spawnOrigin.y = 0.5f;

            for (int i = 0; i < projectileCount; i++)
            {
                // Spread projectiles across the volley arc
                float t = projectileCount > 1
                    ? (float)i / (projectileCount - 1) - 0.5f  // -0.5 to +0.5
                    : 0f;

                float angleOffset = t * spreadAngle;
                Vector3 dir = Quaternion.Euler(0f, angleOffset, 0f) * baseDirection;

                int effectiveDamage = Mathf.RoundToInt(damage * _damageBonusMultiplier);
                Projectile.Spawn(spawnOrigin, dir, effectiveDamage, gameObject);
            }

            OnBroadsideFired?.Invoke(transform.position);
        }
    }
}
