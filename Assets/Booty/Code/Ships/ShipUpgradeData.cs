// ---------------------------------------------------------------------------
// ShipUpgradeData.cs — ScriptableObject: defines a single ship upgrade tier
// ---------------------------------------------------------------------------
// Create via the Unity menu:
//   Assets → Create → Booty → Ships → ShipUpgradeData
//
// Three upgrade categories: Hull (HP), Sails (speed/turn), Cannons (damage).
// Each category has 3 tiers. Tier N requires Tier N-1 to be purchased first.
// ---------------------------------------------------------------------------

using UnityEngine;

namespace Booty.Ships
{
    /// <summary>
    /// Ship upgrade category — which stat group this upgrade improves.
    /// </summary>
    public enum UpgradeType
    {
        /// <summary>Hull plating upgrades: increase max HP.</summary>
        Hull    = 0,

        /// <summary>Sail upgrades: increase max speed and turn rate.</summary>
        Sails   = 1,

        /// <summary>Cannon upgrades: increase broadside damage.</summary>
        Cannons = 2,
    }

    /// <summary>
    /// ScriptableObject defining a single ship upgrade tier.
    /// Holds static properties (type, tier, cost) and stat bonuses.
    /// Runtime tracking (which tiers are purchased) lives in
    /// <see cref="ShipUpgradeManager"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "NewUpgrade", menuName = "Booty/Ships/ShipUpgradeData")]
    public class ShipUpgradeData : ScriptableObject
    {
        // ══════════════════════════════════════════════════════════════════
        //  Identity
        // ══════════════════════════════════════════════════════════════════

        [Header("Upgrade Identity")]
        [Tooltip("Category: Hull (HP), Sails (speed/turn), Cannons (damage).")]
        public UpgradeType upgradeType = UpgradeType.Hull;

        [Tooltip("Tier number within this category (1 = base, 2 = advanced, 3 = elite). " +
                 "Tier N requires Tier N-1 to be purchased first.")]
        [Range(1, 3)]
        public int tier = 1;

        [Tooltip("Short display name shown in the upgrade UI.")]
        public string displayName = "Upgrade";

        [Tooltip("Flavour text describing this upgrade.")]
        [TextArea(2, 4)]
        public string description = "";

        // ══════════════════════════════════════════════════════════════════
        //  Economy
        // ══════════════════════════════════════════════════════════════════

        [Header("Economy")]
        [Tooltip("Gold cost to purchase this upgrade at a port.")]
        [Min(1f)]
        public float cost = 200f;

        // ══════════════════════════════════════════════════════════════════
        //  Stat Bonuses
        // ══════════════════════════════════════════════════════════════════

        [Header("Hull Bonus")]
        [Tooltip("Hull HP increase (absolute value added to base max HP). " +
                 "Relevant for Hull upgrade type.")]
        [Min(0)]
        public int hullBonus = 0;

        [Header("Sail Bonuses")]
        [Tooltip("Speed multiplier bonus (additive, e.g. 0.1 = +10% speed). " +
                 "Relevant for Sails upgrade type.")]
        [Range(0f, 1f)]
        public float speedBonus = 0f;

        [Tooltip("Turn rate multiplier bonus (additive, e.g. 0.05 = +5% turn rate). " +
                 "Relevant for Sails upgrade type.")]
        [Range(0f, 1f)]
        public float turnBonus = 0f;

        [Header("Cannon Bonus")]
        [Tooltip("Cannon damage multiplier bonus (additive, e.g. 0.25 = +25% damage). " +
                 "Relevant for Cannons upgrade type.")]
        [Range(0f, 2f)]
        public float cannonDamageBonus = 0f;

        // ══════════════════════════════════════════════════════════════════
        //  Helper
        // ══════════════════════════════════════════════════════════════════

        public override string ToString() =>
            $"{displayName} (Tier {tier}, {upgradeType}) — {cost:F0}g";
    }
}
