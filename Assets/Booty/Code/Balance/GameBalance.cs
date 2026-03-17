// ---------------------------------------------------------------------------
// GameBalance.cs — ScriptableObject: single source of truth for all tunable values
// ---------------------------------------------------------------------------
// Create an asset via the Unity menu:
//   Assets → Create → Booty → GameBalance
//
// Assign the asset to DifficultyManager in the scene.
// DifficultyManager creates a runtime copy and applies preset multipliers.
//
// Values here represent Normal (baseline) difficulty.
// ---------------------------------------------------------------------------

using UnityEngine;

namespace Booty.Balance
{
    /// <summary>
    /// ScriptableObject containing every tunable gameplay parameter.
    /// This is the canonical source of truth for all balance values.
    /// DifficultyManager copies and modifies this at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "GameBalance", menuName = "Booty/GameBalance")]
    public class GameBalance : ScriptableObject
    {
        // ══════════════════════════════════════════════════════════════════
        //  Player Ship Movement
        // ══════════════════════════════════════════════════════════════════

        [Header("Player Ship — Movement")]
        [Tooltip("Maximum forward speed (world units / sec).")]
        public float playerMaxSpeed     = 12f;

        [Tooltip("Forward acceleration rate (units/sec²).")]
        public float playerAcceleration = 6f;

        [Tooltip("Deceleration rate when throttle is released (units/sec²).")]
        public float playerDeceleration = 4f;

        [Tooltip("Turn rate (degrees / sec) at full speed.")]
        public float playerTurnRate     = 90f;

        // ══════════════════════════════════════════════════════════════════
        //  Enemy Ship Movement
        // ══════════════════════════════════════════════════════════════════

        [Header("Enemy Ship — Movement")]
        [Tooltip("Maximum forward speed for enemy ships (world units / sec).")]
        public float enemyMaxSpeed     = 9f;

        [Tooltip("Forward acceleration rate for enemy ships (units/sec²).")]
        public float enemyAcceleration = 4f;

        [Tooltip("Deceleration rate for enemy ships (units/sec²).")]
        public float enemyDeceleration = 3f;

        [Tooltip("Turn rate for enemy ships (degrees / sec).")]
        public float enemyTurnRate     = 70f;

        // ══════════════════════════════════════════════════════════════════
        //  Cannon / Combat
        // ══════════════════════════════════════════════════════════════════

        [Header("Cannon — Combat")]
        [Tooltip("Damage per cannonball hit on hull.")]
        public int cannonDamage = 8;

        [Tooltip("Maximum broadside firing range (world units).")]
        public float firingRange = 25f;

        [Tooltip("Half-angle of the broadside firing cone (degrees).")]
        public float broadsideHalfAngle = 45f;

        [Tooltip("Cooldown between consecutive broadside volleys (seconds).")]
        public float fireCooldown = 2.0f;

        [Tooltip("Number of projectiles spawned per broadside volley.")]
        public int projectilesPerVolley = 2;

        [Tooltip("Angular spread for the volley (degrees total arc).")]
        public float volleySpreadAngle = 10f;

        [Tooltip("Projectile travel speed (world units / sec).")]
        public float projectileSpeed = 40f;

        // ══════════════════════════════════════════════════════════════════
        //  Hull HP
        // ══════════════════════════════════════════════════════════════════

        [Header("Hull HP")]
        [Tooltip("Maximum hull HP for the player ship.")]
        public int playerMaxHP = 150;

        [Tooltip("Base hull HP for a tier-1 enemy ship.")]
        public int enemyBaseHP = 100;

        [Tooltip("Additional HP added per tier above 1 (tier2 = base+bonus, tier3 = base+2×bonus).")]
        public int enemyHPPerTier = 20;

        // ══════════════════════════════════════════════════════════════════
        //  Economy — Starting Gold & Combat Rewards
        // ══════════════════════════════════════════════════════════════════

        [Header("Economy — Gold")]
        [Tooltip("Starting gold when no save data exists.")]
        public float startingGold = 300f;

