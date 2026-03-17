// ---------------------------------------------------------------------------
// EconomyData.cs — Canonical C# representation of PRD Appendix A6 (Economy)
// ---------------------------------------------------------------------------
// Maps directly to the economy JSON schema (global knobs).
// ---------------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Booty.Config
{
    /// <summary>
    /// Global economy configuration matching Master PRD Appendix A6.
    /// Loaded once from JSON config; provides goods list and event definitions.
    /// </summary>
    [Serializable]
    public class EconomyData
    {
        /// <summary>
        /// Master list of tradeable good identifiers.
        /// P1 uses base_price only; supply/demand comes in Pre-Beta.
        /// </summary>
        public List<string> goods;

        /// <summary>
        /// World event identifiers (supply shocks, booms, famine, etc.).
        /// Alpha+: read-only scaffolding in P1.
        /// </summary>
        public List<string> worldEvents;

        // ── Extension hook ──────────────────────────────────────────────
        public Dictionary<string, string> meta;
    }
}
