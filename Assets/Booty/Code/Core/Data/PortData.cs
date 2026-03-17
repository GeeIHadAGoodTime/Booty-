// ---------------------------------------------------------------------------
// PortData.cs — Canonical C# representation of PRD Appendix A1 (Port Schema)
// ---------------------------------------------------------------------------
// Maps directly to the port JSON schema. Fields marked with later-phase
// comments are present as read-only scaffolding for P1.
// ---------------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Booty.Config
{
    /// <summary>
    /// Canonical port configuration matching Master PRD Appendix A1.
    /// One instance per port loaded from JSON config.
    /// </summary>
    [Serializable]
    public class PortData
    {
        // ── Identity ────────────────────────────────────────────────────
        public string id;
        public string name;
        public string regionId;
        public string factionOwner;      // faction_id of current owner

        // ── Economic (P1: base_price only) ──────────────────────────────
        /// <summary>Per-good base prices. Key = good_id, Value = price.</summary>
        public Dictionary<string, float> basePrice;

        /// <summary>Alpha+: local price modifiers. Read-only scaffolding in P1.</summary>
        public Dictionary<string, float> priceModLocal;

        /// <summary>Alpha+: event-driven price modifiers. Read-only scaffolding in P1.</summary>
        public Dictionary<string, float> priceModEvent;

        /// <summary>Pre-Beta+: supply levels. Read-only scaffolding in P1.</summary>
        public Dictionary<string, float> supply;

        /// <summary>Pre-Beta+: demand levels. Read-only scaffolding in P1.</summary>
        public Dictionary<string, float> demand;

        // ── Governance ──────────────────────────────────────────────────
        /// <summary>Port level 1–5. Alpha+; may be fixed at 1 in P1.</summary>
        public int level = 1;

        /// <summary>Tax policy: "low" | "med" | "high". Pre-Beta+; read-only in P1.</summary>
        public string taxPolicy = "med";

        /// <summary>Stability 0–100. Pre-Beta+; read-only in P1.</summary>
        public float stability = 100f;

        // ── Military ────────────────────────────────────────────────────
        /// <summary>Defense rating — drives garrison strength in port battles.</summary>
        public float defenseRating;

        // ── Income (P1 core) ────────────────────────────────────────────
        /// <summary>Gold generated per income tick when player-owned.</summary>
        public float baseIncome;

        // ── Extension hook ──────────────────────────────────────────────
        public Dictionary<string, string> meta;
    }
}
