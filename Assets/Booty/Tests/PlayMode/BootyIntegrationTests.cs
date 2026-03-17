// ---------------------------------------------------------------------------
// BootyIntegrationTests.cs - PlayMode integration tests: core gameplay loop
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
using Booty.Save;
using Booty.UI;

namespace Booty.Tests.PlayMode
{
    /// <summary>
    /// Integration smoke tests for the core P1 gameplay loop.
    /// Each test is self-contained: creates its own GameObjects and destroys them.
    /// </summary>
    public class BootyIntegrationTests
    {
        // -- Test 1: Player ship movement ────────────────────────────────────

        [UnityTest]
        public IEnumerator Test_PlayerCanSail()
        {
            // Arrange: ship at origin, AI-controlled, facing +Z
            var go = new GameObject("SailTestShip");
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            var sc = go.AddComponent<ShipController>();
            sc.SetPlayerControlled(false);

            // Act: full throttle ahead
            sc.SetThrottle(1f);

            // Wait for physics/movement frames
            yield return new WaitForSeconds(0.5f);

            // Assert: ship moved forward along +Z
            Assert.Greater(go.transform.position.z, 0f,
                "Ship should have moved forward (positive Z) after 0.5s at full throttle. " +
                "Position: " + go.transform.position);

            Object.Destroy(go);
            yield return null;
        }

        // -- Test 2: Enemy takes damage ──────────────────────────────────────

        [UnityTest]
        public IEnumerator Test_EnemyTakesDamage()
        {
            // Arrange
            var go = new GameObject("DamageTestEnemy");
            var hp = go.AddComponent<HPSystem>();
            hp.Configure(100);
            int startHP = hp.CurrentHP;

            // Act
            hp.TakeDamage(30);

            yield return null;

            // Assert
            Assert.AreEqual(startHP - 30, hp.CurrentHP,
                "HP should decrease by exactly 30. Start: " + startHP +
                ", Current: " + hp.CurrentHP);

            // Cleanup: HP death may Destroy the GO, check first
            if (go != null) Object.Destroy(go);
            // Clean up any floating damage numbers spawned
            foreach (var fn in Object.FindObjectsOfType<FloatingDamageNumber>())
                if (fn != null) Object.Destroy(fn.gameObject);
            yield return null;
        }

        // -- Test 3: Kill enemy awards gold ─────────────────────────────────

        [UnityTest]
        public IEnumerator Test_EnemyKillAwardsGold()
        {
            // Arrange: economy system
            var econGO = new GameObject("KillRewardEconomy");
            var economy = econGO.AddComponent<EconomySystem>();
            economy.Initialize(null, null); // starts with 200 gold

            // Arrange: enemy with kill-reward wiring
            var enemyGO = new GameObject("KillRewardEnemy");
            var enemyHP = enemyGO.AddComponent<HPSystem>();
            enemyHP.Configure(50);
            enemyHP.OnDestroyed += () => economy.AwardCombatSpoils(1);

            float startGold = economy.Gold;

            // Act: kill enemy
            enemyHP.TakeDamage(1000);

            // Wait for death event
            yield return new WaitForSeconds(0.5f);

            // Assert
            Assert.Greater(economy.Gold, startGold,
                "Gold should increase after enemy death. Start: " + startGold +
                ", Now: " + economy.Gold);

            Object.Destroy(econGO);
            // enemyGO may already be destroyed by HPSystem death effect
            if (enemyGO != null) Object.Destroy(enemyGO);
            foreach (var fn in Object.FindObjectsOfType<FloatingDamageNumber>())
                if (fn != null) Object.Destroy(fn.gameObject);
            yield return null;
        }

        // -- Test 4: Port capture changes owner ─────────────────────────────

        [UnityTest]
        public IEnumerator Test_PortCaptureChangesOwner()
        {
            // Arrange: port system with one enemy-owned port
            var psGO = new GameObject("CaptureTestPortSystem");
            var portSystem = psGO.AddComponent<PortSystem>();

            var testPort = new PortRuntimeData
            {
                portId       = "test_capture_port",
                portName     = "Test Capture Port",
                regionId     = "test_region",
                factionOwner = "enemy_faction",
                baseIncome   = 100,
                defenseRating = 1f,
                level        = 1,
                worldPosition = Vector3.zero
            };

            portSystem.Initialize(new List<PortRuntimeData> { testPort }, null);

            // Verify initial ownership
            Assert.AreEqual("enemy_faction",
                portSystem.GetPort("test_capture_port").factionOwner,
                "Port should start enemy-owned");

            // Act: capture the port
            bool captured = portSystem.CapturePort("test_capture_port");

            yield return null;

            // Assert
            Assert.IsTrue(captured, "CapturePort should return true");
            Assert.AreEqual("player_pirates",
                portSystem.GetPort("test_capture_port").factionOwner,
                "Port should now be player-owned");

            Object.Destroy(psGO);
            yield return null;
        }

        // -- Test 5: Repair restores HP ──────────────────────────────────────

        [UnityTest]
        public IEnumerator Test_RepairRestoresHP()
        {
            // Arrange: save system
            var saveGO = new GameObject("RepairTestSave");
            var saveSystem = saveGO.AddComponent<SaveSystem>();
            saveSystem.Initialize();

            // Simulate hull damage: currentHull < maxHull
            saveSystem.CurrentState.playerShip.currentHull = 40;
            saveSystem.CurrentState.playerShip.maxHull     = 80;

            // Arrange: economy system (starts with 200 gold -- enough to repair)
            var econGO = new GameObject("RepairTestEconomy");
            var economy = econGO.AddComponent<EconomySystem>();
            economy.Initialize(null, saveSystem);

            // Arrange: repair shop
            var shopGO = new GameObject("RepairTestShop");
            var repairShop = shopGO.AddComponent<RepairShop>();
            repairShop.Initialize(economy, saveSystem);

            // Act
            bool repaired = repairShop.RepairShip();

            yield return null;

            // Assert
            Assert.IsTrue(repaired, "RepairShip should return true when damaged and gold is sufficient");
            Assert.AreEqual(
                saveSystem.CurrentState.playerShip.maxHull,
                saveSystem.CurrentState.playerShip.currentHull,
                "Hull HP should be fully restored to max after repair");

            Object.Destroy(saveGO);
            Object.Destroy(econGO);
            Object.Destroy(shopGO);
            yield return null;
        }
    }
}