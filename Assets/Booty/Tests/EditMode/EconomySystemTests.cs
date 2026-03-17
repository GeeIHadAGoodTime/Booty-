// ---------------------------------------------------------------------------
// EconomySystemTests.cs — EditMode smoke tests for EconomySystem gold logic.
// Tests gold add/spend without requiring PortSystem or SaveSystem dependencies.
// ---------------------------------------------------------------------------

using NUnit.Framework;
using UnityEngine;
using Booty.Economy;

namespace Booty.Tests
{
    /// <summary>
    /// Smoke tests for <see cref="EconomySystem"/> gold mechanics.
    /// Verifies AddGold increases balance and SpendGold enforces funds check.
    /// </summary>
    [TestFixture]
    public class EconomySystemTests
    {
        // ── Test 1 ────────────────────────────────────────────────────────
        /// <summary>
        /// AddGold must increase the Gold balance by the specified amount.
        /// </summary>
        [Test]
        public void EconomySystem_AddGold_IncreasesBalance()
        {
            var go            = new GameObject("TestEconomy_Add");
            var economySystem = go.AddComponent<EconomySystem>();   // Gold starts at 0

            economySystem.AddGold(100f);

            Assert.AreEqual(100f, economySystem.Gold,
                "Gold must equal 100 after AddGold(100).");

            Object.DestroyImmediate(go);
        }

        // ── Test 2 ────────────────────────────────────────────────────────
        /// <summary>
        /// SpendGold must return true and deduct the amount when funds are sufficient.
        /// </summary>
        [Test]
        public void EconomySystem_SpendGold_SucceedsWhenSufficient()
        {
            var go            = new GameObject("TestEconomy_Spend");
            var economySystem = go.AddComponent<EconomySystem>();

            economySystem.AddGold(200f);
            bool result = economySystem.SpendGold(50f);

            Assert.IsTrue(result, "SpendGold must return true when balance is sufficient.");
            Assert.AreEqual(150f, economySystem.Gold,
                "Gold must equal 150 after spending 50 from 200.");

            Object.DestroyImmediate(go);
        }

        // ── Test 3 ────────────────────────────────────────────────────────
        /// <summary>
        /// SpendGold must return false and leave Gold unchanged when funds are insufficient.
        /// </summary>
        [Test]
        public void EconomySystem_SpendGold_FailsWhenInsufficient()
        {
            var go            = new GameObject("TestEconomy_InsufficientFunds");
            var economySystem = go.AddComponent<EconomySystem>();   // Gold starts at 0

            bool result = economySystem.SpendGold(100f);

            Assert.IsFalse(result, "SpendGold must return false when balance is insufficient.");
            Assert.AreEqual(0f, economySystem.Gold,
                "Gold must remain 0 when SpendGold fails.");

            Object.DestroyImmediate(go);
        }
    }
}
