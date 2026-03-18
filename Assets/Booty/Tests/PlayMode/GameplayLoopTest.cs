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
using Booty.Ports;
using Booty.Testing;
using Booty.Core;

namespace Booty.Tests.PlayMode
{
    public class GameplayLoopTest
    {
        // ══════════════════════════════════════════════════════════════════
        //  Shared state for Test 1 (legacy bot loop)
        // ══════════════════════════════════════════════════════════════════

        private GameObject _playerGO;
        private readonly List<GameObject> _enemyGOs = new List<GameObject>();
        private GameObject _econGO;
        private EconomySystem _economy;
        private HPSystem _playerHP;
        private GameplayBot _bot;
        private float _startGold;

        // Extra GOs that tests 2-5 create; all destroyed in TearDown
        private readonly List<GameObject> _extraGOs = new List<GameObject>();

        // ══════════════════════════════════════════════════════════════════
        //  SetUp / TearDown
        // ══════════════════════════════════════════════════════════════════

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
            foreach (var go in _extraGOs) if (go != null) Object.Destroy(go);
            _enemyGOs.Clear();
            _extraGOs.Clear();
            yield return null;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Test 1 — Legacy bot loop (existing, preserved)
        // ══════════════════════════════════════════════════════════════════

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

        // ══════════════════════════════════════════════════════════════════
        //  Test 2 — Three loops: gold increases after each kill
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void ThreeLoops_GoldIncreasesAfterEachKill()
        {
            // Create BotController wired to player systems
            var playerSC = _playerGO.GetComponent<ShipController>();
            var playerBS = _playerGO.GetComponent<BroadsideSystem>();
            var bot = new BotController(playerSC, playerBS, _playerHP, _economy);

            float previousGold = _economy.Gold;
            int totalKills = 0;

            for (int loop = 0; loop < 3; loop++)
            {
                // Create a fresh enemy for this loop
                var enemyGO = new GameObject("LoopEnemy_" + loop);
                _extraGOs.Add(enemyGO);

                var enemyHP = enemyGO.AddComponent<HPSystem>();
                enemyHP.Configure(100);

                bot.RegisterEnemyKill(enemyHP);

                // Kill the enemy instantly by dealing full max HP as damage
                enemyHP.TakeDamage(enemyHP.MaxHP);

                // Award combat spoils manually (tier 1 for simplicity)
                _economy.AwardCombatSpoils(1);

                float currentGold = _economy.Gold;
                Assert.Greater(currentGold, previousGold,
                    string.Format("Loop {0}: gold should increase after kill. Before={1} After={2}",
                        loop, previousGold, currentGold));
                previousGold = currentGold;

                totalKills++;
            }

            Assert.GreaterOrEqual(bot.KillCount, 3,
                "BotController KillCount should be >= 3 after three loops. Actual=" + bot.KillCount);
            Assert.AreEqual(3, totalKills, "Should have completed exactly 3 kill loops");
        }

        // ══════════════════════════════════════════════════════════════════
        //  Test 3 — HP decreases on hit; repair restores HP
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void HP_DecreasesOnHit_RepairRestoresHP()
        {
            // Configure a fresh HP system at 200 max HP
            // (_playerHP is already Configure(200) from SetUp)
            Assert.AreEqual(200, _playerHP.MaxHP, "Player should start with 200 max HP");
            Assert.AreEqual(200, _playerHP.CurrentHP, "Player should start at full HP");

            // Apply 50 damage
            _playerHP.TakeDamage(50);
            Assert.AreEqual(150, _playerHP.CurrentHP,
                "After TakeDamage(50): CurrentHP should be 150");

            // Repair 50 HP
            _playerHP.Heal(50);
            Assert.AreEqual(200, _playerHP.CurrentHP,
                "After Heal(50): CurrentHP should be restored to 200");
        }

