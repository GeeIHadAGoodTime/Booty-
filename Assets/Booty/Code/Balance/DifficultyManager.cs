// ---------------------------------------------------------------------------
// DifficultyManager.cs — Easy / Normal / Hard difficulty presets
// ---------------------------------------------------------------------------
// Attach to a GameObject in World_Main.unity (or let BootyBootstrap create it).
// Assign a GameBalance ScriptableObject asset in the Inspector.
//
// At Awake() the manager clones the base balance and applies the chosen
// preset multipliers, producing ActiveBalance — the runtime copy every other
// system should read via DifficultyManager.Instance.ActiveBalance.
//
// Call SetDifficulty() at any time to swap presets at runtime (e.g. from
// a settings menu or the debug console).
// ---------------------------------------------------------------------------

using UnityEngine;

namespace Booty.Balance
{
    /// <summary>
    /// Difficulty presets applied on top of the base GameBalance values.
    /// </summary>
    public enum Difficulty
    {
        Easy,
        Normal,
        Hard,
    }

    /// <summary>
    /// Singleton MonoBehaviour. Holds the active <see cref="GameBalance"/>
    /// for the current session, modified by the chosen difficulty preset.
    /// </summary>
    public class DifficultyManager : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Inspector
        // ══════════════════════════════════════════════════════════════════

        [Header("Balance Asset")]
        [Tooltip("Assign the GameBalance ScriptableObject asset from Assets/Booty/Balance/.")]
        [SerializeField] private GameBalance baseBalance;

        [Header("Starting Difficulty")]
        [SerializeField] private Difficulty startingDifficulty = Difficulty.Normal;

        // ══════════════════════════════════════════════════════════════════
        //  Singleton
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Global singleton access to the difficulty manager.</summary>
        public static DifficultyManager Instance { get; private set; }

        // ══════════════════════════════════════════════════════════════════
        //  Public State
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Runtime copy of GameBalance with the current difficulty preset applied.
        /// All gameplay systems should read values from this, not from the base asset.
        /// </summary>
        public GameBalance ActiveBalance { get; private set; }

        /// <summary>The currently active difficulty preset.</summary>
        public Difficulty CurrentDifficulty { get; private set; }

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Awake()
        {
            // Singleton enforcement
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Bootstrap with a fallback if no asset is assigned
            if (baseBalance == null)
            {
                Debug.LogWarning("[DifficultyManager] No GameBalance asset assigned — " +
                                 "using default values. Assign one in the Inspector.");
                baseBalance = ScriptableObject.CreateInstance<GameBalance>();
            }

            ApplyDifficulty(startingDifficulty);
            Debug.Log($"[DifficultyManager] Initialized. Difficulty: {CurrentDifficulty}");
        }

        // ══════════════════════════════════════════════════════════════════
        //  Public API
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Initialize the manager with a specific balance asset.
        /// Call this from BootyBootstrap when creating DifficultyManager programmatically.
        /// </summary>
        /// <param name="balance">The GameBalance asset to use as the base.</param>
        /// <param name="difficulty">Difficulty preset to apply immediately.</param>
        public void Initialize(GameBalance balance, Difficulty difficulty = Difficulty.Normal)
        {
            if (balance != null)
                baseBalance = balance;
            else if (baseBalance == null)
                baseBalance = ScriptableObject.CreateInstance<GameBalance>();
            ApplyDifficulty(difficulty);
            Debug.Log($"[DifficultyManager] Initialized via code. Difficulty: {CurrentDifficulty}");
        }

        /// <summary>
        /// Switch to a new difficulty preset. Creates a fresh runtime copy and
        /// applies the new multipliers. Existing system references to ActiveBalance
        /// will see stale data — call <see cref="ConfigureAllSystems"/> if needed.
        /// </summary>
        /// <param name="difficulty">The new difficulty to apply.</param>
        public void SetDifficulty(Difficulty difficulty)
        {
            ApplyDifficulty(difficulty);
            Debug.Log($"[DifficultyManager] Difficulty changed to: {CurrentDifficulty}");
        }

        // ══════════════════════════════════════════════════════════════════
        //  Preset Application
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Clone the base balance and apply the named preset multipliers.
        /// </summary>
        private void ApplyDifficulty(Difficulty difficulty)
        {
            CurrentDifficulty = difficulty;

            // Always start from a fresh clone of the base values
            ActiveBalance = Instantiate(baseBalance);
            ActiveBalance.name = $"GameBalance_Runtime_{difficulty}";

            switch (difficulty)
            {
                case Difficulty.Easy:
                    ApplyEasyPreset();
                    break;

                case Difficulty.Hard:
                    ApplyHardPreset();
                    break;

                case Difficulty.Normal:
                default:
                    // Normal = base values, no changes needed
                    break;
            }
        }