        [Tooltip("Base gold awarded for sinking a tier-1 enemy ship.")]
        public float baseCombatReward = 80f;

        [Tooltip("Additional gold awarded per tier above 1.")]
        public float combatRewardPerTier = 35f;

        [Tooltip("Base gold shown in loot popup for tier-1 ships.")]
        public int baseLootPopupGold = 50;

        [Tooltip("Additional gold shown in loot popup per tier above 1.")]
        public int lootPopupGoldPerTier = 25;

        // ══════════════════════════════════════════════════════════════════
        //  Economy — Port Income
        // ══════════════════════════════════════════════════════════════════

        [Header("Economy — Port Income")]
        [Tooltip("Seconds between income ticks from owned ports.")]
        public float incomeIntervalSeconds = 60f;

        [Tooltip("Multiplier applied to all port income (e.g. 1.0 = normal, 0.5 = half).")]
        public float globalIncomeScalar = 1.0f;

        // ══════════════════════════════════════════════════════════════════
        //  Repair Shop
        // ══════════════════════════════════════════════════════════════════

        [Header("Repair Shop")]
        [Tooltip("Gold cost per 1 hull HP repaired.")]
        public float repairCostPerHP = 1.0f;

        [Tooltip("Multiplier applied to total repair cost.")]
        public float repairCostScalar = 1.0f;

        [Tooltip("Minimum gold cost for any repair (floor).")]
        public float minimumRepairCost = 15f;

        // ══════════════════════════════════════════════════════════════════
        //  Trade Prices (future expansion — placeholder values)
        // ══════════════════════════════════════════════════════════════════

        [Header("Trade Prices")]
        [Tooltip("Base purchase price for trade goods (future use).")]
        public float tradeBaseBuyPrice = 100f;

        [Tooltip("Base sell price for trade goods (future use).")]
        public float tradeBaseSellPrice = 130f;

        // ══════════════════════════════════════════════════════════════════
        //  Crew
        // ══════════════════════════════════════════════════════════════════

        [Header("Crew Costs")]
        [Tooltip("Gold cost to hire one crew member (future use).")]
        public float crewHireCost = 50f;

        [Tooltip("Gold per crew member per income tick as upkeep (future use).")]
        public float crewUpkeepPerTick = 10f;

        // ══════════════════════════════════════════════════════════════════
        //  Enemy Spawner
        // ══════════════════════════════════════════════════════════════════

        [Header("Enemy Spawner")]
        [Tooltip("Seconds between enemy spawn attempts.")]
        public float spawnIntervalSeconds = 45f;

        [Tooltip("Maximum enemies allowed near a single port.")]
        public int maxEnemiesPerPort = 3;

        [Tooltip("Maximum total active enemy ships in the world.")]
        public int maxTotalEnemies = 10;

        [Tooltip("Radius around enemy ports in which enemies spawn.")]
        public float spawnRadiusFromPort = 30f;

        [Tooltip("Enemies won't spawn within this distance of the player.")]
        public float minimumDistanceFromPlayer = 40f;

        // ══════════════════════════════════════════════════════════════════
        //  Renown Rewards
        // ══════════════════════════════════════════════════════════════════

        [Header("Renown")]
        [Tooltip("Base renown awarded for a tier-1 enemy kill.")]
        public float renownPerKill = 5f;

        [Tooltip("Additional renown per tier above 1.")]
        public float renownPerKillTierBonus = 3f;

        [Tooltip("Renown awarded for capturing a player port.")]
        public float renownPerPortCapture = 25f;

        // ══════════════════════════════════════════════════════════════════
        //  Enemy AI Distances
        // ══════════════════════════════════════════════════════════════════

        [Header("Enemy AI")]
        [Tooltip("Distance at which an enemy detects and aggros the player.")]
        public float aggroDistance = 45f;

        [Tooltip("Ideal combat range AI tries to maintain for broadside passes.")]
        public float preferredCombatRange = 18f;

        [Tooltip("Patrol waypoint radius around spawn point.")]
        public float patrolRadius = 30f;
    }
}
