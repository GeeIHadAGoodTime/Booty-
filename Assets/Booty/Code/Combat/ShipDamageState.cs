// ---------------------------------------------------------------------------
// ShipDamageState.cs — Hull and sail damage affect ship performance
// ---------------------------------------------------------------------------
// S3.1: Ship damage states with visual + gameplay effects.
//   - Hull damage  → speed reduction (30% at critical HP)
//   - Sail damage  → turn-rate reduction (40% at critical HP)
//
// Attached to every ship (player and enemy). Hooks into HPSystem.OnDamaged
// and calls ShipController.SetDamageMultipliers() after each hit.
//
// Damage split: each incoming hit is randomly assigned to either hull or
// sails (60% hull / 40% sail). The damage pools grow with total HP loss so
// the debuff naturally tracks the ship's overall health.
// ---------------------------------------------------------------------------

using UnityEngine;
using Booty.Ships;

namespace Booty.Combat
{
    /// <summary>
    /// Tracks hull and sail damage separately and applies performance
    /// penalties to the owning <see cref="ShipController"/>.
    /// </summary>
    public class ShipDamageState : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Configuration
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Speed multiplier at 100% hull damage (0.1 = 90% slower).</summary>
        private const float MinSpeedMultiplier = 0.30f;

        /// <summary>Turn-rate multiplier at 100% sail damage.</summary>
        private const float MinTurnMultiplier = 0.25f;

        /// <summary>Probability that a hit damages the hull (vs. sails).</summary>
        private const float HullHitChance = 0.60f;

        // ══════════════════════════════════════════════════════════════════
        //  State (0-1 normalized, grows as ship takes damage)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Accumulated hull damage ratio (0 = pristine, 1 = destroyed).</summary>
        public float HullDamageRatio { get; private set; }

        /// <summary>Accumulated sail damage ratio (0 = pristine, 1 = torn away).</summary>
        public float SailDamageRatio { get; private set; }

        // ══════════════════════════════════════════════════════════════════
        //  References
        // ══════════════════════════════════════════════════════════════════

        private HPSystem       _hp;
        private ShipController _ship;

        // ══════════════════════════════════════════════════════════════════
        //  Public API
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Wire this damage-state component to its HP and movement systems.
        /// Call once from BootyBootstrap after all components are added.
        /// </summary>
        public void Initialize(HPSystem hp, ShipController ship)
        {
            _hp   = hp;
            _ship = ship;

            HullDamageRatio = 0f;
            SailDamageRatio = 0f;

            if (_hp != null)
                _hp.OnDamaged += OnDamaged;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Start()
        {
            // Self-wire only when Initialize() was not called explicitly.
            if (_hp == null)
            {
                _hp   = GetComponent<HPSystem>();
                _ship = GetComponent<ShipController>();
                if (_hp != null) _hp.OnDamaged += OnDamaged;
            }
        }

        private void OnDestroy()
        {
            if (_hp != null)
                _hp.OnDamaged -= OnDamaged;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Damage Handling
        // ══════════════════════════════════════════════════════════════════

        private void OnDamaged(int currentHP, int maxHP)
        {
            if (maxHP <= 0) return;

            // Overall damage fraction based on current HP
            float overallDamageFraction = 1f - (float)currentHP / maxHP;

            // Randomly route each hit to hull or sails
            // Both pools grow with overall damage but hull grows faster for hull hits
            if (Random.value <= HullHitChance)
            {
                // Hull hit — push hull damage toward overall fraction
                HullDamageRatio = Mathf.Max(HullDamageRatio, overallDamageFraction);
            }
            else
            {
                // Sail hit — push sail damage toward overall fraction
                SailDamageRatio = Mathf.Max(SailDamageRatio, overallDamageFraction);
            }

            // Always nudge both pools a little (ships are interconnected)
            HullDamageRatio = Mathf.Max(HullDamageRatio, overallDamageFraction * 0.5f);
            SailDamageRatio = Mathf.Max(SailDamageRatio, overallDamageFraction * 0.4f);

            // Clamp to [0, 1]
            HullDamageRatio = Mathf.Clamp01(HullDamageRatio);
            SailDamageRatio = Mathf.Clamp01(SailDamageRatio);

            ApplyMultipliers();
        }

        private void ApplyMultipliers()
        {
            if (_ship == null) return;

            float speedMult = Mathf.Lerp(1f, MinSpeedMultiplier, HullDamageRatio);
            float turnMult  = Mathf.Lerp(1f, MinTurnMultiplier,  SailDamageRatio);

            _ship.SetDamageMultipliers(speedMult, turnMult);
        }
    }
}
