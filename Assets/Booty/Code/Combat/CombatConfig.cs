// ---------------------------------------------------------------------------
// CombatConfig.cs — Tuning constants for combat, AI, and projectiles
// ---------------------------------------------------------------------------
// Central location for all combat-related magic numbers.
// Exposed via static properties so any combat system can read them.
// Values can be overridden at startup from JSON config in later phases.
// ---------------------------------------------------------------------------

namespace Booty.Combat
{
    /// <summary>
    /// Static combat tuning constants for P1 vertical slice.
    /// Pure data — no MonoBehaviour, no singleton, no state.
    /// </summary>
    public static class CombatConfig
    {
        // ══════════════════════════════════════════════════════════════════
        //  Broadside / Weapons
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Damage per cannonball hit on hull.</summary>
        public const int BaseDamage = 10;

        /// <summary>Maximum broadside firing range (world units).</summary>
        public const float FiringRange = 25f;

        /// <summary>Half-angle of the broadside firing cone (degrees).</summary>
        public const float BroadsideHalfAngle = 45f;

        /// <summary>Cooldown between consecutive broadside volleys (seconds).</summary>
        public const float FireCooldown = 2.5f;

        /// <summary>Number of projectiles per volley.</summary>
        public const int ProjectilesPerVolley = 3;

        /// <summary>Angular spread for the volley (degrees, total arc).</summary>
        public const float VolleySpreadAngle = 10f;

        // ══════════════════════════════════════════════════════════════════
        //  Projectile
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Projectile travel speed (world units / sec).</summary>
        public const float ProjectileSpeed = 40f;

        /// <summary>Maximum projectile lifetime (seconds). Auto-destroy after this.</summary>
        public const float ProjectileLifetime = 3f;

        /// <summary>Projectile collision sphere radius.</summary>
        public const float ProjectileRadius = 0.3f;

        // ══════════════════════════════════════════════════════════════════
        //  HP / Ship
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Default hull HP for a player ship.</summary>
        public const int DefaultPlayerHP = 150;

        /// <summary>Default hull HP for a basic enemy ship.</summary>
        public const int DefaultEnemyHP = 80;

        // ══════════════════════════════════════════════════════════════════
        //  Enemy AI
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Distance at which an enemy detects and aggros the player.</summary>
        public const float AggroDistance = 45f;

        /// <summary>Distance at which the enemy disengages (if implemented).</summary>
        public const float DisengageDistance = 70f;

        /// <summary>Ideal distance AI tries to maintain for broadside passes.</summary>
        public const float PreferredCombatRange = 18f;

        /// <summary>AI patrol waypoint radius (random circle near spawn).</summary>
        public const float PatrolRadius = 30f;

        /// <summary>Time between AI patrol waypoint changes (seconds).</summary>
        public const float PatrolWaypointInterval = 8f;

        // ══════════════════════════════════════════════════════════════════
        //  Rewards
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Base gold reward for sinking an enemy ship.</summary>
        public const int GoldRewardPerKill = 50;

        /// <summary>Base renown reward for sinking an enemy ship.</summary>
        public const float RenownRewardPerKill = 5f;
    }
}
