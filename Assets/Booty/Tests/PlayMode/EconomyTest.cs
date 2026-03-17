// ---------------------------------------------------------------------------
// EconomyTest.cs - PlayMode tests: gold awards and kill-event integration
// ---------------------------------------------------------------------------
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Booty.Economy;
using Booty.Combat;

namespace Booty.Tests.PlayMode
{
    public class EconomyTest
    {
        private EconomySystem _economy;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var go  = new GameObject("TestEconomySystem");
            _economy = go.AddComponent<EconomySystem>();
            // Initialize with null portSystem and saveSystem - both are null-safe
            _economy.Initialize(null, null);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_economy != null) Object.Destroy(_economy.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AwardCombatSpoils_Tier1_IncreasesGold()
        {
            float startGold = _economy.Gold;
            _economy.AwardCombatSpoils(1);
            yield return null;
            Assert.Greater(_economy.Gold, startGold,
                "Gold should increase after tier-1 kill. Started: " + startGold +
                ", now: " + _economy.Gold);
        }

        [UnityTest]
        public IEnumerator AwardCombatSpoils_HigherTier_AwardsMoreGold()
        {
            float g0 = _economy.Gold;
            _economy.AwardCombatSpoils(1);
            yield return null;
            float tier1Award = _economy.Gold - g0;

            float g1 = _economy.Gold;
            _economy.AwardCombatSpoils(3);
            yield return null;
            float tier3Award = _economy.Gold - g1;

            Assert.Greater(tier3Award, tier1Award,
                "Tier-3 award (" + tier3Award + ") should exceed tier-1 (" + tier1Award + ")");
        }

        [UnityTest]
        public IEnumerator KillEnemy_ViaOnDestroyedEvent_AwardsGold()
        {
            // Mirror BootyBootstrap wiring: enemy.OnDestroyed -> AwardCombatSpoils
            var enemyGO = new GameObject("TestEnemy");
            var enemyHP = enemyGO.AddComponent<HPSystem>();
            enemyHP.Configure(100);

            int tier = 2;
            enemyHP.OnDestroyed += () => _economy.AwardCombatSpoils(tier);

            float startGold = _economy.Gold;

            // Kill the enemy
            enemyHP.TakeDamage(1000);

            // Wait for death event to fire and gold to update
            yield return new WaitForSeconds(1f);

            Object.Destroy(enemyGO);

            Assert.Greater(_economy.Gold, startGold,
                "Gold should increase after enemy death event. Started: " + startGold +
                ", now: " + _economy.Gold);
        }
    }
}
