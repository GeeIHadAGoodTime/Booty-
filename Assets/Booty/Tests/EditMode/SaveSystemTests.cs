// ---------------------------------------------------------------------------
// SaveSystemTests.cs — EditMode smoke tests for SaveSystem / GameState.
// Verifies default values and JSON round-trip fidelity.
// Does NOT exercise disk I/O — tests are purely in-memory.
// ---------------------------------------------------------------------------

using NUnit.Framework;
using UnityEngine;
using Booty.Save;

namespace Booty.Tests
{
    /// <summary>
    /// Smoke tests for <see cref="GameState"/> JSON serialization round-trips
    /// as performed by <see cref="SaveSystem"/>.
    /// </summary>
    [TestFixture]
    public class SaveSystemTests
    {
        // ── Test 1 ────────────────────────────────────────────────────────
        /// <summary>
        /// A freshly constructed GameState must carry the P1 default gold of 200.
        /// This mirrors SaveSystem.CreateNewState() behaviour.
        /// </summary>
        [Test]
        public void SaveSystem_CreateNewState_HasDefaultGold()
        {
            var state = new GameState();

            Assert.AreEqual(200f, state.player.gold,
                "Default GameState.player.gold must be 200 (P1 starting gold).");
        }

        // ── Test 2 ────────────────────────────────────────────────────────
        /// <summary>
        /// Player fields must survive a JsonUtility.ToJson → FromJson round-trip
        /// with no data loss or mutation.
        /// </summary>
        [Test]
        public void SaveSystem_JsonRoundTrip_PlayerFieldsPreserved()
        {
            var original = new GameState();
            original.player.gold   = 500f;
            original.player.renown = 42f;
            original.player.name   = "TestCaptain";

            string json        = JsonUtility.ToJson(original);
            var    deserialized = JsonUtility.FromJson<GameState>(json);

            Assert.IsNotNull(deserialized, "Deserialized state must not be null.");
            Assert.AreEqual(500f,         deserialized.player.gold,   "gold mismatch after round-trip.");
            Assert.AreEqual(42f,          deserialized.player.renown, "renown mismatch after round-trip.");
            Assert.AreEqual("TestCaptain", deserialized.player.name,  "name mismatch after round-trip.");
        }

        // ── Test 3 ────────────────────────────────────────────────────────
        /// <summary>
        /// Ship fields must survive a round-trip without corruption.
        /// </summary>
        [Test]
        public void SaveSystem_JsonRoundTrip_ShipFieldsPreserved()
        {
            var original = new GameState();
            original.playerShip.shipClassId  = "brigantine";
            original.playerShip.currentHull  = 60;
            original.playerShip.maxHull      = 100;

            string json        = JsonUtility.ToJson(original);
            var    deserialized = JsonUtility.FromJson<GameState>(json);

            Assert.IsNotNull(deserialized, "Deserialized state must not be null.");
            Assert.AreEqual("brigantine", deserialized.playerShip.shipClassId, "shipClassId mismatch.");
            Assert.AreEqual(60,           deserialized.playerShip.currentHull,  "currentHull mismatch.");
            Assert.AreEqual(100,          deserialized.playerShip.maxHull,      "maxHull mismatch.");
        }
    }
}