        // ══════════════════════════════════════════════════════════════════
        //  Test 4 — Port ownership changes on capture
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void PortOwnership_ChangesOnCapture()
        {
            // Create and initialize a PortSystem with 2 hostile ports
            var portGO = new GameObject("TestPortSystem");
            _extraGOs.Add(portGO);

            var portSystem = portGO.AddComponent<PortSystem>();

            var portConfigs = new List<PortRuntimeData>
            {
                new PortRuntimeData
                {
                    portId       = "port_enemy_1",
                    portName     = "Port Enemy 1",
                    factionOwner = "british",
                    regionId     = "caribbean",
                    baseIncome   = 50,
                    defenseRating = 1f,
                    level        = 1
                },
                new PortRuntimeData
                {
                    portId       = "port_enemy_2",
                    portName     = "Port Enemy 2",
                    factionOwner = "spanish",
                    regionId     = "caribbean",
                    baseIncome   = 60,
                    defenseRating = 2f,
                    level        = 1
                }
            };

            // null SaveSystem is safe — ApplySaveData() guards with null check
            portSystem.Initialize(portConfigs, null);

            // Verify initial ownership
            Assert.AreEqual("british", portSystem.GetPort("port_enemy_1").factionOwner,
                "port_enemy_1 should initially be owned by 'british'");
            Assert.AreEqual("spanish", portSystem.GetPort("port_enemy_2").factionOwner,
                "port_enemy_2 should initially be owned by 'spanish'");

            // Create BotController and capture both ports
            var playerSC = _playerGO.GetComponent<ShipController>();
            var playerBS = _playerGO.GetComponent<BroadsideSystem>();
            var bot = new BotController(playerSC, playerBS, _playerHP, _economy);

            bool captured1 = bot.CapturePort(portSystem, "port_enemy_1");
            Assert.IsTrue(captured1, "CapturePort should return true for port_enemy_1");
            Assert.AreEqual("player_pirates",
                portSystem.GetPort("port_enemy_1").factionOwner,
                "port_enemy_1 should now be owned by 'player_pirates'");

            bool captured2 = bot.CapturePort(portSystem, "port_enemy_2");
            Assert.IsTrue(captured2, "CapturePort should return true for port_enemy_2");
            Assert.AreEqual("player_pirates",
                portSystem.GetPort("port_enemy_2").factionOwner,
                "port_enemy_2 should now be owned by 'player_pirates'");
        }

        // ══════════════════════════════════════════════════════════════════
        //  Test 5 — Full loop: 3 cycles, all assertions
        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator FullLoop_ThreeCycles_AllAssertionsPass()
        {
            // ── Setup ────────────────────────────────────────────────────

            // Port system with 3 capturable ports (one per loop)
            var portGO = new GameObject("FullLoopPortSystem");
            _extraGOs.Add(portGO);
            var portSystem = portGO.AddComponent<PortSystem>();

            var portConfigs = new List<PortRuntimeData>
            {
                new PortRuntimeData
                {
                    portId        = "port_loop_1",
                    portName      = "Loop Port 1",
                    factionOwner  = "british",
                    regionId      = "caribbean",
                    baseIncome    = 50,
                    defenseRating = 1f,
                    level         = 1
                },
                new PortRuntimeData
                {
                    portId        = "port_loop_2",
                    portName      = "Loop Port 2",
                    factionOwner  = "spanish",
                    regionId      = "caribbean",
                    baseIncome    = 55,
                    defenseRating = 1f,
                    level         = 1
                },
                new PortRuntimeData
                {
                    portId        = "port_loop_3",
                    portName      = "Loop Port 3",
                    factionOwner  = "french",
                    regionId      = "caribbean",
                    baseIncome    = 60,
                    defenseRating = 1f,
                    level         = 1
                }
            };

            portSystem.Initialize(portConfigs, null);

            // Create BotController
            var playerSC = _playerGO.GetComponent<ShipController>();
            var playerBS = _playerGO.GetComponent<BroadsideSystem>();
            var bot = new BotController(playerSC, playerBS, _playerHP, _economy);

            string[] portIds = { "port_loop_1", "port_loop_2", "port_loop_3" };
            float previousGold = _economy.Gold;
            int loopGoldIncreases = 0;

            // ── 3-loop execution ─────────────────────────────────────────

            for (int loop = 0; loop < 3; loop++)
            {
                // 1. Sail phase: call MoveToward — assert no exceptions thrown
                Vector3 enemyPos = new Vector3(50f + loop * 10f, 0f, 0f);
                Assert.DoesNotThrow(
                    () => bot.MoveToward(enemyPos),
                    string.Format("Loop {0}: MoveToward should not throw", loop));

                // 2. Kill phase: create enemy and kill it instantly
                var enemyGO = new GameObject("FullLoopEnemy_" + loop);
                _extraGOs.Add(enemyGO);

                var enemyHP = enemyGO.AddComponent<HPSystem>();
                enemyHP.Configure(100);

                bot.RegisterEnemyKill(enemyHP);

                // Kill by dealing max HP damage
                enemyHP.TakeDamage(enemyHP.MaxHP);
                Assert.IsTrue(enemyHP.IsDead,
                    string.Format("Loop {0}: enemy should be dead after full-damage hit", loop));

                // 3. Gold phase: award combat spoils; assert gold increased
                int tier = loop + 1;
                _economy.AwardCombatSpoils(tier);

                float currentGold = _economy.Gold;
                Assert.Greater(currentGold, previousGold,
                    string.Format("Loop {0}: gold should increase after AwardCombatSpoils(tier={1}). Before={2} After={3}",
                        loop, tier, previousGold, currentGold));
                if (currentGold > previousGold) loopGoldIncreases++;
                previousGold = currentGold;

                // 4. Capture phase: capture the port for this loop
                bool captured = bot.CapturePort(portSystem, portIds[loop]);
                Assert.IsTrue(captured,
                    string.Format("Loop {0}: CapturePort({1}) should succeed", loop, portIds[loop]));
                Assert.AreEqual("player_pirates",
                    portSystem.GetPort(portIds[loop]).factionOwner,
                    string.Format("Loop {0}: {1} should now be owned by player_pirates", loop, portIds[loop]));

                // 5. Repair phase: take 30 damage then repair 30
                int hpBefore = _playerHP.CurrentHP;
                _playerHP.TakeDamage(30);
                Assert.AreEqual(hpBefore - 30, _playerHP.CurrentHP,
                    string.Format("Loop {0}: HP should decrease by 30 after TakeDamage(30)", loop));

                bot.RepairShip(30);
                Assert.AreEqual(hpBefore, _playerHP.CurrentHP,
                    string.Format("Loop {0}: HP should be restored to {1} after RepairShip(30)", loop, hpBefore));

                yield return null;
            }

            // ── Final assertions ─────────────────────────────────────────

            Assert.GreaterOrEqual(bot.KillCount, 3,
                "bot.KillCount should be >= 3 after 3 loops. Actual=" + bot.KillCount);

            Assert.AreEqual(3, loopGoldIncreases,
                "Gold should have increased in all 3 loops. Actual increases=" + loopGoldIncreases);

            Assert.IsFalse(_playerHP.IsDead, "Player should not be dead after 3 repair cycles");

            Assert.AreEqual("player_pirates",
                portSystem.GetPort("port_loop_1").factionOwner,
                "port_loop_1 should be player-owned at end");
            Assert.AreEqual("player_pirates",
                portSystem.GetPort("port_loop_2").factionOwner,
                "port_loop_2 should be player-owned at end");
            Assert.AreEqual("player_pirates",
                portSystem.GetPort("port_loop_3").factionOwner,
                "port_loop_3 should be player-owned at end");
        }

