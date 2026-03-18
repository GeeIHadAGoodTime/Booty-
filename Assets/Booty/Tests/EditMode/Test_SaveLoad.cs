// ---------------------------------------------------------------------------
// Test_SaveLoad.cs — EditMode tests for GameState serialization and save fidelity.
//
// Complements SaveSystemTests.cs (which covers player + ship round-trips).
// This file adds coverage for: port ownership, economy timer, and full-state
// multi-section fidelity.
//
// All tests are pure in-memory — no disk I/O, no SaveSystem MonoBehaviour needed.
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Booty.Save;

namespace Booty.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="GameState"/> JSON serialization round-trips.
    /// Covers port ownership, economy timer, and full multi-section state.
    /// </summary>
    [TestFixture]
    public class Test_SaveLoad
    {
        // ── Test 1 ────────────────────────────────────────────────────────
        /// <summary>
        /// Port ownership data must survive a JsonUtility round-trip.
        /// Port faction IDs determine income and capture-victory conditions;
        /// corruption here would break the economy loop.
        /// </summary>
        [Test]
        public void SaveLoad_PortOwnership_SurvivesJsonRoundTrip()
        {
            var original = new GameState();
            original.ports = new List<PortSaveData>
            {
                new PortSaveData { portId = "port_havana",    factionOwner = "player_pirates" },
                new PortSaveData { portId = "port_tortuga",   factionOwner = "spanish_crown"  },
                new PortSaveData { portId = "port_nassau",    factionOwner = "player_pirates" },
            };

            string json        = JsonUtility.ToJson(original);
            var    deserialized = JsonUtility.FromJson<GameState>(json);

            Assert.IsNotNull(deserialized.ports,
                "Ports list must not be null after deserialization.");
            Assert.AreEqual(3, deserialized.ports.Count,
                "All 3 port entries must survive the round-trip.");

            Assert.AreEqual("port_havana",    deserialized.ports[0].portId,      "portId[0] mismatch.");
            Assert.AreEqual("player_pirates", deserialized.ports[0].factionOwner, "factionOwner[0] mismatch.");

            Assert.AreEqual("port_tortuga",   deserialized.ports[1].portId,      "portId[1] mismatch.");
            Assert.AreEqual("spanish_crown",  deserialized.ports[1].factionOwner, "factionOwner[1] mismatch.");

            Assert.AreEqual("port_nassau",    deserialized.ports[2].portId,      "portId[2] mismatch.");
            Assert.AreEqual("player_pirates", deserialized.ports[2].factionOwner, "factionOwner[2] mismatch.");
        }

        // ── Test 2 ────────────────────────────────────────────────────────
        /// <summary>
        /// Economy timer must survive a round-trip without drift.
        /// If the timer is lost, players lose accumulated time toward the next income tick
        /// every time the game is saved and loaded.
        /// </summary>
        [Test]
        public void SaveLoad_EconomyState_IncomeTimerPreserved()
        {
            var original = new GameState();
            original.economy.incomeTimer = 47.5f;   // mid-tick timer value

            string json        = JsonUtility.ToJson(original);
            var    deserialized = JsonUtility.FromJson<GameState>(json);

            Assert.IsNotNull(deserialized.economy,
                "Economy save data must not be null after deserialization.");
            Assert.AreEqual(47.5f, deserialized.economy.incomeTimer, 0.0001f,
                "incomeTimer must be preserved exactly across a JSON round-trip.");
        }

        // ── Test 3 ────────────────────────────────────────────────────────
        /// <summary>
        /// A fully-populated GameState (player + ship + ports + economy) must
        /// survive a round-trip with no field corruption in any section.
        /// This guards against future schema changes accidentally dropping fields.
        /// </summary>
        [Test]
        public void SaveLoad_CompleteState_AllSectionsRoundTripCorrectly()
        {
            var original = new GameState
            {
                saveVersion = "1.0",
                timestamp   = "2026-03-16T00:00:00Z",
                player = new PlayerSaveData
                {
                    name      = "BlackbeardJr",
                    gold      = 999f,
                    renown    = 77f,
                    rank      = "admiral",
                    positionX = 12.5f,
                    positionZ = -8.25f,
                    rotationY = 270f,
                },
                playerShip = new ShipSaveData
                {
                    shipClassId  = "frigate",
                    currentHull  = 40,
                    maxHull      = 120,
                },
                economy = new EconomySaveData
                {
                    incomeTimer = 33.3f,
                },
            };
            original.ports.Add(new PortSaveData { portId = "port_a", factionOwner = "player_pirates" });

            string json        = JsonUtility.ToJson(original);
            var    deserialized = JsonUtility.FromJson<GameState>(json);

            // Player section
            Assert.AreEqual("BlackbeardJr", deserialized.player.name,      "player.name mismatch.");
            Assert.AreEqual(999f,           deserialized.player.gold,      "player.gold mismatch.");
            Assert.AreEqual(77f,            deserialized.player.renown,    "player.renown mismatch.");
            Assert.AreEqual(270f,           deserialized.player.rotationY, "player.rotationY mismatch.");

            // Ship section
            Assert.AreEqual("frigate", deserialized.playerShip.shipClassId,  "ship.shipClassId mismatch.");
            Assert.AreEqual(40,        deserialized.playerShip.currentHull,   "ship.currentHull mismatch.");
            Assert.AreEqual(120,       deserialized.playerShip.maxHull,        "ship.maxHull mismatch.");

            // Economy section
            Assert.AreEqual(33.3f, deserialized.economy.incomeTimer, 0.0001f, "economy.incomeTimer mismatch.");

            // Ports section
            Assert.AreEqual(1,                deserialized.ports.Count,                "ports.Count mismatch.");
            Assert.AreEqual("port_a",         deserialized.ports[0].portId,            "ports[0].portId mismatch.");
            Assert.AreEqual("player_pirates", deserialized.ports[0].factionOwner,      "ports[0].factionOwner mismatch.");

            // Metadata
            Assert.AreEqual("1.0",                deserialized.saveVersion, "saveVersion mismatch.");
            Assert.AreEqual("2026-03-16T00:00:00Z", deserialized.timestamp,  "timestamp mismatch.");
        }
    }
}
