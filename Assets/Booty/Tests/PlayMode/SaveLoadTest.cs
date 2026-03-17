// ---------------------------------------------------------------------------
// SaveLoadTest.cs — PlayMode integration tests for save/load round-trip
// ---------------------------------------------------------------------------
// Tests GameState persistence: captured ports, player position, HP values.
// Pure in-memory — exercises CaptureFromSystems() + JsonUtility round-trip.
// No disk I/O required: all assertions run on deserialized GameState objects.
// ---------------------------------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Booty.Ships;
using Booty.Combat;
using Booty.Ports;
using Booty.Save;

namespace Booty.Tests.PlayMode
{
    /// <summary>
    /// PlayMode integration tests for the save/load pipeline.
    /// Verifies that CaptureFromSystems() correctly populates GameState
    /// and that state survives a JsonUtility serialization round-trip.
    /// </summary>
    public class SaveLoadTest
    {
        // ══════════════════════════════════════════════════════════════════
        //  Shared state
        // ══════════════════════════════════════════════════════════════════

        private readonly List<GameObject> _gos = new List<GameObject>();

        // ══════════════════════════════════════════════════════════════════
        //  SetUp / TearDown
        // ══════════════════════════════════════════════════════════════════

        [UnitySetUp]
        public IEnumerator SetUp() { yield return null; }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var go in _gos)
                if (go != null) Object.Destroy(go);
            _gos.Clear();
            yield return null;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Helpers
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Create a SaveSystem MonoBehaviour and call Initialize().</summary>
        private SaveSystem MakeSaveSystem()
        {
            var go = new GameObject("TestSaveSystem");
            _gos.Add(go);
            var ss = go.AddComponent<SaveSystem>();
            ss.Initialize();
            return ss;
        }

        /// <summary>Create a PortSystem with named ports, all initially british-owned.</summary>
        private PortSystem MakePortSystem(params string[] portIds)
        {
            var go = new GameObject("TestPortSystem");
            _gos.Add(go);
            var ps = go.AddComponent<PortSystem>();
            var configs = new List<PortRuntimeData>();
            foreach (var id in portIds)
                configs.Add(new PortRuntimeData
                {
                    portId = id, portName = id,
                    factionOwner = "british", regionId = "caribbean",
                    baseIncome = 50, defenseRating = 1f, level = 1
                });
            ps.Initialize(configs, null);
            return ps;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Test 1 — Captured ports survive JSON round-trip
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// After capturing 2 of 3 ports, CaptureFromSystems() populates capturedPortIds.
        /// Those IDs must survive JsonUtility serialization unchanged.
        /// </summary>
        [UnityTest]
        public IEnumerator SaveLoad_RoundTrip_CapturedPortsPersist()
        {
            var ss         = MakeSaveSystem();
            var portSystem = MakePortSystem("sl_port_a", "sl_port_b", "sl_port_c");

            portSystem.CapturePort("sl_port_a");
            portSystem.CapturePort("sl_port_b");
            // sl_port_c remains british-owned

            ss.CaptureFromSystems(null, null, portSystem);

            string json     = JsonUtility.ToJson(ss.CurrentState);
            var    restored = JsonUtility.FromJson<GameState>(json);

            Assert.IsNotNull(restored.capturedPortIds,
                "capturedPortIds must not be null after round-trip");
            Assert.AreEqual(2, restored.capturedPortIds.Count,
                "Must have exactly 2 captured ports after round-trip");
            Assert.IsTrue(restored.capturedPortIds.Contains("sl_port_a"),
                "sl_port_a must be in capturedPortIds");
            Assert.IsTrue(restored.capturedPortIds.Contains("sl_port_b"),
                "sl_port_b must be in capturedPortIds");
            Assert.IsFalse(restored.capturedPortIds.Contains("sl_port_c"),
                "sl_port_c (not captured) must NOT be in capturedPortIds");

            yield return null;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Test 2 — Player position persists
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// CaptureFromSystems() reads ShipController.transform.position and
        /// writes positionX/Z into GameState. Verifies round-trip accuracy.
        /// </summary>
        [UnityTest]
        public IEnumerator SaveLoad_PlayerPosition_Persists()
        {
            var ss = MakeSaveSystem();

            var shipGO = new GameObject("TestShip");
            _gos.Add(shipGO);
            shipGO.AddComponent<ShipController>();
            shipGO.transform.position = new Vector3(15f, 0f, -25f);
            var ship = shipGO.GetComponent<ShipController>();

            ss.CaptureFromSystems(null, ship, null);

            string json     = JsonUtility.ToJson(ss.CurrentState);
            var    restored = JsonUtility.FromJson<GameState>(json);

            Assert.AreEqual(15f,  restored.player.positionX, 0.01f,
                "positionX must be 15f after round-trip");
            Assert.AreEqual(-25f, restored.player.positionZ, 0.01f,
                "positionZ must be -25f after round-trip");

            yield return null;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Test 3 — HP (currentHull + maxHull) persists
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// After Configure(200) + TakeDamage(80), currentHP == 120.
        /// CaptureFromSystems() must write currentHull=120, maxHull=200 into GameState.
        /// </summary>
        [UnityTest]
        public IEnumerator SaveLoad_HP_Persists()
        {
            var ss = MakeSaveSystem();

            var hpGO = new GameObject("TestHP");
            _gos.Add(hpGO);
            var hp = hpGO.AddComponent<HPSystem>();
            hp.Configure(200);
            hp.TakeDamage(80);   // currentHP = 120

            ss.CaptureFromSystems(hp, null, null);

            string json     = JsonUtility.ToJson(ss.CurrentState);
            var    restored = JsonUtility.FromJson<GameState>(json);

            Assert.AreEqual(120, restored.playerShip.currentHull,
                "currentHull must be 120 after TakeDamage(80) from 200");
            Assert.AreEqual(200, restored.playerShip.maxHull,
                "maxHull must be 200");

            yield return null;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Test 4 — Edge case: zero gold
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Zero-value floats must survive JsonUtility serialization unchanged
        /// (JsonUtility does not skip zero-value fields).
        /// </summary>
        [UnityTest]
        public IEnumerator SaveLoad_EdgeCase_ZeroGold()
        {
            var ss = MakeSaveSystem();
            ss.CurrentState.player.gold = 0f;

            string json     = JsonUtility.ToJson(ss.CurrentState);
            var    restored = JsonUtility.FromJson<GameState>(json);

            Assert.AreEqual(0f, restored.player.gold, 0.001f,
                "Zero gold must survive round-trip — must not be skipped or default");

            yield return null;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Test 5 — Edge case: max HP (no damage taken)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// When no damage has been taken, currentHP == maxHP.
        /// CaptureFromSystems() must write both as equal values.
        /// </summary>
        [UnityTest]
        public IEnumerator SaveLoad_EdgeCase_MaxHP()
        {
            var ss = MakeSaveSystem();

            var hpGO = new GameObject("TestHPMax");
            _gos.Add(hpGO);
            var hp = hpGO.AddComponent<HPSystem>();
            hp.Configure(100);
            // No damage taken — currentHP == maxHP == 100

            ss.CaptureFromSystems(hp, null, null);

            string json     = JsonUtility.ToJson(ss.CurrentState);
            var    restored = JsonUtility.FromJson<GameState>(json);

            Assert.AreEqual(100, restored.playerShip.currentHull,
                "currentHull must equal maxHull when undamaged");
            Assert.AreEqual(100, restored.playerShip.maxHull,
                "maxHull must be 100");

            yield return null;
        }
    }
}
