// ---------------------------------------------------------------------------
// Test_ShipUpgradesAndCrew.cs — EditMode tests for ShipUpgradeManager + CrewManager
// ---------------------------------------------------------------------------
// Verifies:
//   - Upgrade purchase deducts gold and applies stat bonuses
//   - Tier prerequisite enforcement (cannot buy Tier 2 before Tier 1)
//   - Crew hire deducts gold and changes crew count
//   - Crew dismiss is free and changes crew count
//   - CrewSpeedMultiplier scales correctly with crew count
// ---------------------------------------------------------------------------

using NUnit.Framework;
using UnityEngine;
using Booty.Ships;
using Booty.Economy;
using Booty.Combat;
using Booty.Ports;
using Booty.Save;

namespace Booty.Tests
{
    /// <summary>
    /// EditMode unit tests for <see cref="ShipUpgradeManager"/> and
    /// <see cref="CrewManager"/>. No game loop required.
    /// </summary>
    [TestFixture]
    public class Test_ShipUpgradesAndCrew
    {
        // ── Shared test fixtures ─────────────────────────────────────────────
        private GameObject        _shipGO;
        private ShipController    _shipController;
        private HPSystem          _hpSystem;
        private BroadsideSystem   _broadside;
        private EconomySystem     _economy;
        private ShipUpgradeManager _upgradeManager;
        private CrewManager       _crewManager;

        [SetUp]
        public void SetUp()
        {
            // Create a minimal player ship GO with all required components
            _shipGO         = new GameObject("TestShip");
            _shipController = _shipGO.AddComponent<ShipController>();
            _hpSystem       = _shipGO.AddComponent<HPSystem>();
            _broadside      = _shipGO.AddComponent<BroadsideSystem>();

            // Configure base HP
            _hpSystem.Configure(80);
            _broadside.Initialize(_shipController);

            // Economy system (no PortSystem/SaveSystem needed for gold-only ops)
            var econGO = new GameObject("TestEconomy");
            _economy   = econGO.AddComponent<EconomySystem>();
            // Give the player 1000 gold to start tests
            _economy.SetGold(1000f);

            // Upgrade manager
            var upgradeGO   = new GameObject("TestUpgradeManager");
            _upgradeManager = upgradeGO.AddComponent<ShipUpgradeManager>();
            _upgradeManager.Initialize(
                _shipController, _hpSystem, _broadside, _economy,
                baseMaxHP: 80);

            // Crew manager
            var crewGO   = new GameObject("TestCrewManager");
            _crewManager = crewGO.AddComponent<CrewManager>();
            _crewManager.Initialize(_economy, _shipController, startingCrew: 5);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_shipGO);
            Object.DestroyImmediate(_economy.gameObject);
            Object.DestroyImmediate(_upgradeManager.gameObject);
            Object.DestroyImmediate(_crewManager.gameObject);
        }

        // ════════════════════════════════════════════════════════════════════
        //  Upgrade Tests
        // ════════════════════════════════════════════════════════════════════

        // ── Test 1 ────────────────────────────────────────────────────────
        /// <summary>
        /// Buying a Hull Tier 1 upgrade must deduct the correct gold cost.
        /// </summary>
        [Test]
        public void Upgrade_HullTier1_DeductsCorrectGold()
        {
            var upgrade = CreateHullUpgrade(tier: 1, cost: 200f, hullBonus: 20);
            float goldBefore = _economy.Gold;

            bool bought = _upgradeManager.BuyUpgrade(upgrade);

            Assert.IsTrue(bought, "BuyUpgrade must succeed when gold is sufficient.");
            Assert.AreEqual(goldBefore - 200f, _economy.Gold, 0.01f,
                "Gold must decrease by the upgrade cost (200g).");

            Object.DestroyImmediate(upgrade);
        }

        // ── Test 2 ────────────────────────────────────────────────────────
        /// <summary>
        /// Buying a Hull Tier 1 upgrade must increase the player ship's max HP.
        /// </summary>
        [Test]
        public void Upgrade_HullTier1_IncreasesMaxHP()
        {
            int hpBefore = _hpSystem.MaxHP;
            var upgrade  = CreateHullUpgrade(tier: 1, cost: 200f, hullBonus: 20);

            _upgradeManager.BuyUpgrade(upgrade);

            Assert.AreEqual(hpBefore + 20, _hpSystem.MaxHP,
                "Max HP must increase by hullBonus after hull upgrade.");

            Object.DestroyImmediate(upgrade);
        }

        // ── Test 3 ────────────────────────────────────────────────────────
        /// <summary>
        /// Cannot purchase Tier 2 before Tier 1 has been purchased.
        /// </summary>
        [Test]
        public void Upgrade_Tier2_RequiresTier1_Prerequisite()
        {
            var tier2 = CreateHullUpgrade(tier: 2, cost: 400f, hullBonus: 40);

            bool bought = _upgradeManager.BuyUpgrade(tier2);

            Assert.IsFalse(bought,
                "Tier 2 upgrade must NOT be purchasable before Tier 1 is owned.");

            Object.DestroyImmediate(tier2);
        }

