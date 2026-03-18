// ---------------------------------------------------------------------------
// FactionDataSO.cs — ScriptableObject: static faction identity + colour
// ---------------------------------------------------------------------------
// Create via:  Assets → Create → Booty → Faction Data
// Assign the 4 faction assets to BootyBootstrap.factionAssets in the Inspector.
//
// factionId must match the strings used in:
//   - ports.json  (faction_owner)
//   - EnemyMetadata.sourceFaction  (set by EnemySpawner from port.factionOwner)
//
// Canonical IDs (keep in sync with factions.json):
//   "british_crown"  — The British Crown
//   "spanish_crown"  — The Spanish Crown
//   "french_crown"   — The French Crown
//   "npc_pirates"    — NPC Pirate factions (not the player)
// ---------------------------------------------------------------------------

using UnityEngine;

namespace Booty.Faction
{
    /// <summary>
    /// Immutable faction definition: identity, display name, colour, and
    /// optional flag icon. Runtime reputation is managed separately by
    /// <see cref="ReputationManager"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "FactionData", menuName = "Booty/Faction Data")]
    public class FactionDataSO : ScriptableObject
    {
        // ── Identity ─────────────────────────────────────────────────────

        [Header("Identity")]
        [Tooltip("Machine-readable ID. Must match PortRuntimeData.factionOwner and\n" +
                 "EnemyMetadata.sourceFaction. E.g. 'british_crown', 'spanish_crown'.")]
        public string factionId = "";

        [Tooltip("Human-readable name shown in UI (e.g. 'The British Crown').")]
        public string factionName = "Unknown Faction";

        // ── Visuals ───────────────────────────────────────────────────────

        [Header("Visuals")]
        [Tooltip("Representative colour for map markers, flag tinting, and UI elements.")]
        public Color factionColor = Color.white;

        [Tooltip("Flag or crest icon Sprite. Assign in Inspector. May be null during early dev.")]
        public Sprite flagIcon;

        // ── Starting Reputation ───────────────────────────────────────────

        [Header("Starting Reputation")]
        [Tooltip("Initial player reputation with this faction.\n" +
                 "Range: -100 (max hostile) to +100 (max allied). 0 = neutral.")]
        [Range(-100f, 100f)]
        public float startingReputation = 0f;
    }
}
