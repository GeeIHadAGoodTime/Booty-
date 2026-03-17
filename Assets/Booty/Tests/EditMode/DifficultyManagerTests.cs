// ---------------------------------------------------------------------------
// DifficultyManagerTests.cs — EditMode tests for GameBalance + DifficultyManager
// ---------------------------------------------------------------------------
// Validates that:
//   1. GameBalance ScriptableObject can be created with default values.
//   2. DifficultyManager.Initialize() populates ActiveBalance correctly.
//   3. Easy preset buffs player and nerfs enemies.
//   4. Hard preset nerfs player and buffs enemies.
//   5. Normal preset leaves values unchanged from base.
// ---------------------------------------------------------------------------

using NUnit.Framework;
using UnityEngine;
using Booty.Balance;

namespace Booty.Tests
{
    /// <summary>
    /// Smoke tests for <see cref="GameBalance"/> and <see cref="DifficultyManager"/>.
    /// All tests run in EditMode — no scene required.
    /// </summary>
    [TestFixture]
    public class DifficultyManagerTests
    {
        // ── Helpers ──────────────────────────────────────────────────────────

        private static GameBalance MakeBalance()
        {
            var b = ScriptableObject.CreateInstance<GameBalance>();
            // Use known values for predictable assertions
            b.playerMaxHP         = 150;
            b.enemyBaseHP         = 100;
            b.cannonDamage        = 8;
            b.baseCombatReward    = 80f;
            b.repairCostScalar    = 1.0f;
            b.spawnIntervalSeconds = 45f;
            b.maxTotalEnemies     = 10;
            return b;
        }

        private static DifficultyManager MakeManager(GameBalance balance, Difficulty difficulty)
        {
            var go      = new GameObject("TestDifficultyManager");
            var manager = go.AddComponent<DifficultyManager>();
            manager.Initialize(balance, difficulty);
            return manager;
        }

        // ── Test 1 ───────────────────────────────────────────────────────────
        /// <summary>
        /// GameBalance ScriptableObject can be instantiated and has non-zero defaults.
        /// </summary>
        [Test]
        public void GameBalance_CreateInstance_HasNonZeroDefaults()
        {
            var balance = ScriptableObject.CreateInstance<GameBalance>();

            Assert.Greater(balance.playerMaxHP,         0, "playerMaxHP must be positive.");
            Assert.Greater(balance.enemyBaseHP,         0, "enemyBaseHP must be positive.");
            Assert.Greater(balance.cannonDamage,        0, "cannonDamage must be positive.");
            Assert.Greater(balance.baseCombatReward,    0f, "baseCombatReward must be positive.");
            Assert.Greater(balance.incomeIntervalSeconds, 0f, "incomeIntervalSeconds must be positive.");

            Object.DestroyImmediate(balance);
        }

        // ── Test 2 ───────────────────────────────────────────────────────────
        /// <summary>
        /// Normal difficulty: ActiveBalance values equal the base balance values.
        /// </summary>
        [Test]
        public void DifficultyManager_Normal_ActiveBalanceMatchesBase()
        {
            var baseBalance = MakeBalance();
            var manager     = MakeManager(baseBalance, Difficulty.Normal);

            Assert.AreEqual(baseBalance.playerMaxHP,      manager.ActiveBalance.playerMaxHP,
                "Normal difficulty must not change playerMaxHP.");
            Assert.AreEqual(baseBalance.enemyBaseHP,      manager.ActiveBalance.enemyBaseHP,
                "Normal difficulty must not change enemyBaseHP.");
            Assert.AreEqual(baseBalance.cannonDamage,     manager.ActiveBalance.cannonDamage,
                "Normal difficulty must not change cannonDamage.");
            Assert.AreEqual(baseBalance.baseCombatReward, manager.ActiveBalance.baseCombatReward,
                "Normal difficulty must not change baseCombatReward.");

            Object.DestroyImmediate(manager.gameObject);
            Object.DestroyImmediate(baseBalance);
        }

