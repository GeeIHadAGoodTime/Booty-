// ---------------------------------------------------------------------------
// ShipData.cs — Canonical C# representation of PRD Appendix A5 (Ship Schema)
// ---------------------------------------------------------------------------
// Maps directly to the ship JSON schema. Field names and semantics match the
// PRD. Fields marked with phase comments are present as read-only scaffolding
// and must not have gameplay logic in P1.
// ---------------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Booty.Config
{
    /// <summary>
    /// Canonical ship configuration matching Master PRD Appendix A5.
    /// One instance per ship archetype loaded from JSON config.
    /// </summary>
    [Serializable]
    public class ShipData
    {
        // ── Identity ────────────────────────────────────────────────────
        public string id;
        public string name;
        public string shipClass;         // "sloop", "brig", "frigate", etc.

        // ── Combat / Structure ──────────────────────────────────────────
        public int hull;                 // hit points / structural integrity
        public int sail;                 // sail health (optional damage in P1)
        public float speed;              // base speed scalar on 2D nav plane
        public float turnRate;           // turning agility (degrees/sec)
        public int broadsideSlots;       // cannon slots per side

        // ── Ammo (Alpha+) — present but inactive in P1 ─────────────────
        public List<string> ammoTypes;   // ammo_id list

        // ── Crew (abstracted in P1) ─────────────────────────────────────
        public int crewMin;
        public int crewOptimal;

        // ── Progression & Economy ───────────────────────────────────────
        public int value;                // purchase / sell value in gold
        public int tier;                 // rough power tier for progression

        // ── Extension hook ──────────────────────────────────────────────
        // Free-form dictionary for DLC / expansion data.
        // P1: not read at runtime.
        public Dictionary<string, string> meta;
    }
}