        /// <summary>
        /// Easy preset: player buffed, enemies nerfed, economy generous.
        /// Ideal for first-time players or casual sessions.
        /// </summary>
        private void ApplyEasyPreset()
        {
            var b = ActiveBalance;
            var base_ = baseBalance;

            // ── Player buffs ─────────────────────────────────────────────
            b.playerMaxHP      = Mathf.RoundToInt(base_.playerMaxHP * 1.5f);   // +50% hull
            b.cannonDamage     = Mathf.RoundToInt(base_.cannonDamage * 1.25f); // +25% damage
            b.fireCooldown     = base_.fireCooldown * 0.75f;                   // 25% faster refire

            // ── Enemy nerfs ───────────────────────────────────────────────
            b.enemyBaseHP      = Mathf.RoundToInt(base_.enemyBaseHP * 0.7f);   // -30% enemy hull
            b.enemyHPPerTier   = Mathf.RoundToInt(base_.enemyHPPerTier * 0.7f);
            b.enemyMaxSpeed    = base_.enemyMaxSpeed * 0.85f;                  // slightly slower
            b.aggroDistance    = base_.aggroDistance * 0.8f;                   // shorter leash

            // ── Economy buffs ─────────────────────────────────────────────
            b.baseCombatReward    = base_.baseCombatReward * 1.5f;             // +50% kill gold (legacy)
            b.combatRewardPerTier = base_.combatRewardPerTier * 1.5f;
            b.goldKillTier1       = base_.goldKillTier1 * 1.5f;               // +50% tier-specific kill gold
            b.goldKillTier2       = base_.goldKillTier2 * 1.5f;
            b.goldKillTier3       = base_.goldKillTier3 * 1.5f;
            b.goldKillTier4       = base_.goldKillTier4 * 1.5f;
            b.goldKillTier5       = base_.goldKillTier5 * 1.5f;
            b.startingGold        = base_.startingGold * 1.5f;                 // extra starting gold
            b.repairCostScalar    = base_.repairCostScalar * 0.5f;             // 50% cheaper repairs

            // ── Spawn nerfs ───────────────────────────────────────────────
            b.spawnIntervalSeconds       = base_.spawnIntervalSeconds * 1.5f;  // 50% slower spawns
            b.maxTotalEnemies            = Mathf.RoundToInt(base_.maxTotalEnemies * 0.7f);
            b.minimumDistanceFromPlayer  = base_.minimumDistanceFromPlayer * 1.25f; // safer distance
        }

        /// <summary>
        /// Hard preset: player nerfed, enemies buffed, economy tight.
        /// For veterans who want a real challenge.
        /// </summary>
        private void ApplyHardPreset()
        {
            var b = ActiveBalance;
            var base_ = baseBalance;

            // ── Player nerfs ──────────────────────────────────────────────
            b.playerMaxHP      = Mathf.RoundToInt(base_.playerMaxHP * 0.7f);   // -30% hull
            b.cannonDamage     = Mathf.RoundToInt(base_.cannonDamage * 0.8f);  // -20% damage
            b.fireCooldown     = base_.fireCooldown * 1.3f;                    // 30% slower refire

            // ── Enemy buffs ───────────────────────────────────────────────
            b.enemyBaseHP      = Mathf.RoundToInt(base_.enemyBaseHP * 1.5f);   // +50% enemy hull
            b.enemyHPPerTier   = Mathf.RoundToInt(base_.enemyHPPerTier * 1.5f);
            b.enemyMaxSpeed    = base_.enemyMaxSpeed * 1.25f;                  // 25% faster enemies
            b.enemyTurnRate    = base_.enemyTurnRate * 1.2f;                   // more agile AI
            b.aggroDistance    = base_.aggroDistance * 1.3f;                   // enemies spot you earlier
            b.cannonDamage     = Mathf.RoundToInt(base_.cannonDamage * 0.8f);  // player deals less

            // ── Economy nerfs ─────────────────────────────────────────────
            b.baseCombatReward    = base_.baseCombatReward * 0.75f;            // -25% kill gold (legacy)
            b.combatRewardPerTier = base_.combatRewardPerTier * 0.75f;
            b.goldKillTier1       = base_.goldKillTier1 * 0.75f;              // -25% tier-specific kill gold
            b.goldKillTier2       = base_.goldKillTier2 * 0.75f;
            b.goldKillTier3       = base_.goldKillTier3 * 0.75f;
            b.goldKillTier4       = base_.goldKillTier4 * 0.75f;
            b.goldKillTier5       = base_.goldKillTier5 * 0.75f;
            b.startingGold        = base_.startingGold * 0.7f;                 // tighter start
            b.repairCostScalar    = base_.repairCostScalar * 2.0f;             // double repair costs

            // ── Spawn buffs ───────────────────────────────────────────────
            b.spawnIntervalSeconds = base_.spawnIntervalSeconds * 0.6f;        // 40% faster spawns
            b.maxTotalEnemies      = Mathf.RoundToInt(base_.maxTotalEnemies * 1.5f);
            b.maxEnemiesPerPort    = Mathf.RoundToInt(base_.maxEnemiesPerPort * 1.5f);
        }
    }
}