        // ── Test 3 ───────────────────────────────────────────────────────────
        /// <summary>
        /// Easy difficulty: player HP is greater than base; enemy HP is less than base.
        /// </summary>
        [Test]
        public void DifficultyManager_Easy_BuffsPlayerNerfsEnemies()
        {
            var baseBalance = MakeBalance();
            var manager     = MakeManager(baseBalance, Difficulty.Easy);

            Assert.Greater(manager.ActiveBalance.playerMaxHP, baseBalance.playerMaxHP,
                "Easy must increase playerMaxHP above base.");
            Assert.Less(manager.ActiveBalance.enemyBaseHP, baseBalance.enemyBaseHP,
                "Easy must decrease enemyBaseHP below base.");
            Assert.Greater(manager.ActiveBalance.baseCombatReward, baseBalance.baseCombatReward,
                "Easy must increase baseCombatReward above base.");
            Assert.Less(manager.ActiveBalance.repairCostScalar, baseBalance.repairCostScalar,
                "Easy must decrease repairCostScalar (cheaper repairs).");

            Object.DestroyImmediate(manager.gameObject);
            Object.DestroyImmediate(baseBalance);
        }

        // ── Test 4 ───────────────────────────────────────────────────────────
        /// <summary>
        /// Hard difficulty: player HP is less than base; enemy HP is greater than base.
        /// </summary>
        [Test]
        public void DifficultyManager_Hard_NerfsPlayerBuffsEnemies()
        {
            var baseBalance = MakeBalance();
            var manager     = MakeManager(baseBalance, Difficulty.Hard);

            Assert.Less(manager.ActiveBalance.playerMaxHP, baseBalance.playerMaxHP,
                "Hard must decrease playerMaxHP below base.");
            Assert.Greater(manager.ActiveBalance.enemyBaseHP, baseBalance.enemyBaseHP,
                "Hard must increase enemyBaseHP above base.");
            Assert.Less(manager.ActiveBalance.baseCombatReward, baseBalance.baseCombatReward,
                "Hard must decrease baseCombatReward (less loot).");
            Assert.Greater(manager.ActiveBalance.repairCostScalar, baseBalance.repairCostScalar,
                "Hard must increase repairCostScalar (more expensive repairs).");

            Object.DestroyImmediate(manager.gameObject);
            Object.DestroyImmediate(baseBalance);
        }

        // ── Test 5 ───────────────────────────────────────────────────────────
        /// <summary>
        /// ActiveBalance is a separate copy — modifying it does not affect the base.
        /// </summary>
        [Test]
        public void DifficultyManager_ActiveBalance_IsIndependentCopy()
        {
            var baseBalance = MakeBalance();
            var manager     = MakeManager(baseBalance, Difficulty.Normal);

            // Mutate the active copy
            manager.ActiveBalance.playerMaxHP = 9999;

            // Base must be unchanged
            Assert.AreEqual(150, baseBalance.playerMaxHP,
                "Mutating ActiveBalance must not affect the base ScriptableObject.");

            Object.DestroyImmediate(manager.gameObject);
            Object.DestroyImmediate(baseBalance);
        }

        // ── Test 6 ───────────────────────────────────────────────────────────
        /// <summary>
        /// SetDifficulty() at runtime switches presets correctly.
        /// </summary>
        [Test]
        public void DifficultyManager_SetDifficulty_SwitchesPreset()
        {
            var baseBalance = MakeBalance();
            var manager     = MakeManager(baseBalance, Difficulty.Normal);

            // Start Normal
            Assert.AreEqual(Difficulty.Normal, manager.CurrentDifficulty);
            int normalHP = manager.ActiveBalance.playerMaxHP;

            // Switch to Easy
            manager.SetDifficulty(Difficulty.Easy);
            Assert.AreEqual(Difficulty.Easy, manager.CurrentDifficulty);
            Assert.Greater(manager.ActiveBalance.playerMaxHP, normalHP,
                "After switching to Easy, playerMaxHP must exceed Normal value.");

            // Switch to Hard
            manager.SetDifficulty(Difficulty.Hard);
            Assert.AreEqual(Difficulty.Hard, manager.CurrentDifficulty);
            Assert.Less(manager.ActiveBalance.playerMaxHP, normalHP,
                "After switching to Hard, playerMaxHP must be below Normal value.");

            Object.DestroyImmediate(manager.gameObject);
            Object.DestroyImmediate(baseBalance);
        }
    }
}
