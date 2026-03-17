// ---------------------------------------------------------------------------
// PortEconomy.cs — ScriptableObject: per-port supply/demand configuration
// ---------------------------------------------------------------------------
// Create via the Unity menu:
//   Assets → Create → Booty → Economy → PortEconomy
//
// Each port gets one PortEconomy asset. It lists which goods the port
// trades, their starting supply levels, and how fast prices drift over time.
// TradeManager reads these assets at runtime to compute buy/sell prices.
// ---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Booty.Economy
{
    /// <summary>
    /// Mutable runtime entry for a single good inside a port economy.
    /// The supply level drives the price multiplier (low supply → high price).
    /// </summary>
    [Serializable]
    public class PortGoodsEntry
    {
        [Tooltip("The trade good this entry describes.")]
        public GoodsData goods;

        [Tooltip("Starting supply level. 0 = completely out of stock (price high), " +
                 "1 = oversupplied (price low).")]
        [Range(0f, 1f)]
        public float supplyLevel = 0.5f;

        [Tooltip("Extra demand at this port for this good. Values > 1 raise buy/sell " +
                 "prices above the supply-curve baseline.")]
        [Range(0.5f, 3f)]
        public float demandMultiplier = 1f;

        [Tooltip("How fast supply recovers towards its baseline per in-game minute.")]
        [Range(0.001f, 0.1f)]
        public float supplyRecoveryRate = 0.01f;

        // ── Runtime (not serialised to the asset) ────────────────────────
        [NonSerialized] public float RuntimeSupplyLevel;
        [NonSerialized] public bool  RuntimeInitialised;
    }

    /// <summary>
    /// ScriptableObject that defines a port's trade economy.
    /// Holds the list of goods traded, their supply/demand parameters,
    /// and the price drift configuration used by <see cref="TradeManager"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "NewPortEconomy", menuName = "Booty/Economy/PortEconomy")]
    public class PortEconomy : ScriptableObject
    {
        // ══════════════════════════════════════════════════════════════════
        //  Port Identity
        // ══════════════════════════════════════════════════════════════════

        [Header("Port Identity")]
        [Tooltip("Must match the portId used by PortSystem (e.g. 'port_nassau').")]
        public string portId;

        [Tooltip("Human-readable port name shown in the trade UI.")]
        public string portDisplayName;

        [Tooltip("Flavour description of the port's economy.")]
        [TextArea(2, 3)]
        public string economyDescription;

        // ══════════════════════════════════════════════════════════════════
        //  Trade Goods
        // ══════════════════════════════════════════════════════════════════

        [Header("Available Trade Goods")]
        [Tooltip("All goods available for trade at this port.")]
        public List<PortGoodsEntry> goods = new List<PortGoodsEntry>();

        // ══════════════════════════════════════════════════════════════════
        //  Price Drift
        // ══════════════════════════════════════════════════════════════════

        [Header("Price Drift Settings")]
        [Tooltip("Seconds of real time between supply/demand drift ticks.")]
        [Min(1f)]
        public float driftIntervalSeconds = 30f;

        [Tooltip("Maximum random drift applied per tick to each good's supply level. " +
                 "Keeps prices feeling alive even when no trades happen.")]
        [Range(0f, 0.1f)]
        public float randomDriftMagnitude = 0.02f;

        // ══════════════════════════════════════════════════════════════════
        //  Runtime Helpers
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Initialise runtime supply levels from the serialised baseline.
        /// Call once when the port first enters play (via TradeManager.RegisterPort).
        /// </summary>
        public void InitialiseRuntime()
        {
            foreach (var entry in goods)
            {
                if (entry != null && !entry.RuntimeInitialised)
                {
                    entry.RuntimeSupplyLevel  = entry.supplyLevel;
                    entry.RuntimeInitialised  = true;
                }
            }
        }

        /// <summary>
        /// Returns the goods entry for a specific GoodsData asset, or null.
        /// </summary>
        public PortGoodsEntry FindEntry(GoodsData target)
        {
            if (target == null) return null;
            foreach (var entry in goods)
            {
                if (entry != null && entry.goods == target)
                    return entry;
            }
            return null;
        }

        /// <summary>
        /// Compute the price multiplier for a good based on its current supply
        /// and demand. Price multiplier formula:
        ///   multiplier = demandMultiplier / max(0.1, supplyLevel)
        /// Clamped to [0.25, 4.0] to prevent extreme prices.
        /// </summary>
        /// <param name="entry">The runtime goods entry.</param>
        /// <returns>Price multiplier (1.0 = exactly base value).</returns>
        public static float ComputePriceMultiplier(PortGoodsEntry entry)
        {
            if (entry == null) return 1f;

            float supply = entry.RuntimeInitialised
                ? entry.RuntimeSupplyLevel
                : entry.supplyLevel;

            float raw = entry.demandMultiplier / Mathf.Max(0.1f, supply);
            return Mathf.Clamp(raw, 0.25f, 4f);
        }

        /// <summary>
        /// Apply one supply-drift tick. Drifts supply toward baseline and adds
        /// random noise. Called by TradeManager every <see cref="driftIntervalSeconds"/>.
        /// </summary>
        public void ApplyDriftTick()
        {
            foreach (var entry in goods)
            {
                if (entry == null) continue;

                // Recover toward baseline
                float baseline = entry.supplyLevel;
                entry.RuntimeSupplyLevel = Mathf.MoveTowards(
                    entry.RuntimeSupplyLevel,
                    baseline,
                    entry.supplyRecoveryRate);

                // Random drift noise
                float noise = UnityEngine.Random.Range(-randomDriftMagnitude, randomDriftMagnitude);
                entry.RuntimeSupplyLevel = Mathf.Clamp01(entry.RuntimeSupplyLevel + noise);
            }
        }
    }
}
