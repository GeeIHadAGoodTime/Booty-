// ---------------------------------------------------------------------------
// LootTable.cs — Tiered loot configuration for defeated enemy ships
// ---------------------------------------------------------------------------
// ScriptableObject: assign to the Inspector field on BootyBootstrap or
// AI.EnemySpawner. If no asset is assigned, use LootTable.RollDefault()
// which reads from the built-in Caribbean loot table.
//
// Tiers:
//   Tier 1 — Sloop    (small pirates, low reward)
//   Tier 2 — Brig     (military escort, moderate reward)
//   Tier 3 — Galleon  (treasure fleet, high reward + ship capture chance)
//
// Usage:
//   var result = lootTable.Roll(enemyTier, difficultyMultiplier);
//   economySystem.AddGold(result.Gold);
//   foreach (var cargo in result.Cargo) { ... add to CargoInventory ... }
//   if (result.ShipCaptureOffered) { ... show capture prompt ... }
//
// Cargo IDs in LootCargoEntry match GoodsData.goodsId:
//   "rum", "sugar", "spices", "silk", "gunpowder", "timber"
// ---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Booty.Combat
{
    // ══════════════════════════════════════════════════════════════════════
    //  Result Type
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A single cargo type yielded by a loot roll.
    /// The <c>goodsId</c> string matches <c>GoodsData.goodsId</c> in the Economy system.
    /// </summary>
    [Serializable]
    public struct LootCargoEntry
    {
        /// <summary>Goods identifier (e.g. "rum", "spices").</summary>
        public string goodsId;

        /// <summary>Number of units of this good.</summary>
        public int quantity;
    }

    /// <summary>
    /// Complete loot result from a single roll: gold, cargo, and capture flag.
    /// </summary>
    public class LootResult
    {
        /// <summary>Gold coins awarded to the player.</summary>
        public int Gold;

        /// <summary>
        /// Cargo items dropped around the ship's death position.
        /// Resolve <c>goodsId</c> strings against <c>GoodsData</c> assets when adding
        /// to <c>CargoInventory</c>.
        /// </summary>
        public List<LootCargoEntry> Cargo = new List<LootCargoEntry>();

        /// <summary>
        /// True when the player may attempt to claim this ship as a prize.
        /// Display a capture popup when this flag is set.
        /// </summary>
        public bool ShipCaptureOffered;

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder($"LootResult [{Gold}g");
            foreach (var c in Cargo)
                sb.Append($", {c.quantity}× {c.goodsId}");
            if (ShipCaptureOffered) sb.Append(", SHIP CAPTURE");
            sb.Append("]");
            return sb.ToString();
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Configuration Types (Inspector-visible)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Loot configuration for a single enemy tier (1–3).
    /// </summary>
    [Serializable]
    public class LootTierEntry
    {
        [Tooltip("Enemy tier this entry applies to (1 = sloop, 2 = brig, 3 = galleon).")]
        [Range(1, 3)] public int tier = 1;

        [Header("Gold")]
        [Tooltip("Minimum gold before difficulty scaling.")]
        [Min(0)] public int goldMin = 40;
        [Tooltip("Maximum gold before difficulty scaling.")]
        [Min(0)] public int goldMax = 100;

        [Header("Cargo Pools")]
        [Tooltip("Each pool entry is rolled independently. " +
                 "Set dropChance to control how often each good appears.")]
        public List<LootCargoPool> cargoPools = new List<LootCargoPool>();

        [Header("Ship Capture")]
        [Tooltip("Probability (0–1) that a capture opportunity is offered " +
                 "after defeating this ship tier.")]
        [Range(0f, 1f)] public float shipCaptureChance = 0.05f;
    }

    /// <summary>
    /// A single cargo good entry in a <see cref="LootTierEntry"/> pool.
    /// </summary>
    [Serializable]
    public class LootCargoPool
    {
        [Tooltip("Goods ID to drop. Must match a GoodsData.goodsId in the Economy system " +
                 "(rum, sugar, spices, silk, gunpowder, timber).")]
        public string goodsId = "rum";

        [Tooltip("Minimum units dropped when this pool entry is selected.")]
        [Min(0)] public int quantityMin = 1;
        [Tooltip("Maximum units dropped when this pool entry is selected.")]
        [Min(0)] public int quantityMax = 4;

        [Tooltip("Probability (0–1) this entry appears in any given loot roll.")]
        [Range(0f, 1f)] public float dropChance = 0.40f;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  LootTable ScriptableObject
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ScriptableObject defining gold, cargo, and ship-capture rewards for each
    /// enemy tier. Assign to <c>BootyBootstrap.lootTableAsset</c> in the Inspector.
    /// <para>
    /// If no asset is assigned, call <see cref="RollDefault"/> for built-in
    /// Caribbean defaults — no ScriptableObject required.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "LootTable", menuName = "Booty/Combat/LootTable")]
    public class LootTable : ScriptableObject
    {
        [Header("Tier Entries")]
        [Tooltip("One entry per enemy tier (1, 2, 3). Add all three for full coverage.")]
        [SerializeField] private List<LootTierEntry> tiers = new List<LootTierEntry>();

        // ── Unity Lifecycle ────────────────────────────────────────────

        private void OnEnable()
        {
            // Auto-fill with defaults if the list is empty (e.g. newly created asset)
            if (tiers == null || tiers.Count == 0)
                tiers = BuildDefaultTiers();
        }

        // ══════════════════════════════════════════════════════════════════
        //  Instance API
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Roll loot for an enemy of the specified tier.
        /// </summary>
        /// <param name="enemyTier">1 = sloop, 2 = brig, 3 = galleon.</param>
        /// <param name="difficultyMult">
        /// Multiplier applied to the gold result (from DifficultyManager).
        /// Default 1.0 = no scaling.
        /// </param>
        /// <returns>A <see cref="LootResult"/> with gold, cargo entries, and capture flag.</returns>
        public LootResult Roll(int enemyTier, float difficultyMult = 1f)
        {
            var entry = FindEntry(enemyTier);
            return RollEntry(entry, difficultyMult);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Static Fallback API
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Roll loot using built-in defaults — no ScriptableObject asset required.
        /// Use this when <c>lootTableAsset</c> is null in BootyBootstrap.
        /// </summary>
        /// <param name="enemyTier">1, 2, or 3.</param>
        /// <param name="difficultyMult">Gold multiplier from difficulty settings.</param>
        public static LootResult RollDefault(int enemyTier, float difficultyMult = 1f)
        {
            var defaults = BuildDefaultTiers();
            LootTierEntry entry = null;
            foreach (var t in defaults)
            {
                if (t.tier == enemyTier)
                {
                    entry = t;
                    break;
                }
            }
            // Fallback to tier 1 if the requested tier is not found
            if (entry == null) entry = defaults[0];
            return RollEntry(entry, difficultyMult);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Internals
        // ══════════════════════════════════════════════════════════════════

        private LootTierEntry FindEntry(int tier)
        {
            LootTierEntry best = null;
            foreach (var e in tiers)
            {
                if (e.tier == tier) return e; // exact match
                if (best == null)  best = e;  // keep any as fallback
            }
            // No tier list at all — synthesize from defaults
            return best ?? BuildDefaultTiers()[0];
        }

        private static LootResult RollEntry(LootTierEntry entry, float difficultyMult)
        {
            var result = new LootResult();

            // ── Gold ──────────────────────────────────────────────────
            int rawGold = UnityEngine.Random.Range(entry.goldMin, entry.goldMax + 1);
            result.Gold = Mathf.RoundToInt(rawGold * Mathf.Max(0f, difficultyMult));

            // ── Cargo ─────────────────────────────────────────────────
            foreach (var pool in entry.cargoPools)
            {
                if (UnityEngine.Random.value > pool.dropChance) continue;

                int qty = UnityEngine.Random.Range(pool.quantityMin,
                                                    Mathf.Max(pool.quantityMin, pool.quantityMax) + 1);
                if (qty > 0)
                {
                    result.Cargo.Add(new LootCargoEntry
                    {
                        goodsId  = pool.goodsId,
                        quantity = qty,
                    });
                }
            }

            // ── Ship Capture ──────────────────────────────────────────
            result.ShipCaptureOffered = UnityEngine.Random.value < entry.shipCaptureChance;

            Debug.Log($"[LootTable] Rolled tier={entry.tier} mult={difficultyMult:F2}: {result}");
            return result;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Built-in Caribbean Loot Table
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds the default three-tier loot table using canonical Caribbean goods.
        /// Goods IDs ("rum", "sugar", etc.) match <c>GoodsData.goodsId</c> values.
        /// </summary>
        private static List<LootTierEntry> BuildDefaultTiers()
        {
            return new List<LootTierEntry>
            {
                // ── Tier 1: Sloop — small pirates, light cargo ────────────
                new LootTierEntry
                {
                    tier    = 1,
                    goldMin = 40,
                    goldMax = 90,
                    cargoPools = new List<LootCargoPool>
                    {
                        new LootCargoPool { goodsId = "rum",    quantityMin = 1, quantityMax = 3, dropChance = 0.60f },
                        new LootCargoPool { goodsId = "sugar",  quantityMin = 1, quantityMax = 2, dropChance = 0.40f },
                        new LootCargoPool { goodsId = "timber", quantityMin = 1, quantityMax = 4, dropChance = 0.30f },
                    },
                    shipCaptureChance = 0.05f,
                },

                // ── Tier 2: Brig — military escort, mixed cargo ───────────
                new LootTierEntry
                {
                    tier    = 2,
                    goldMin = 110,
                    goldMax = 220,
                    cargoPools = new List<LootCargoPool>
                    {
                        new LootCargoPool { goodsId = "spices",    quantityMin = 2, quantityMax = 5, dropChance = 0.55f },
                        new LootCargoPool { goodsId = "gunpowder", quantityMin = 1, quantityMax = 3, dropChance = 0.45f },
                        new LootCargoPool { goodsId = "rum",       quantityMin = 2, quantityMax = 4, dropChance = 0.50f },
                        new LootCargoPool { goodsId = "silk",      quantityMin = 1, quantityMax = 2, dropChance = 0.30f },
                    },
                    shipCaptureChance = 0.12f,
                },

                // ── Tier 3: Galleon — treasure fleet, maximum reward ──────
                new LootTierEntry
                {
                    tier    = 3,
                    goldMin = 260,
                    goldMax = 520,
                    cargoPools = new List<LootCargoPool>
                    {
                        new LootCargoPool { goodsId = "silk",      quantityMin = 3, quantityMax = 8, dropChance = 0.70f },
                        new LootCargoPool { goodsId = "spices",    quantityMin = 3, quantityMax = 6, dropChance = 0.65f },
                        new LootCargoPool { goodsId = "gunpowder", quantityMin = 2, quantityMax = 5, dropChance = 0.40f },
                        new LootCargoPool { goodsId = "rum",       quantityMin = 3, quantityMax = 7, dropChance = 0.60f },
                        new LootCargoPool { goodsId = "sugar",     quantityMin = 4, quantityMax = 8, dropChance = 0.50f },
                    },
                    shipCaptureChance = 0.25f,
                },
            };
        }
    }
}