        // ── Test 4 ────────────────────────────────────────────────────────
        /// <summary>
        /// After buying Tier 1, Tier 2 becomes available.
        /// </summary>
        [Test]
        public void Upgrade_Tier2_AvailableAfterTier1()
        {
            var tier1 = CreateHullUpgrade(tier: 1, cost: 200f, hullBonus: 20);
            var tier2 = CreateHullUpgrade(tier: 2, cost: 400f, hullBonus: 40);

            bool boughtT1 = _upgradeManager.BuyUpgrade(tier1);
            bool boughtT2 = _upgradeManager.BuyUpgrade(tier2);

            Assert.IsTrue(boughtT1,  "Tier 1 must purchase successfully.");
            Assert.IsTrue(boughtT2,  "Tier 2 must be purchasable after Tier 1.");
            Assert.AreEqual(80 + 20 + 40, _hpSystem.MaxHP,
                "Max HP must reflect cumulative hull bonuses from both tiers.");

            Object.DestroyImmediate(tier1);
            Object.DestroyImmediate(tier2);
        }

        // ════════════════════════════════════════════════════════════════════
        //  Crew Tests
        // ════════════════════════════════════════════════════════════════════

        // ── Test 5 ────────────────────────────────────────────────────────
        /// <summary>
        /// Hiring crew deducts the correct gold (costPerHead × count).
        /// </summary>
        [Test]
        public void Crew_Hire_DeductsCorrectGold()
        {
            float goldBefore = _economy.Gold;
            // Default costPerHead in CrewManager inspector is 25g
            bool hired = _crewManager.HireCrew(3);

            Assert.IsTrue(hired, "HireCrew must succeed when gold is sufficient.");
            Assert.AreEqual(goldBefore - 75f, _economy.Gold, 0.01f,
                "Hiring 3 crew at 25g each must deduct 75g.");
        }

        // ── Test 6 ────────────────────────────────────────────────────────
        /// <summary>
        /// Dismissing crew reduces crew count. No gold cost.
        /// </summary>
        [Test]
        public void Crew_Dismiss_ReducesCrewCountForFree()
        {
            int crewBefore  = _crewManager.CurrentCrew;
            float goldBefore = _economy.Gold;

            bool dismissed = _crewManager.DismissCrew(2);

            Assert.IsTrue(dismissed, "DismissCrew must succeed when above minCrew.");
            Assert.AreEqual(crewBefore - 2, _crewManager.CurrentCrew,
                "Crew count must decrease by the dismissed amount.");
            Assert.AreEqual(goldBefore, _economy.Gold, 0.01f,
                "Dismissing crew must cost no gold.");
        }

        // ── Test 7 ────────────────────────────────────────────────────────
        /// <summary>
        /// CrewSpeedMultiplier must be less than 1.0 when below optimal crew.
        /// </summary>
        [Test]
        public void Crew_SpeedMultiplier_BelowOptimal_IsLessThanOne()
        {
            // Starting crew is 5, optimalCrew default is 10
            float speedMult = _crewManager.CrewSpeedMultiplier;

            Assert.Less(speedMult, 1.0f,
                "Speed multiplier must be < 1.0 when crew count is below optimal.");
            Assert.Greater(speedMult, 0f,
                "Speed multiplier must always be positive.");
        }

        // ── Test 8 ────────────────────────────────────────────────────────
        /// <summary>
        /// Cannot dismiss crew below minCrew (1 by default).
        /// </summary>
        [Test]
        public void Crew_Dismiss_CannotGoBelowMinCrew()
        {
            // Dismiss down to 1 (minCrew)
            _crewManager.DismissCrew(_crewManager.CurrentCrew - 1);

            // Try to dismiss one more
            bool dismissed = _crewManager.DismissCrew(1);

            Assert.IsFalse(dismissed,
                "Cannot dismiss crew below minCrew threshold.");
            Assert.AreEqual(_crewManager.MinCrew, _crewManager.CurrentCrew,
                "Crew must not go below minCrew.");
        }

        // ════════════════════════════════════════════════════════════════════
        //  Helpers
        // ════════════════════════════════════════════════════════════════════

        private static ShipUpgradeData CreateHullUpgrade(int tier, float cost, int hullBonus)
        {
            var so = ScriptableObject.CreateInstance<ShipUpgradeData>();
            so.upgradeType = UpgradeType.Hull;
            so.tier        = tier;
            so.displayName = $"Test Hull T{tier}";
            so.cost        = cost;
            so.hullBonus   = hullBonus;
            return so;
        }
    }
}
