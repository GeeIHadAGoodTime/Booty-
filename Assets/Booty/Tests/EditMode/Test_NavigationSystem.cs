// ---------------------------------------------------------------------------
// Test_NavigationSystem.cs — EditMode tests for ShipController and ShipDamageState.
//
// ShipController.Update() doesn't run in EditMode — we test:
//   (a) Initial state values that don't require a game loop.
//   (b) ShipDamageState ratios, which update synchronously via HPSystem events.
//
// Note: TakeDamage spawns FloatingDamageNumber GOs; cleaned up in TearDown.
// ---------------------------------------------------------------------------

using NUnit.Framework;
using UnityEngine;
using Booty.Combat;
using Booty.Ships;
using Booty.UI;

namespace Booty.Tests
{
    /// <summary>
    /// EditMode tests for navigation mechanics via <see cref="ShipController"/>
    /// and hull/sail performance penalties via <see cref="ShipDamageState"/>.
    /// </summary>
    [TestFixture]
    public class Test_NavigationSystem
    {
        // ══════════════════════════════════════════════════════════════════
        //  Teardown — clean up FloatingDamageNumber GOs from TakeDamage
        // ══════════════════════════════════════════════════════════════════

        [TearDown]
        public void TearDown()
        {
            var damageNumbers = Object.FindObjectsByType<FloatingDamageNumber>(
                FindObjectsSortMode.None);
            foreach (var dn in damageNumbers)
                if (dn != null)
                    Object.DestroyImmediate(dn.gameObject);
        }

        // ── Test 1 ────────────────────────────────────────────────────────
        /// <summary>
        /// A freshly created ship must begin at rest (CurrentSpeed = 0).
        /// No movement should occur without Update() being driven by the game loop.
        /// </summary>
        [Test]
        public void NavigationSystem_NewShip_StartsAtZeroSpeed()
        {
            var go   = new GameObject("TestNav_Speed");
            var ship = go.AddComponent<ShipController>();

            Assert.AreEqual(0f, ship.CurrentSpeed,
                "ShipController.CurrentSpeed must be 0 before any Update tick.");
            Assert.AreEqual(0f, ship.SpeedNormalized,
                "SpeedNormalized must be 0 when CurrentSpeed is 0.");

            Object.DestroyImmediate(go);
        }

        // ── Test 2 ────────────────────────────────────────────────────────
        /// <summary>
        /// A freshly wired ShipDamageState must report zero hull and sail damage.
        /// Ratios must be 0 before the ship has taken any hits.
        /// </summary>
        [Test]
        public void NavigationSystem_DamageState_RatiosBeginAtZero()
        {
            var go          = new GameObject("TestNav_DamageInit");
            var hp          = go.AddComponent<HPSystem>();
            var ship        = go.AddComponent<ShipController>();
            var damageState = go.AddComponent<ShipDamageState>();
            // ShipDamageState.Awake() auto-wires to HPSystem on the same GO.

            hp.Configure(100);

            Assert.AreEqual(0f, damageState.HullDamageRatio,
                "HullDamageRatio must be 0 before any damage is taken.");
            Assert.AreEqual(0f, damageState.SailDamageRatio,
                "SailDamageRatio must be 0 before any damage is taken.");

            Object.DestroyImmediate(go);
        }

        // ── Test 3 ────────────────────────────────────────────────────────
        /// <summary>
        /// After taking damage, ShipDamageState must report non-zero hull and sail
        /// damage ratios. The ratios are driven by HPSystem.OnDamaged which fires
        /// synchronously — no game loop needed.
        ///
        /// We call Initialize() explicitly rather than relying on Awake() auto-wiring,
        /// because NUnit [Test] (non-[UnityTest]) may not advance Unity's lifecycle for
        /// event subscriptions to wire automatically.
        ///
        /// At 50% HP loss both HullDamageRatio and SailDamageRatio are nudged to
        /// at least 25% and 20% respectively (see ShipDamageState.OnDamaged nudge logic).
        /// </summary>
        [Test]
        public void NavigationSystem_DamageState_RatiosIncreaseAfterHit()
        {
            var goHP    = new GameObject("TestNav_DamageHit_HP");
            var goShip  = new GameObject("TestNav_DamageHit_Ship");
            var goDmg   = new GameObject("TestNav_DamageHit_Dmg");

            var hp          = goHP.AddComponent<HPSystem>();
            var ship        = goShip.AddComponent<ShipController>();
            var damageState = goDmg.AddComponent<ShipDamageState>();

            // Explicitly wire the damage state to avoid Awake() ordering ambiguity
            hp.Configure(100);
            damageState.Initialize(hp, ship);  // subscribes to hp.OnDamaged

            hp.TakeDamage(50);     // 50% hull lost → overallDamageFraction = 0.5
            // Both pools are always nudged: hull >= 0.5*0.5=0.25, sail >= 0.5*0.4=0.20

            Assert.Greater(damageState.HullDamageRatio, 0f,
                "HullDamageRatio must increase after taking 50% hull damage.");
            Assert.Greater(damageState.SailDamageRatio, 0f,
                "SailDamageRatio must increase after taking 50% hull damage.");

            Assert.GreaterOrEqual(damageState.HullDamageRatio, 0.25f,
                "HullDamageRatio must be at least 0.25 (hull nudge = overallFraction * 0.5).");
            Assert.GreaterOrEqual(damageState.SailDamageRatio, 0.20f,
                "SailDamageRatio must be at least 0.20 (sail nudge = overallFraction * 0.4).");

            Object.DestroyImmediate(goHP);
            Object.DestroyImmediate(goShip);
            Object.DestroyImmediate(goDmg);
        }
    }
}
