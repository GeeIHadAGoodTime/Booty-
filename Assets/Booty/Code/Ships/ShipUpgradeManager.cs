// ---------------------------------------------------------------------------
// ShipUpgradeManager.cs — Manages ship upgrade purchases at ports
// ---------------------------------------------------------------------------
// Tracks which upgrade tiers the player has purchased (Hull / Sails / Cannons)
// and applies the accumulated stat bonuses to the player's ship components.
//
// Upgrade tiers must be purchased in sequence (1 → 2 → 3).
// Each purchase deducts gold from EconomySystem and immediately applies
// the stat change: hull HP via HPSystem.SetMaxHP, speed/turn via
// ShipController.SetUpgradeMultipliers, damage via
// BroadsideSystem.SetDamageBonusMultiplier.
//
// WIRING (BootyBootstrap):
//   var upgradeManager = upgradeGO.AddComponent<ShipUpgradeManager>();
//   upgradeManager.Initialize(
//       shipController, hpSystem, broadsideSystem, economy,
//       balance.playerMaxHP, allUpgradeAssets);
// ---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;
using Booty.Economy;
using Booty.Combat;

namespace Booty.Ships
{
    /// <summary>
    /// Manages ship upgrade purchases. Tracks which of the three tiers for each
    /// of the three upgrade categories (Hull / Sails / Cannons) have been bought,
    /// and applies the combined stat bonuses to the player's ship.
    /// </summary>
    public class ShipUpgradeManager : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Inspector
        // ══════════════════════════════════════════════════════════════════

        [Header("Available Upgrades")]
        [Tooltip("All ShipUpgradeData assets available for purchase. " +
                 "Assign all 9 tier assets (Hull T1-3, Sails T1-3, Cannons T1-3).")]
        [SerializeField] private List<ShipUpgradeData> upgradeAssets = new();

        // ══════════════════════════════════════════════════════════════════
        //  Events
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Fired when an upgrade is successfully purchased.
        /// Arg: the ShipUpgradeData asset that was purchased.
        /// </summary>
        public event Action<ShipUpgradeData> OnUpgradePurchased;

        // ══════════════════════════════════════════════════════════════════
        //  Runtime State
        // ══════════════════════════════════════════════════════════════════

        // Ship component references
        private ShipController  _shipController;
        private HPSystem        _hpSystem;
        private BroadsideSystem _broadside;
        private EconomySystem   _economy;

        // Base stats (from BootyBootstrap / balance config — before upgrades)
        private int _baseMaxHP;

        // Purchased tier flags: index 0 = tier 1, 1 = tier 2, 2 = tier 3
        private readonly bool[] _hullPurchased    = new bool[3];
        private readonly bool[] _sailsPurchased   = new bool[3];
        private readonly bool[] _cannonsPurchased = new bool[3];

        // Accumulated bonuses (sum of all purchased tiers)
        private int   _totalHullBonus      = 0;
        private float _totalSpeedBonus     = 0f;
        private float _totalTurnBonus      = 0f;
        private float _totalDamageBonus    = 0f;

        // ══════════════════════════════════════════════════════════════════
        //  Public Accessors
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Read-only list of all upgrade assets registered at init.</summary>
        public IReadOnlyList<ShipUpgradeData> UpgradeAssets => upgradeAssets;

        /// <summary>
        /// True if the given tier (1-3) of the given upgrade type has been purchased.
        /// </summary>
        public bool IsPurchased(UpgradeType type, int tier)
        {
            if (tier < 1 || tier > 3) return false;
            return GetPurchasedArray(type)[tier - 1];
        }

        /// <summary>Current hull HP bonus from all purchased hull upgrades.</summary>
        public int TotalHullBonus    => _totalHullBonus;

        /// <summary>Current speed bonus multiplier from all purchased sail upgrades.</summary>
        public float TotalSpeedBonus  => _totalSpeedBonus;

        /// <summary>Current turn rate bonus multiplier from all purchased sail upgrades.</summary>
        public float TotalTurnBonus   => _totalTurnBonus;

        /// <summary>Current damage bonus multiplier from all purchased cannon upgrades.</summary>
        public float TotalDamageBonus => _totalDamageBonus;

