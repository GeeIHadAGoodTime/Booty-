// ---------------------------------------------------------------------------
// ReputationManager.cs — Per-faction reputation tracker + relation effects
// ---------------------------------------------------------------------------
// Singleton MonoBehaviour. Created and initialized by BootyBootstrap.
//
// Reputation range: -100 (max hostile) to +100 (max allied), starts at 0.
//
// FactionRelation thresholds (per task spec):
//   Hostile  : rep < -50  — ships attack on sight
//   Neutral  : -50..+50   — ships ignore the player
//   Allied   : rep > +50  — trade discounts + quest access unlocked
//
// Typical rep deltas:
//   Sinking an enemy ship:      -10  (wired in BootyBootstrap + EnemySpawner)
//   Completing a faction quest: +10  (wired via QuestReward.reputationFactionId)
//
// EnemyAI reads:
//   ReputationManager.Instance.IsHostile(factionId)
//   → true  = attack on sight (Hostile)
//   → false = patrol only, never aggro (Neutral or Allied)
// ---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Booty.Faction
{
    // ─────────────────────────────────────────────────────────────────────────
    //  FactionRelation Enum
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Describes the player's standing with a faction, derived from the
    /// current reputation score.
    /// </summary>
    public enum FactionRelation
    {
        /// <summary>
        /// Reputation &lt; -50. Enemy ships belonging to this faction attack the
        /// player on sight.
        /// </summary>
        Hostile,

        /// <summary>
        /// Reputation -50 to +50 (inclusive). Ships patrol and ignore the player.
        /// </summary>
        Neutral,

        /// <summary>
        /// Reputation &gt; +50. Ships ignore the player; trade discounts and
        /// faction-specific quest access are unlocked.
        /// </summary>
        Allied,
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  ReputationManager
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Singleton that tracks the player's per-faction reputation scores and
    /// exposes relation queries used by EnemyAI, Economy, and UI.
    /// </summary>
    public class ReputationManager : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Constants
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Reputation below this value → Hostile relation.</summary>
        public const float HostileThreshold = -50f;

        /// <summary>Reputation above this value → Allied relation.</summary>
        public const float AlliedThreshold  =  50f;

        /// <summary>Lower reputation bound (max hostile).</summary>
        public const float MinReputation    = -100f;

        /// <summary>Upper reputation bound (max allied).</summary>
        public const float MaxReputation    =  100f;

        /// <summary>Proportional trade discount when Allied (10%).</summary>
        public const float AlliedTradeDiscount = 0.10f;

        // ══════════════════════════════════════════════════════════════════
        //  Singleton
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Global singleton access. Set in Awake.</summary>
        public static ReputationManager Instance { get; private set; }

        // ══════════════════════════════════════════════════════════════════
        //  Events
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Fired when a faction's reputation score changes.
        /// Args: factionId, newReputation, delta.
        /// </summary>
        public event Action<string, float, float> OnReputationChanged;

        /// <summary>
        /// Fired when a faction's relation tier crosses a threshold.
        /// Args: factionId, newRelation.
        /// </summary>
        public event Action<string, FactionRelation> OnRelationChanged;

        // ══════════════════════════════════════════════════════════════════
        //  State
        // ══════════════════════════════════════════════════════════════════

        private readonly Dictionary<string, float>          _reputations = new();
        private readonly Dictionary<string, FactionRelation> _relations  = new();
        private readonly Dictionary<string, FactionDataSO>  _factionData = new();

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Public API — Initialization
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Load faction definitions and set starting reputations.
        /// Called by BootyBootstrap during scene setup.
        /// </summary>
        /// <param name="factionAssets">
        /// List of <see cref="FactionDataSO"/> assets (one per faction).
        /// Null entries or assets with empty factionId are skipped.
        /// </param>
        public void Initialize(System.Collections.Generic.IEnumerable<FactionDataSO> factionAssets)
        {
            _reputations.Clear();
            _relations.Clear();
            _factionData.Clear();

            if (factionAssets == null)
            {
                Debug.LogWarning("[ReputationManager] No faction assets provided — " +
                                 "reputation system will have empty faction set.");
                return;
            }

            foreach (var asset in factionAssets)
            {
                if (asset == null || string.IsNullOrEmpty(asset.factionId))
                    continue;

                if (_factionData.ContainsKey(asset.factionId))
                {
                    Debug.LogWarning($"[ReputationManager] Duplicate factionId '{asset.factionId}' — skipping.");
                    continue;
                }

                float startRep = Mathf.Clamp(asset.startingReputation, MinReputation, MaxReputation);
                _factionData[asset.factionId]  = asset;
                _reputations[asset.factionId]  = startRep;
                _relations[asset.factionId]    = CalculateRelation(startRep);
            }

            if (_factionData.Count > 0)
            {
                Debug.Log($"[ReputationManager] Initialized {_factionData.Count} faction(s): " +
                          string.Join(", ", _factionData.Keys));
            }
            else
            {
                Debug.LogWarning("[ReputationManager] Initialized with zero factions. " +
                                 "Assign FactionDataSO assets to BootyBootstrap.factionAssets.");
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Public API — Queries
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Get the current reputation score (-100 to +100) for a faction.
        /// Returns 0 (neutral score) for unregistered factions.
        /// </summary>
        /// <param name="factionId">Faction ID to query.</param>
        /// <returns>Reputation value, or 0 if factionId is unknown.</returns>
        public float GetReputation(string factionId)
        {
            if (string.IsNullOrEmpty(factionId)) return 0f;
            return _reputations.TryGetValue(factionId, out var rep) ? rep : 0f;
        }

        /// <summary>
        /// Get the player's current <see cref="FactionRelation"/> with a faction.
        /// <para>
        /// Empty factionId → <see cref="FactionRelation.Hostile"/> (generic enemies
        /// with no allegiance always attack).
        /// Unknown factionId → <see cref="FactionRelation.Neutral"/>.
        /// </para>
        /// </summary>
        public FactionRelation GetRelation(string factionId)
        {
            // Empty = no allegiance. Treat as always-hostile so untagged enemies
            // keep their default aggressive behaviour.
            if (string.IsNullOrEmpty(factionId))
                return FactionRelation.Hostile;

            return _relations.TryGetValue(factionId, out var rel) ? rel : FactionRelation.Neutral;
        }

        /// <summary>True when reputation with <paramref name="factionId"/> is below -50.</summary>
        public bool IsHostile(string factionId) => GetRelation(factionId) == FactionRelation.Hostile;

        /// <summary>True when reputation with <paramref name="factionId"/> is above +50.</summary>
        public bool IsAllied(string factionId)  => GetRelation(factionId) == FactionRelation.Allied;

        /// <summary>
        /// Trade discount for a faction. Returns 10% when Allied, 0% otherwise.
        /// Used by Economy/TradeManager for port pricing.
        /// </summary>
        public float GetTradeDiscount(string factionId)
            => IsAllied(factionId) ? AlliedTradeDiscount : 0f;

        /// <summary>
        /// Get the <see cref="FactionDataSO"/> for a faction, or null if not registered.
        /// </summary>
        public FactionDataSO GetFactionData(string factionId)
        {
            _factionData.TryGetValue(factionId, out var data);
            return data;
        }

        /// <summary>All faction IDs currently tracked.</summary>
        public IEnumerable<string> GetAllFactionIds() => _factionData.Keys;

        // ══════════════════════════════════════════════════════════════════
        //  Public API — Mutation
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Modify the player's reputation with a faction by <paramref name="delta"/>.
        /// Clamped to [-100, +100]. Fires <see cref="OnReputationChanged"/> and
        /// <see cref="OnRelationChanged"/> (the latter only when the tier changes).
        /// </summary>
        /// <param name="factionId">Target faction ID.</param>
        /// <param name="delta">Positive = friendlier, negative = more hostile.</param>
        public void ModifyReputation(string factionId, float delta)
        {
            if (string.IsNullOrEmpty(factionId) || delta == 0f)
                return;

            // Unknown factions are tracked dynamically (start at 0 / Neutral)
            if (!_reputations.ContainsKey(factionId))
            {
                _reputations[factionId] = 0f;
                _relations[factionId]   = FactionRelation.Neutral;
            }

            float oldRep = _reputations[factionId];
            float newRep = Mathf.Clamp(oldRep + delta, MinReputation, MaxReputation);
            _reputations[factionId] = newRep;

            FactionRelation oldRelation = _relations[factionId];
            FactionRelation newRelation = CalculateRelation(newRep);
            _relations[factionId] = newRelation;

            float actualDelta = newRep - oldRep;
            if (Mathf.Abs(actualDelta) < 0.001f) return;

            Debug.Log($"[ReputationManager] {factionId}: rep {oldRep:+0;-0} → {newRep:+0;-0} " +
                      $"(Δ{actualDelta:+0;-0}) | {newRelation}");

            OnReputationChanged?.Invoke(factionId, newRep, actualDelta);

            if (newRelation != oldRelation)
            {
                Debug.Log($"[ReputationManager] {factionId} relation: {oldRelation} → {newRelation}");
                OnRelationChanged?.Invoke(factionId, newRelation);
            }
        }

        /// <summary>
        /// Force-set a faction's reputation to an exact value.
        /// Useful for save/restore and debug console.
        /// </summary>
        /// <param name="factionId">Target faction ID.</param>
        /// <param name="value">New reputation value (clamped to [-100, +100]).</param>
        public void SetReputation(string factionId, float value)
        {
            float clamped = Mathf.Clamp(value, MinReputation, MaxReputation);
            float current = GetReputation(factionId);
            ModifyReputation(factionId, clamped - current);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Internal Helpers
        // ══════════════════════════════════════════════════════════════════

        private static FactionRelation CalculateRelation(float rep)
        {
            if (rep > AlliedThreshold)  return FactionRelation.Allied;
            if (rep < HostileThreshold) return FactionRelation.Hostile;
            return FactionRelation.Neutral;
        }
    }
}
