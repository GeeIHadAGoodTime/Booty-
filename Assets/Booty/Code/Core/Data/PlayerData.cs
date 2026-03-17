// ---------------------------------------------------------------------------
// PlayerData.cs — Canonical C# representation of PRD Appendix A3 (Player)
// ---------------------------------------------------------------------------
// Maps directly to the player progression JSON schema.
// ---------------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Booty.Config
{
    /// <summary>
    /// Canonical player progression data matching Master PRD Appendix A3.
    /// Persisted as part of the save file.
    /// </summary>
    [Serializable]
    public class PlayerData
    {
        // ── Progression (P1 core) ───────────────────────────────────────
        /// <summary>Fame / notoriety scalar. Increases from combat and port capture.</summary>
        public float renown;

        /// <summary>Rank enum string: "nobody" | "officer" | "noble" | "lord".</summary>
        public string rank = "nobody";

        /// <summary>Alpha+: per-faction reputation. Key = faction_id, -100..+100.</summary>
        public Dictionary<string, float> factionRep;

        // ── Vassalage (Alpha+) — null / inactive in P1 ─────────────────
        public VassalageContract vassalageContract;

        // ── Kingdom (Pre-Beta+) — read-only scaffolding in P1 ──────────
        public KingdomData kingdom;

        // ── Economy (P1 core) ───────────────────────────────────────────
        /// <summary>Current gold held by the player.</summary>
        public int gold;

        // ── Extension hook ──────────────────────────────────────────────
        public Dictionary<string, string> meta;
    }

    /// <summary>
    /// Vassalage contract block (Appendix A3). Null in P1.
    /// </summary>
    [Serializable]
    public class VassalageContract
    {
        public string factionId;
        public int stipend;
        public string fiefPortId;        // nullable
        public bool obligationsActive;
    }

    /// <summary>
    /// Kingdom state block (Appendix A3). Read-only scaffolding in P1.
    /// </summary>
    [Serializable]
    public class KingdomData
    {
        public bool isIndependent;
        public List<string> portsOwned;
        public float legitimacy;         // 0–100, meaningful in Beta
    }
}
