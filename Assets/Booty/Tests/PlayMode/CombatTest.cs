// ---------------------------------------------------------------------------
// CombatTest.cs - PlayMode tests: broadside damage flow and HP system
// ---------------------------------------------------------------------------
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Booty.Ships;
using Booty.Combat;

namespace Booty.Tests.PlayMode
{
    public class CombatTest
    {
        private GameObject _playerGO;
        private GameObject _enemyGO;
        private BroadsideSystem _playerBS;
        private HPSystem _enemyHP;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Player ship: origin, facing +Z (forward). Starboard = +X.
            _playerGO = new GameObject("CombatTestPlayer");
            _playerGO.tag = "Player";
            var playerSC = _playerGO.AddComponent<ShipController>();
            playerSC.SetPlayerControlled(false);
            _playerBS = _playerGO.AddComponent<BroadsideSystem>();
            _playerBS.Initialize(playerSC);
            _playerGO.transform.position = Vector3.zero;
            _playerGO.transform.rotation = Quaternion.identity;

            // Enemy ship: positioned in starboard arc (right side = +X)
            // within firing range so broadside volley can hit
            _enemyGO = new GameObject("CombatTestEnemy");
            _enemyHP  = _enemyGO.AddComponent<HPSystem>();
            _enemyHP.Configure(200);
            var enemySC = _enemyGO.AddComponent<ShipController>();
            enemySC.SetPlayerControlled(false);
            // CapsuleCollider for projectile trigger detection
            var col = _enemyGO.AddComponent<CapsuleCollider>();
            col.isTrigger = false;
            // Position within starboard arc: right of player, half firing range
            float range = _playerBS.FiringRange * 0.5f;
            _enemyGO.transform.position = new Vector3(range, 0f, 0f);

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_playerGO != null) Object.Destroy(_playerGO);
            if (_enemyGO != null) Object.Destroy(_enemyGO);
            // Clean up any lingering projectiles
            foreach (var p in Object.FindObjectsOfType<Projectile>())
                Object.Destroy(p.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Broadside_DealsDamage_ToEnemy()
        {
            int startHP = _enemyHP.CurrentHP;

            // Verify arc check before firing
            bool inArc = _playerBS.IsInStarboardArc(_enemyGO.transform.position);
            Assert.IsTrue(inArc,
                "Enemy should be in starboard arc. Player at " +
                _playerGO.transform.position + ", enemy at " + _enemyGO.transform.position +
                ", player forward: " + _playerGO.transform.forward);

            // Fire the starboard broadside
            bool fired = _playerBS.FireStarboard();
            Assert.IsTrue(fired, "FireStarboard should succeed (broadside should be ready)");

            // Wait for projectile(s) to travel to enemy and trigger hit
            yield return new WaitForSeconds(2f);

            Assert.Less(_enemyHP.CurrentHP, startHP,
                "Enemy HP should decrease after broadside. Started: " + startHP +
                ", current: " + _enemyHP.CurrentHP + ". Check projectile hit detection.");
        }

        [UnityTest]
        public IEnumerator HPSystem_TakeDamage_ReducesHP()
        {
            int startHP = _enemyHP.CurrentHP;
            _enemyHP.TakeDamage(50);
            yield return null;
            Assert.AreEqual(startHP - 50, _enemyHP.CurrentHP,
                "HP should decrease by exactly 50");
        }

        [UnityTest]
        public IEnumerator HPSystem_Death_FiresEvent()
        {
            bool deathFired = false;
            _enemyHP.OnDestroyed += () => { deathFired = true; };

            // Deal lethal damage
            _enemyHP.TakeDamage(_enemyHP.MaxHP + 1000);

            yield return new WaitForSeconds(0.5f);

            Assert.IsTrue(deathFired, "OnDestroyed event should fire after fatal damage");
            Assert.IsTrue(_enemyHP.IsDead, "IsDead should be true after fatal damage");
        }
    }
}
