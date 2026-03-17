// ---------------------------------------------------------------------------
// GameplayBot.cs — PlayMode acceptance test for S3.5 three-loop state machine
// ---------------------------------------------------------------------------
// Drives Sail→Hunt→Fight→Loot→CapturePort→Dock→Repair loop 3 times.
// Uses BotController (programmatic API) to avoid MonoBehaviour Update() timing.
// Distinct from Code/Testing/GameplayBot.cs (MonoBehaviour runtime bot).
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

namespace Booty.Tests.PlayMode
{
    /// <summary>
    /// S3.5 acceptance test: three complete Sail→Hunt→Fight→Loot→CapturePort→Dock→Repair loops.
    /// Uses BotController for programmatic API calls rather than MonoBehaviour timing.
    /// </summary>
    public class GameplayBotTest
    {
        // ══════════════════════════════════════════════════════════════════
        //  Shared state
        // ══════════════════════════════════════════════════════════════════

        private GameObject _playerGO;
        private readonly List<GameObject> _extraGOs = new List<GameObject>();
        private HPSystem _playerHP;
        private EconomySystem _economy;

        // ══════════════════════════════════════════════════════════════════
        //  SetUp / TearDown
        // ══════════════════════════════════════════════════════════════════

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _playerGO = new GameObject("BotTestPlayer");
            _playerGO.tag = "Player";
            var sc = _playerGO.AddComponent<ShipController>();
            _playerHP = _playerGO.AddComponent<HPSystem>();
            _playerHP.Configure(200);
            var bs = _playerGO.AddComponent<BroadsideSystem>();
            bs.Initialize(sc);
            _playerGO.transform.position = Vector3.zero;

