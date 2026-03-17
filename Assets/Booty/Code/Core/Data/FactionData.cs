// ---------------------------------------------------------------------------
// FactionData.cs — Canonical C# representation of PRD Appendix A2 (Faction)
// ---------------------------------------------------------------------------
// Maps directly to the faction JSON schema.
// ---------------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Booty.Config
{
    /// <summary>
    /// Canonical faction configuration matching Master PRD Appendix A2.
    /// </summary>
    [Serializable]
    public class FactionData
    {
        // ── Identity ────────────────────────────────────────────────────
        public string id;
        public string name;

        /// <summary>RGB colour used for map markers, flags, UI tinting.</summary>
        public int[] color = new int[3];

        // ── Relations (Alpha+) — read-only scaffolding in P1 ───────────
        /// <summary>Attitude toward other factions. Key = faction_id, -100..+100.</summary>
        public Dictionary<string, float> attitude;

        /// <summary>Pre-Beta+: war score per opposing faction.</summary>
        public Dictionary<string, float> warScore;

        /// <summary>
        /// War state per opposing faction.
        /// Values: "peace" | "truce" | "war".
        /// Alpha+: toggled via scripts. P1: static backdrop.
        /// </summary>
        public Dictionary<string, string> warState;

        // ── Diplomacy Flags ─────────────────────────────────────────────
        public DiplomacyFlags diplomacyFlags;

        // ── Extension hook ──────────────────────────────────────────────
        public Dictionary<string, string> meta;
    }

    /// <summary>
    /// Diplomacy flag block for a faction (subset of Appendix A2).
    /// </summary>
    [Serializable]
    public class DiplomacyFlags
    {
        public bool isMajor;
        public bool isPirate;
        public bool recognizesPlayerKingdom;
    }
}
