// ---------------------------------------------------------------------------
// BalanceConfig.cs — Static compile-time balance constants
// ---------------------------------------------------------------------------
// Single source of truth for all game balance numbers.
// Tuned for 30-60 minute sessions per PRD target.
// GameBalance ScriptableObject uses these as its default values.
// ---------------------------------------------------------------------------

using UnityEngine;

namespace Booty.Config
{
    /// <summary>
    /// Static configuration class providing compile-time balance constants.
    /// All values are tuned for 30-60 minute play sessions.
    /// </summary>
    public static class BalanceConfig
    {
        // ══════════════════════════════════════════════════════════════════
        //  Ship Speeds Per Tier (world units / sec)
        // ══════════════════════════════════════════════════════════════════

        // Tier 1 — Sloop (light, fast)
        public const float SloopMaxSpeed      = 14f;
        public const float SloopTurnRate      = 100f;  // degrees/sec
        public const float SloopAcceleration  = 7f;

        // Tier 2 — Brig (balanced)
        public const float BrigMaxSpeed      = 11f;
        public const float BrigTurnRate      = 80f;
        public const float BrigAcceleration  = 5f;

        // Tier 3 — Frigate (heavy, slow)
        public const float FrigateMaxSpeed      = 8f;
        public const float FrigateTurnRate      = 60f;
        public const float FrigateAcceleration  = 3.5f;

        // Player ship (starts as Sloop)
        public const float PlayerMaxSpeed      = SloopMaxSpeed;
        public const float PlayerTurnRate      = SloopTurnRate;
        public const float PlayerAcceleration  = SloopAcceleration;

        // Enemy ships (slightly slower than player for fair feel)
        public const float EnemyMaxSpeed      = 9f;
        public const float EnemyTurnRate      = 70f;
        public const float EnemyAcceleration  = 4f;

        // ══════════════════════════════════════════════════════════════════
        //  Broadside Damage / Range / Reload Per Tier
        // ══════════════════════════════════════════════════════════════════

        // Tier 1 — Basic cannons
        public const int   CannonT1Damage      = 8;
        public const float CannonT1Range       = 25f;
        public const float CannonT1Reload      = 2.0f;  // seconds
        public const int   CannonT1Projectiles = 2;

        // Tier 2 — Improved cannons
        public const int   CannonT2Damage      = 12;
        public const float CannonT2Range       = 30f;
        public const float CannonT2Reload      = 1.5f;
        public const int   CannonT2Projectiles = 3;

        // Tier 3 — Heavy cannons
        public const int   CannonT3Damage      = 18;
        public const float CannonT3Range       = 35f;
        public const float CannonT3Reload      = 1.2f;
        public const int   CannonT3Projectiles = 4;

        // Default (T1 baseline for new games)
        public const int   DefaultCannonDamage          = CannonT1Damage;
        public const float DefaultFiringRange           = CannonT1Range;
        public const float DefaultFireCooldown          = CannonT1Reload;
        public const int   DefaultProjectilesPerVolley  = CannonT1Projectiles;
        public const float DefaultBroadsideHalfAngle    = 45f;  // degrees
        public const float DefaultVolleySpreadAngle     = 10f;   // degrees
        public const float DefaultProjectileSpeed       = 40f;   // units/sec

        // ══════════════════════════════════════════════════════════════════
        //  Hull HP Per Tier
        // ══════════════════════════════════════════════════════════════════

        public const int PlayerMaxHP   = 150;
        public const int EnemyHPTier1  = 100;
        public const int EnemyHPTier2  = 120;  // T1 + 20
        public const int EnemyHPTier3  = 140;  // T1 + 40
        public const int EnemyHPPerTier = 20;   // HP bonus per tier above 1

        // ══════════════════════════════════════════════════════════════════
        //  Combat Gold Rewards
        // ══════════════════════════════════════════════════════════════════