            var econGO = new GameObject("BotTestEconomy");
            _extraGOs.Add(econGO);
            _economy = econGO.AddComponent<EconomySystem>();
            _economy.Initialize(null, null);

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_playerGO != null) Object.Destroy(_playerGO);
            foreach (var go in _extraGOs)
                if (go != null) Object.Destroy(go);
            _extraGOs.Clear();
            yield return null;
        }

        // ══════════════════════════════════════════════════════════════════
        //  S3.5 acceptance test: 3-loop state machine
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Runs 3 complete Sail→Hunt→Fight→Loot→CapturePort→Dock→Repair loops.
        /// Assertions per loop: gold increases, HP decreases on hit, port ownership
        /// changes, repair restores HP. Final: KillCount>=3, all 3 ports player-owned.
        /// </summary>
        [UnityTest]
        public IEnumerator ThreeLoop_StateMachine_AllAssertionsPass()
        {
            // ── Setup: port system with 3 capturable ports ────────────────
            var portGO = new GameObject("BotTestPortSystem");
            _extraGOs.Add(portGO);
            var portSystem = portGO.AddComponent<PortSystem>();

            var portConfigs = new List<PortRuntimeData>
            {
                new PortRuntimeData { portId = "bot_port_1", portName = "Bot Port 1",
                    factionOwner = "british", regionId = "caribbean",
                    baseIncome = 50, defenseRating = 1f, level = 1 },
                new PortRuntimeData { portId = "bot_port_2", portName = "Bot Port 2",
                    factionOwner = "spanish", regionId = "caribbean",
                    baseIncome = 55, defenseRating = 1f, level = 1 },
                new PortRuntimeData { portId = "bot_port_3", portName = "Bot Port 3",
                    factionOwner = "french",  regionId = "caribbean",
                    baseIncome = 60, defenseRating = 1f, level = 1 },
            };
            portSystem.Initialize(portConfigs, null);

            // ── Bot wired to player systems ───────────────────────────────
            var playerSC = _playerGO.GetComponent<ShipController>();
            var playerBS = _playerGO.GetComponent<BroadsideSystem>();
            var bot = new BotController(playerSC, playerBS, _playerHP, _economy);

            string[] portIds = { "bot_port_1", "bot_port_2", "bot_port_3" };
            float previousGold = _economy.Gold;
            int capturedPorts = 0;

            // ── 3-loop execution ──────────────────────────────────────────
            for (int loop = 0; loop < 3; loop++)
            {
                // ── STATE: SAIL ───────────────────────────────────────────
                Debug.Log("[GameplayBot] LOOP " + loop + " STATE SAIL");
                Vector3 enemyPos = new Vector3(60f + loop * 15f, 0f, 0f);
                Assert.DoesNotThrow(() => bot.MoveToward(enemyPos),
                    "Loop " + loop + ": SAIL — MoveToward must not throw");

                // ── STATE: HUNT ───────────────────────────────────────────
                Debug.Log("[GameplayBot] LOOP " + loop + " STATE HUNT");
                var enemyGO = new GameObject("BotEnemy_" + loop);
                _extraGOs.Add(enemyGO);
                var enemyHP = enemyGO.AddComponent<HPSystem>();
                enemyHP.Configure(100);
                bot.RegisterEnemyKill(enemyHP);
                Assert.IsFalse(enemyHP.IsDead,
                    "Loop " + loop + ": HUNT — enemy must be alive before combat");

                // ── STATE: FIGHT ──────────────────────────────────────────
                Debug.Log("[GameplayBot] LOOP " + loop + " STATE FIGHT");
                enemyHP.TakeDamage(enemyHP.MaxHP);
                Assert.IsTrue(enemyHP.IsDead,
                    "Loop " + loop + ": FIGHT — enemy must be dead after full-damage hit");

                // ── STATE: LOOT ───────────────────────────────────────────
                Debug.Log("[GameplayBot] LOOP " + loop + " STATE LOOT");
                int tier = loop + 1;
                _economy.AwardCombatSpoils(tier);
                float currentGold = _economy.Gold;
                Assert.Greater(currentGold, previousGold,
                    "Loop " + loop + ": LOOT — gold must increase after AwardCombatSpoils(tier=" + tier + ")");
                previousGold = currentGold;

                // ── STATE: CAPTURE_PORT ───────────────────────────────────
                Debug.Log("[GameplayBot] LOOP " + loop + " STATE CAPTURE_PORT");
                bool captured = bot.CapturePort(portSystem, portIds[loop]);
                Assert.IsTrue(captured,
                    "Loop " + loop + ": CAPTURE_PORT — CapturePort(" + portIds[loop] + ") must return true");
                Assert.AreEqual("player_pirates",
                    portSystem.GetPort(portIds[loop]).factionOwner,
                    "Loop " + loop + ": CAPTURE_PORT — port must be player-owned after capture");
                capturedPorts++;

                // ── STATE: DOCK ───────────────────────────────────────────
                Debug.Log("[GameplayBot] LOOP " + loop + " STATE DOCK");
                int hpBefore = _playerHP.CurrentHP;
                _playerHP.TakeDamage(40);
                Assert.AreEqual(hpBefore - 40, _playerHP.CurrentHP,
                    "Loop " + loop + ": DOCK — HP must decrease by 40 after TakeDamage(40)");

                // ── STATE: REPAIR ─────────────────────────────────────────
                Debug.Log("[GameplayBot] LOOP " + loop + " STATE REPAIR");
                bot.RepairShip(40);
                Assert.AreEqual(hpBefore, _playerHP.CurrentHP,
                    "Loop " + loop + ": REPAIR — HP must restore to " + hpBefore + " after RepairShip(40)");

                yield return null; // one frame per loop
            }

            // ── Final assertions ──────────────────────────────────────────
            Assert.GreaterOrEqual(bot.KillCount, 3,
                "Final: KillCount must be >= 3. Actual=" + bot.KillCount);
            Assert.AreEqual(3, capturedPorts,
                "Final: must have captured exactly 3 ports");
            Assert.IsFalse(_playerHP.IsDead,
                "Final: player must survive 3 full loops");
            Assert.AreEqual("player_pirates",
                portSystem.GetPort("bot_port_1").factionOwner,
                "Final: bot_port_1 must be player-owned");
            Assert.AreEqual("player_pirates",
                portSystem.GetPort("bot_port_2").factionOwner,
                "Final: bot_port_2 must be player-owned");
            Assert.AreEqual("player_pirates",
                portSystem.GetPort("bot_port_3").factionOwner,
                "Final: bot_port_3 must be player-owned");
        }
    }
}