        // ══════════════════════════════════════════════════════════════════
        //  Initialization
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Wire all ship components and base stats. Call from BootyBootstrap after
        /// the player ship is created and all components are configured.
        /// </summary>
        /// <param name="shipController">The player's ShipController.</param>
        /// <param name="hpSystem">The player's HPSystem.</param>
        /// <param name="broadside">The player's BroadsideSystem.</param>
        /// <param name="economy">The shared EconomySystem (gold source).</param>
        /// <param name="baseMaxHP">The balance-configured max HP (before upgrades).</param>
        /// <param name="assets">Optional: add upgrade assets programmatically.</param>
        public void Initialize(
            ShipController  shipController,
            HPSystem        hpSystem,
            BroadsideSystem broadside,
            EconomySystem   economy,
            int             baseMaxHP,
            IEnumerable<ShipUpgradeData> assets = null)
        {
            _shipController = shipController;
            _hpSystem       = hpSystem;
            _broadside      = broadside;
            _economy        = economy;
            _baseMaxHP      = baseMaxHP;

            if (assets != null)
                upgradeAssets.AddRange(assets);

            Debug.Log($"[ShipUpgradeManager] Initialized. Base HP: {baseMaxHP}. " +
                      $"Upgrade assets: {upgradeAssets.Count}");
        }

        // ══════════════════════════════════════════════════════════════════
        //  Public API
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Check whether the given upgrade can currently be purchased.
        /// Fails if: already purchased, tier prerequisite not met, or insufficient gold.
        /// </summary>
        public bool CanBuyUpgrade(ShipUpgradeData data)
        {
            if (data == null) return false;

            int tierIndex = data.tier - 1;
            if (tierIndex < 0 || tierIndex > 2) return false;

            bool[] purchased = GetPurchasedArray(data.upgradeType);

            // Already bought this tier?
            if (purchased[tierIndex]) return false;

            // Tier prerequisite: tier N requires tier N-1 to be purchased first
            if (tierIndex > 0 && !purchased[tierIndex - 1]) return false;

            // Sufficient gold?
            if (_economy == null || _economy.Gold < data.cost) return false;

            return true;
        }

        /// <summary>
        /// Attempt to purchase the given upgrade.
        /// Deducts gold, marks the tier as purchased, and applies stat bonuses.
        /// </summary>
        /// <returns>True if the purchase succeeded.</returns>
        public bool BuyUpgrade(ShipUpgradeData data)
        {
            if (!CanBuyUpgrade(data))
            {
                Debug.Log($"[ShipUpgradeManager] Cannot buy upgrade: {data?.displayName}. " +
                           "Check prerequisites and gold.");
                return false;
            }

            // Deduct gold
            bool spent = _economy.SpendGold(data.cost);
            if (!spent)
            {
                Debug.LogWarning($"[ShipUpgradeManager] SpendGold failed for {data.displayName}.");
                return false;
            }

            // Mark tier as purchased
            GetPurchasedArray(data.upgradeType)[data.tier - 1] = true;

            // Accumulate bonuses
            _totalHullBonus   += data.hullBonus;
            _totalSpeedBonus  += data.speedBonus;
            _totalTurnBonus   += data.turnBonus;
            _totalDamageBonus += data.cannonDamageBonus;

            // Apply all bonuses to the ship
            ApplyUpgrades();

            Debug.Log($"[ShipUpgradeManager] Purchased: {data.displayName} (Tier {data.tier}). " +
                      $"Cost: {data.cost:F0}g. " +
                      $"Hull+{_totalHullBonus} Speed+{_totalSpeedBonus:F2} " +
                      $"Turn+{_totalTurnBonus:F2} Dmg+{_totalDamageBonus:F2}");

            OnUpgradePurchased?.Invoke(data);
            return true;
        }

        /// <summary>
        /// Get the ShipUpgradeData asset for the given type + tier, or null if not found.
        /// </summary>
        public ShipUpgradeData GetUpgradeAsset(UpgradeType type, int tier)
        {
            foreach (var asset in upgradeAssets)
            {
                if (asset != null && asset.upgradeType == type && asset.tier == tier)
                    return asset;
            }
            return null;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Stat Application
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Apply all accumulated upgrade bonuses to the player's ship components.
        /// Safe to call multiple times — accumulates correctly.
        /// </summary>
        private void ApplyUpgrades()
        {
            // Hull: increase max HP (preserves current HP ratio)
            if (_hpSystem != null)
            {
                _hpSystem.SetMaxHP(_baseMaxHP + _totalHullBonus);
            }

            // Sails: multiplicative speed and turn bonuses
            if (_shipController != null)
            {
                _shipController.SetUpgradeMultipliers(
                    1f + _totalSpeedBonus,
                    1f + _totalTurnBonus);
            }

            // Cannons: multiplicative damage bonus
            if (_broadside != null)
            {
                _broadside.SetDamageBonusMultiplier(1f + _totalDamageBonus);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Helpers
        // ══════════════════════════════════════════════════════════════════

        private bool[] GetPurchasedArray(UpgradeType type)
        {
            return type switch
            {
                UpgradeType.Hull    => _hullPurchased,
                UpgradeType.Sails   => _sailsPurchased,
                UpgradeType.Cannons => _cannonsPurchased,
                _                   => _hullPurchased,
            };
        }
    }
}
