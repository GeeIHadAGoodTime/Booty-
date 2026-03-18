// ---------------------------------------------------------------------------
// Test_CombatSystem.cs — EditMode tests for HPSystem: damage, death flag,
// and the OnDestroyed event.
//
// All assertions are synchronous — no game loop required.
// FloatingDamageNumber GOs created by TakeDamage are cleaned up in TearDown.
// ---------------------------------------------------------------------------

using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Booty.Combat;
using Booty.UI;

namespace Booty.Tests
{
    /// <summary>
    /// EditMode tests for combat damage flow via <see cref="HPSystem"/>.
    /// Covers: HP reduction, ship destruction flag, and destruction event.
    /// </summary>
    [TestFixture]
    public class Test_CombatSystem
    {
        // ══════════════════════════════════════════════════════════════════
        //  Teardown — clean up any FloatingDamageNumber GOs left by TakeDamage
        // ══════════════════════════════════════════════════════════════════

        [TearDown]
        public void TearDown()
        {
            // TakeDamage spawns a FloatingDamageNumber Canvas GO per hit.
            // Destroy them so they don't pollute the EditMode scene.
            var damageNumbers = Object.FindObjectsByType<FloatingDamageNumber>(
                FindObjectsSortMode.None);
            foreach (var dn in damageNumbers)
                if (dn != null)
                    Object.DestroyImmediate(dn.gameObject);
        }

        // ── Test 1 ────────────────────────────────────────────────────────
        /// <summary>
        /// TakeDamage must reduce CurrentHP by the damage amount.
        /// A ship that takes 30 damage from 100 HP must have 70 HP remaining.
        /// </summary>
        [Test]
        public void CombatSystem_TakeDamage_ReducesCurrentHP()
        {
            var go = new GameObject("TestCombat_Damage");
            var hp = go.AddComponent<HPSystem>();
            hp.Configure(100);

            hp.TakeDamage(30);

            Assert.AreEqual(70, hp.CurrentHP,
                "HP must decrease by the damage amount: 100 - 30 = 70.");

            Object.DestroyImmediate(go);
        }

        // ── Test 2 ────────────────────────────────────────────────────────
        /// <summary>
        /// Lethal damage must set IsDead to true and bring CurrentHP to zero.
        /// Ship destruction must be detectable without the game loop.
        ///
        /// Note: Die() → SpawnDeathParticles() triggers multiple Unity EditMode
        /// errors (renderer.material instantiation + Destroy in edit mode). We use
        /// LogAssert.ignoreFailingMessages around the call to suppress them so the
        /// meaningful IsDead assertion can run.
        /// </summary>
        [Test]
        public void CombatSystem_TakeDamage_LethalDamage_SetsIsDead()
        {
            var go = new GameObject("TestCombat_Kill");
            var hp = go.AddComponent<HPSystem>();
            hp.Configure(50);

            // Die() triggers SpawnDeathParticles which generates edit-mode-only errors
            // (renderer.material + Destroy x8). Suppress them for this assertion.
            LogAssert.ignoreFailingMessages = true;
            hp.TakeDamage(50);
            LogAssert.ignoreFailingMessages = false;

            Assert.IsTrue(hp.IsDead,
                "IsDead must be true after HP reaches zero.");
            Assert.AreEqual(0, hp.CurrentHP,
                "CurrentHP must be clamped to zero on lethal damage.");

            Object.DestroyImmediate(go);
        }

        // ── Test 3 ────────────────────────────────────────────────────────
        /// <summary>
        /// OnDestroyed must fire synchronously when HP reaches zero.
        /// Subscribers (reward systems, game-over flow) depend on this event.
        ///
        /// Note: Die() → SpawnDeathParticles() triggers multiple Unity EditMode
        /// errors. We suppress them via ignoreFailingMessages so only the event
        /// assertion matters.
        /// </summary>
        [Test]
        public void CombatSystem_OnDestroyed_FiresWhenHPReachesZero()
        {
            var go = new GameObject("TestCombat_Event");
            var hp = go.AddComponent<HPSystem>();
            hp.Configure(10);

            bool eventFired = false;
            hp.OnDestroyed += () => eventFired = true;

            // Suppress edit-mode-only errors from SpawnDeathParticles
            LogAssert.ignoreFailingMessages = true;
            hp.TakeDamage(10);
            LogAssert.ignoreFailingMessages = false;

            Assert.IsTrue(eventFired,
                "OnDestroyed event must fire the moment HP reaches zero.");

            Object.DestroyImmediate(go);
        }
    }
}
