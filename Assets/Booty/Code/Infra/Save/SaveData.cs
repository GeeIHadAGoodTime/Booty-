// ---------------------------------------------------------------------------
// SaveData.cs — Comprehensive serializable save record for multi-slot persistence
// ---------------------------------------------------------------------------
// Used exclusively by SaveManager for multi-slot Save(slot)/Load(slot) API.
// The legacy GameState / SaveSystem remain active for existing system bindings.
//
// Captured fields per task spec S2-SAVE:
//   - Player gold, ship tier, crew count (reserved), current port, discovered ports
//   - Cargo inventory: list of (goodsId, quantity) pairs
//   - Quest progress: per-quest status + per-objective progress counts
//
// Namespace: Booty.Save
// ---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Booty.Save
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Top-level save document
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Full serializable snapshot of game state. One document per save slot.
    /// JsonUtility serialises this to/from Application.persistentDataPath.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        // ── Metadata ──────────────────────────────────────────────────────────

        /// <summary>Schema version — increment if save format changes.</summary>
        public string saveVersion = "1.0";

        /// <summary>ISO-8601 UTC timestamp written at save time.</summary>
        public string timestamp = "";

        /// <summary>Which slot (0 = auto-save, 1-3 = manual) this data was written to.</summary>
        public int slotIndex = 0;

        // ── Player ────────────────────────────────────────────────────────────

        /// <summary>Player gold at the time of save. Source: EconomySystem.Gold.</summary>
        public float gold = 200f;

        /// <summary>
        /// Ship class / tier identifier (e.g. "sloop", "brigantine", "frigate").
        /// Source: ShipSaveData.shipClassId in the legacy SaveSystem.
        /// </summary>
        public string shipTierId = "sloop";

        /// <summary>
        /// Crew count. Reserved for the future crew system — always 0 in S2.
        /// </summary>
        public int crewCount = 0;

        /// <summary>ID of the last port the player visited or captured.</summary>
        public string currentPortId = "";

        /// <summary>IDs of all ports the player has visited or captured.</summary>
        public List<string> discoveredPorts = new List<string>();

        // ── Cargo hold ────────────────────────────────────────────────────────

        /// <summary>
        /// Contents of the player's cargo hold at save time.
        /// Each entry pairs a GoodsData.goodsId with a quantity.
        /// Restored by resolving GoodsData via Resources.FindObjectsOfTypeAll.
        /// </summary>
        public List<CargoSaveEntry> cargo = new List<CargoSaveEntry>();

        // ── Quests ────────────────────────────────────────────────────────────

        /// <summary>
        /// Per-quest status and objective progress at save time.
        /// Indexed by QuestData.questId. Restored by SaveManager into QuestManager.
        /// </summary>
        public List<QuestSaveEntry> quests = new List<QuestSaveEntry>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Cargo
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// One cargo hold entry: a trade good (by ID) and how many units the player holds.
    /// The string ID matches GoodsData.goodsId (e.g. "rum", "gunpowder").
    /// </summary>
    [Serializable]
    public class CargoSaveEntry
    {
        /// <summary>Matches GoodsData.goodsId — used to resolve the ScriptableObject at load time.</summary>
        public string goodsId = "";

        /// <summary>Number of units held in the cargo hold.</summary>
        public int quantity = 0;

        public CargoSaveEntry() { }

        public CargoSaveEntry(string id, int qty)
        {
            goodsId  = id;
            quantity = qty;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Quests
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Save record for one quest instance.
    /// statusInt maps directly to the QuestStatus enum (int cast).
    /// objectiveCounts is parallel to QuestData.objectives — index N holds the
    /// CurrentCount for objective N.
    /// </summary>
    [Serializable]
    public class QuestSaveEntry
    {
        /// <summary>Matches QuestData.questId.</summary>
        public string questId = "";

        /// <summary>
        /// Cast of QuestStatus enum:
        ///   0 = Locked, 1 = Available, 2 = Active, 3 = Completed, 4 = Failed
        /// </summary>
        public int statusInt = 1;   // default: Available

        /// <summary>Seconds elapsed for timed quests. 0 for non-timed quests.</summary>
        public float elapsedSeconds = 0f;

        /// <summary>
        /// Per-objective progress counters, parallel to QuestData.objectives list.
        /// Count at index N = QuestObjectiveProgress.CurrentCount for objective N.
        /// </summary>
        public List<int> objectiveCounts = new List<int>();

        public QuestSaveEntry() { }

        public QuestSaveEntry(string id, int status, float elapsed, List<int> counts)
        {
            questId        = id;
            statusInt      = status;
            elapsedSeconds = elapsed;
            objectiveCounts = counts ?? new List<int>();
        }
    }
}
