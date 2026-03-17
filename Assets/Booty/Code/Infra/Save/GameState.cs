using System;
using System.Collections.Generic;
using UnityEngine;

namespace Booty.Save
{
    /// <summary>
    /// Serializable snapshot of the full game state. This is the single top-level
    /// save struct as defined by the Implementation Topology (section 4.3).
    /// All fields map to the canonical PRD schemas in Appendix A.
    /// </summary>
    [Serializable]
    public class GameState
    {
        // ---- Save metadata ----
        public string saveVersion = "1.0";
        public string timestamp = "";

        // ---- Player state ----
        public PlayerSaveData player = new PlayerSaveData();

        // ---- Port ownership and state ----
        public List<PortSaveData> ports = new List<PortSaveData>();

        // ---- Active ship data ----
        public ShipSaveData playerShip = new ShipSaveData();

        // ---- Economy state ----
        public EconomySaveData economy = new EconomySaveData();

        // ---- Captured port IDs (player-owned ports) ----
        public List<string> capturedPortIds = new List<string>();

        // ---- World state ----
        public int difficultyLevel = 0;
        public int enemySpawnSeed = 0;
    }

    /// <summary>
    /// Player-specific save data. Maps to PRD Appendix A3 (Player Progression Schema).
    /// Fields marked as inactive for P1 are present but unused by gameplay logic.
    /// </summary>
    [Serializable]
    public class PlayerSaveData
    {
        public string name = "Captain";
        public float gold = 200f;
        public float renown = 0f;
        public string rank = "nobody"; // read-only scaffolding in P1
        public float positionX = 0f;
        public float positionZ = 0f;
        public float rotationY = 0f;
        public int currentHP = 0;
        public int maxHP = 80;
    }

    /// <summary>
    /// Per-port save data. Maps to PRD Appendix A1 (Port Schema).
    /// Only the mutable runtime state is saved; static config comes from ports.json.
    /// </summary>
    [Serializable]
    public class PortSaveData
    {
        public string portId = "";
        public string factionOwner = "";
    }

    /// <summary>
    /// Player ship save data. Maps to PRD Appendix A5 (Ship Schema).
    /// </summary>
    [Serializable]
    public class ShipSaveData
    {
        public string shipClassId = "sloop";
        public int currentHull = 80;
        public int maxHull = 80;
    }

    /// <summary>
    /// Economy snapshot. Tracks accumulated income timers.
    /// Maps to PRD Appendix A6 (Economy Schema) -- minimal for P1.
    /// </summary>
    [Serializable]
    public class EconomySaveData
    {
        public float incomeTimer = 0f;
    }
}