        // ══════════════════════════════════════════════════════════════════
        //  Test 6 — Renown increases after kills and port capture
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void KillAndCapture_RenownIncreases()
        {
            // ── Renown system setup ──────────────────────────────────────
            var renownGO = new GameObject("TestRenownSystem");
            _extraGOs.Add(renownGO);
            var renownSystem = renownGO.AddComponent<RenownSystem>();
            renownSystem.Initialize(null, null); // null SaveSystem and PortSystem are safe
            float startRenown = renownSystem.Renown;

            // ── Port system for capture event ────────────────────────────
            var portGO = new GameObject("TestRenownPortSystem");
            _extraGOs.Add(portGO);
            var portSystem = portGO.AddComponent<PortSystem>();
            var portConfigs = new System.Collections.Generic.List<PortRuntimeData>
            {
                new PortRuntimeData
                {
                    portId        = "port_renown_test",
                    portName      = "Renown Test Port",
                    factionOwner  = "british",
                    regionId      = "caribbean",
                    baseIncome    = 50,
                    defenseRating = 1f,
                    level         = 1
                }
            };
            portSystem.Initialize(portConfigs, null);

            // Wire renown to port capture events manually
            portSystem.OnPortCaptured += (portId, faction) =>
            {
                if (faction == "player_pirates")
                    renownSystem.AddRenown(25f); // RenownPerPortCapture default
            };

            // ── Kill awards renown ───────────────────────────────────────
            renownSystem.AwardKillRenown(1); // tier 1: default +5 renown
            Assert.Greater(renownSystem.Renown, startRenown,
                "Renown should increase after AwardKillRenown(1). Actual=" + renownSystem.Renown);

            float afterKillRenown = renownSystem.Renown;

            // ── Port capture awards renown ───────────────────────────────
            portSystem.CapturePort("port_renown_test");
            Assert.Greater(renownSystem.Renown, afterKillRenown,
                "Renown should increase after port capture. Actual=" + renownSystem.Renown);

            // ── Final check: kill(+5) + capture(+25) >= 30 ───────────────
            Assert.GreaterOrEqual(renownSystem.Renown, 30f,
                "After 1 kill (+5) + 1 capture (+25), renown should be >= 30. Actual=" + renownSystem.Renown);
        }
    }
}
