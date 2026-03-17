// ---------------------------------------------------------------------------
// GameplayLoopTest.cs - PlayMode integration test: GameplayBot autonomous loop
// ---------------------------------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Booty.Ships;
using Booty.Combat;
using Booty.Economy;
using Booty.Testing;

namespace Booty.Tests.PlayMode
{
    public class GameplayLoopTest
    {
        private GameObject _playerGO;
        private readonly List<GameObject> _enemyGOs = new List<GameObject>();
        private GameObject _econGO;
        private EconomySystem _economy;
        private HPSystem _playerHP;
        private GameplayBot _bot;
        private float _startGold;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _playerGO = new GameObject("TestPlayer");
            _playerGO.tag = "Player";
            var playerSC = _playerGO.AddComponent<ShipController>();
            _playerHP    = _playerGO.AddComponent<HPSystem>();
            _playerHP.Configure(200);
            var playerBS = _playerGO.AddComponent<BroadsideSystem>();
            playerBS.Initialize(playerSC);
            _playerGO.transform.position = Vector3.zero;

            _econGO    = new GameObject("TestEconomySystem");
            _economy   = _econGO.AddComponent<EconomySystem>();
            _economy.Initialize(null, null);
            _startGold = _economy.Gold;

            for (int i = 0; i < 2; i++)
            {
                var enemyGO = new GameObject("TestEnemy_" + i);
                enemyGO.transform.position = new Vector3(30f + i * 5f, 0f, 0f);
                var enemySC = enemyGO.AddComponent<ShipController>();
                var enemyHP = enemyGO.AddComponent<HPSystem>();
                enemyHP.Configure(100);
                var enemyBS = enemyGO.AddComponent<BroadsideSystem>();
                enemyBS.Initialize(enemySC);
                var ai = enemyGO.AddComponent<EnemyAI>();
                ai.Initialize(_playerGO.transform, enemySC, enemyBS, enemyHP);
                int tier = i + 1;
                enemyHP.OnDestroyed += () => _economy.AwardCombatSpoils(tier);
                _enemyGOs.Add(enemyGO);
            }

            _bot         = _playerGO.AddComponent<GameplayBot>();
            _bot.enabled = false;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            if (_playerGO != null) Object.Destroy(_playerGO);
            foreach (var e in _enemyGOs) if (e != null) Object.Destroy(e);
            if (_econGO != null) Object.Destroy(_econGO);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Bot_PlaysLoop_GoldIncreasesKillsOccurPlayerSurvives()
        {
            Time.timeScale = 10f;
            _bot.enabled = true;
            yield return new WaitForSeconds(6f);
            Time.timeScale = 1f;

            Assert.Greater(_economy.Gold, _startGold,
                "Gold should increase. Bot KillCount=" + _bot.KillCount);
            Assert.GreaterOrEqual(_bot.KillCount, 1,
                "Bot should kill >= 1 enemy in 60 game-seconds");
            Assert.IsFalse(_playerHP.IsDead, "Player should survive");
            yield return null;
        }
    }
}
