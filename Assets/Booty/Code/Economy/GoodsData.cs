// ---------------------------------------------------------------------------
// GoodsData.cs — ScriptableObject: defines a single trade good
// ---------------------------------------------------------------------------
// Create via the Unity menu:
//   Assets → Create → Booty → Economy → GoodsData
//
// Six canonical goods: Rum, Sugar, Spices, Silk, Gunpowder, Timber.
// Base value is the "fair market" price; actual buy/sell prices are
// modified by supply/demand multipliers in PortEconomy assets.
// ---------------------------------------------------------------------------

using UnityEngine;

namespace Booty.Economy
{
    /// <summary>
    /// Categories of trade goods — drives supply/demand simulation logic.
    /// </summary>
    public enum GoodsCategory
    {
        /// <summary>High-value goods (rum, spices, silk). High price variance.</summary>
        Luxury = 0,

        /// <summary>Bulk goods (sugar, timber). Lower price variance, reliable profit.</summary>
        Commodity = 1,

        /// <summary>Military supplies (gunpowder). Restricted at some ports.</summary>
        Military = 2,
    }

    /// <summary>
    /// ScriptableObject defining a single class of trade good.
    /// Holds static properties (id, display name, base value, category).
    /// Runtime price fluctuations live in <see cref="PortEconomy"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "NewGoods", menuName = "Booty/Economy/GoodsData")]
    public class GoodsData : ScriptableObject
    {
        // ══════════════════════════════════════════════════════════════════
        //  Goods Definition
        // ══════════════════════════════════════════════════════════════════

        [Header("Goods Definition")]
        [Tooltip("Unique identifier used by trade systems (e.g. 'rum', 'gunpowder').")]
        public string goodsId;

        [Tooltip("Human-readable name shown in the trade UI.")]
        public string displayName;

        [Tooltip("Flavour text shown in the trade tooltip.")]
        [TextArea(2, 4)]
        public string description;

        // ══════════════════════════════════════════════════════════════════
        //  Value & Category
        // ══════════════════════════════════════════════════════════════════

        [Header("Value & Category")]
        [Tooltip("Fair-market price in gold. Actual prices are multiplied by port " +
                 "supply/demand ratios.")]
        [Min(1f)]
        public float baseValue = 50f;

        [Tooltip("Governs how strongly this good's price responds to supply/demand " +
                 "shifts. Higher = more volatile.")]
        [Range(0.1f, 3f)]
        public float priceVolatility = 1f;

        [Tooltip("Classification that drives port specialisation logic.")]
        public GoodsCategory category = GoodsCategory.Commodity;

        // ══════════════════════════════════════════════════════════════════
        //  Cargo
        // ══════════════════════════════════════════════════════════════════

        [Header("Cargo")]
        [Tooltip("How many units of cargo hold a single unit of this good consumes.")]
        [Min(1)]
        public int cargoWeight = 1;

        // ══════════════════════════════════════════════════════════════════
        //  Helper
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns the fair-market price rounded to the nearest gold piece.
        /// </summary>
        public override string ToString() => $"{displayName} ({baseValue:F0}g)";
    }
}