        // ~80g per kill T1, scales with tier for ~30-60 min to amass fleet
        public const float BaseCombatReward    = 80f;   // for tier-1 kill
        public const float CombatRewardPerTier = 35f;  // additional per tier
        public const float StartingGold        = 300f;  // enough for 1-2 repairs

        // Loot popup display (visual only, does NOT duplicate AddGold)
        public const int BaseLootPopupGold    = 50;
        public const int LootPopupGoldPerTier = 25;

        // ══════════════════════════════════════════════════════════════════
        //  Repair Costs
        // ══════════════════════════════════════════════════════════════════

        // Full repair of 150 HP ship costs ~150g at T1 cannon rate (~2 kills)
        public const float RepairCostPerHP   = 1.0f;    // gold per 1 HP
        public const float RepairCostScalar  = 1.0f;    // global multiplier
        public const float MinimumRepairCost = 15f;     // floor to avoid trivial repairs

        // ══════════════════════════════════════════════════════════════════
        //  Port Income Rates
        // ══════════════════════════════════════════════════════════════════

        // 1 port = 50g/min → 1500g/30min → can afford 10 full repairs
        // Designed so ports matter but combat remains primary income early
        public const float IncomeIntervalSeconds = 60f;  // tick every 60 seconds
        public const float GlobalIncomeScalar    = 1.0f; // normal difficulty

        // ══════════════════════════════════════════════════════════════════
        //  Enemy Scaling Curve
        // ══════════════════════════════════════════════════════════════════

        // Minutes 0-10: only tier-1 enemies (tutorial feel)
        // Minutes 10-25: mix of tier-1 and tier-2
        // Minutes 25+: tier-3 present; player should have upgrades by now
        public const float EnemyTier2UnlockMinutes = 10f;
        public const float EnemyTier3UnlockMinutes = 25f;

        // Spawn settings (target: world always feels inhabited)
        public const float SpawnIntervalSeconds  = 45f;
        public const int   MaxEnemiesPerPort     = 3;
        public const int   MaxTotalEnemies       = 10;
        public const float SpawnRadiusFromPort   = 30f;
        public const float MinDistanceFromPlayer = 40f;

        // ══════════════════════════════════════════════════════════════════
        //  Renown Rewards
        // ══════════════════════════════════════════════════════════════════

        public const float RenownPerKill          = 5f;
        public const float RenownPerKillTierBonus = 3f;
        public const float RenownPerPortCapture   = 25f;

        // ══════════════════════════════════════════════════════════════════
        //  Enemy AI Distances
        // ══════════════════════════════════════════════════════════════════

        public const float AggroDistance        = 45f;  // units
        public const float PreferredCombatRange = 18f;  // units (inside T1 range of 25)
        public const float PatrolRadius         = 30f;  // units around spawn point

        // ══════════════════════════════════════════════════════════════════
        //  Session Pacing Summary (comment block)
        // ══════════════════════════════════════════════════════════════════
        //
        // 30-min session target path:
        //   min 0-5:  Learn WASD + Q/E broadsides vs tier-1 enemies
        //   min 5-10: First port capture, ~400g accumulated (3-4 kills + start gold)
        //   min 10-20: Tier-2 enemies appear, buy hull upgrade, hold 2 ports
        //   min 20-30: Tier-3 enemies, upgrade cannons, hold 3-4 ports
        //   Result: player controls Caribbean, has 1000-1500g, all quests complete
        //
        // 60-min session follows same curve but with more exploration and trade runs.

        // ══════════════════════════════════════════════════════════════════
        //  Helper: HP for a given enemy tier
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Returns base HP for an enemy of the given tier (1-based).</summary>
        public static int EnemyHPForTier(int tier)
        {
            return EnemyHPTier1 + Mathf.Max(0, tier - 1) * EnemyHPPerTier;
        }

        /// <summary>Returns combat gold reward for defeating an enemy of the given tier.</summary>
        public static float CombatRewardForTier(int tier)
        {
            return BaseCombatReward + Mathf.Max(0, tier - 1) * CombatRewardPerTier;
        }
    }
}
