// ---------------------------------------------------------------------------
// Test_TradeSystem.cs — EditMode tests for EconomySystem trade mechanics.
//
// "Buy low, sell high" in Booty! maps to:
//   Buy low  = earn combat spoils at Tier 1 (base reward — weakest enemies)
//   Sell high = earn combat spoils at Tier 2+ (scaled bonus — elite enemies)
//
// Inventory updates are tracked via the Gold balance (the single currency).
// ---------------------------------------------------------------------------

using NUnit.Framework;
using UnityEngine;
using Booty.Economy;

namespace Booty.Tests
{
    /// <summary>
    /// EditMode tests for trade/economy mechanics via <see cref="EconomySystem"/>.
    /// Covers: tier-scaled combat spoils, gold event, and buy-then-sell cycle.
    /// </summary>
    [TestFixture]
    public class Test_TradeSystem
    {
        // ── Test 1 ────────────────────────────────────────────────────────
        /// <summary>
        /// Sinking a Tier 1 enemy (Sloop) awards the tier-1 default reward.
        /// This is the "buy low" case — minimum return per transaction.
        /// Default (no GameBalance configured): goldKillTier1 = 10g.
        /// </summary>
        [Test]
        public void TradeSystem_BuyLow_Tier1CombatSpoils_MatchesBaseReward()
        {
            var go       = new GameObject("TestTrade_Tier1");
            var economy  = go.AddComponent<EconomySystem>();
            // Economy starts at 0 gold (no SaveSystem / PortSystem wired)

            float awarded = economy.AwardCombatSpoils(enemyTier: 1);

            // goldKillTier1 default = 10g (GameBalance single source of truth)
            Assert.AreEqual(10f, awarded,
                "Tier 1 spoils must equal goldKillTier1 default (10g).");
            Assert.AreEqual(10f, economy.Gold,
                "Gold balance must increase by the spoils amount.");

            Object.DestroyImmediate(go);
        }

        // ── Test 2 ────────────────────────────────────────────────────────
        /// <summary>
        /// Sinking a Tier 2 enemy (Brigantine) awards the tier-2 default reward.
        /// This is the "sell high" case — taking on tougher enemies yields more.
        /// Default (no GameBalance configured): goldKillTier2 = 25g.
        /// </summary>
        [Test]
        public void TradeSystem_SellHigh_Tier2CombatSpoils_IncludesScaledBonus()
        {
            var go      = new GameObject("TestTrade_Tier2");
            var economy = go.AddComponent<EconomySystem>();

            float awarded = economy.AwardCombatSpoils(enemyTier: 2);

            // goldKillTier2 default = 25g (GameBalance single source of truth)
            Assert.AreEqual(25f, awarded,
                "Tier 2 spoils must equal goldKillTier2 default (25g).");
            Assert.AreEqual(25f, economy.Gold,
                "Gold balance must reflect the tier-specific reward.");

            Object.DestroyImmediate(go);
        }

        // ── Test 3 ────────────────────────────────────────────────────────
        /// <summary>
        /// A full buy-then-sell cycle must correctly update the inventory (gold balance).
        /// Sequence: sink two Tier 4 Galleons (+200g) → spend 80g (buy supplies)
        ///           → sink one Tier 4 Galleon (+100g).
        /// Expected final balance: 200 - 80 + 100 = 220g.
        /// </summary>
        [Test]
        public void TradeSystem_BuySellCycle_InventoryUpdatesCorrectly()
        {
            var go      = new GameObject("TestTrade_Cycle");
            var economy = go.AddComponent<EconomySystem>();

            // Earn initial capital (sink two Tier 4 Galleons → 200g)
            economy.AwardCombatSpoils(enemyTier: 4); // +100 → 100
            economy.AwardCombatSpoils(enemyTier: 4); // +100 → 200

            // Buy supplies / invest (spend 80 gold)
            bool spent = economy.SpendGold(80f);

            // Earn return by sinking another Tier 4 Galleon
            economy.AwardCombatSpoils(enemyTier: 4); // +100

            // 200 - 80 + 100 = 220
            Assert.IsTrue(spent, "SpendGold must succeed when balance is sufficient.");
            Assert.AreEqual(220f, economy.Gold,
                "Final gold must be: 200 (earned) - 80 (spent) + 100 (earned) = 220.");

            Object.DestroyImmediate(go);
        }
    }
}
