// ---------------------------------------------------------------------------
// CargoInventory.cs — Player cargo hold: tracks owned trade goods
// ---------------------------------------------------------------------------
// Attach to the player ship GameObject (or any persistent manager).
// Initialise via Initialize(maxCapacity). TradeManager calls AddGoods /
// RemoveGoods when the player buys or sells at port.
// ---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Booty.Economy
{
    /// <summary>
    /// One stack of a single trade good in the cargo hold.
    /// </summary>
    [Serializable]
    public class CargoEntry
    {
        public GoodsData goods;
        public int       quantity;

        public CargoEntry(GoodsData g, int q) { goods = g; quantity = q; }
    }

    /// <summary>
    /// Tracks the player's cargo hold: which goods are held and in what quantity.
    /// Enforces a maximum weight capacity across all goods.
    /// </summary>
    public class CargoInventory : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Events
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Fired whenever the cargo changes.
        /// Args: the GoodsData that changed, the new quantity for that good.
        /// </summary>
        public event Action<GoodsData, int> OnCargoChanged;

        // ══════════════════════════════════════════════════════════════════
        //  Inspector
        // ══════════════════════════════════════════════════════════════════

        [Header("Cargo Hold")]
        [Tooltip("Maximum total cargo weight the hold can carry.")]
        [SerializeField] private int maxCapacity = 100;

        // ══════════════════════════════════════════════════════════════════
        //  State
        // ══════════════════════════════════════════════════════════════════

        private readonly List<CargoEntry> _items = new List<CargoEntry>();

        // ══════════════════════════════════════════════════════════════════
        //  Initialisation
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Configure the cargo hold's maximum weight capacity.
        /// Called by BootyBootstrap during startup.
        /// </summary>
        /// <param name="capacity">Maximum weight units the hold can carry.</param>
        public void Initialize(int capacity)
        {
            maxCapacity = capacity;
            _items.Clear();
            Debug.Log($"[CargoInventory] Initialised. Capacity: {maxCapacity}");
        }

        // ══════════════════════════════════════════════════════════════════
        //  Public API
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Total weight of all goods currently in the hold.
        /// </summary>
        public int UsedCapacity
        {
            get
            {
                int total = 0;
                foreach (var entry in _items)
                    total += entry.quantity * (entry.goods != null ? entry.goods.cargoWeight : 1);
                return total;
            }
        }

        /// <summary>Maximum weight the hold can carry.</summary>
        public int MaxCapacity => maxCapacity;

        /// <summary>Remaining free weight in the hold.</summary>
        public int FreeCapacity => maxCapacity - UsedCapacity;

        /// <summary>True if the hold is at or over capacity.</summary>
        public bool IsFull => UsedCapacity >= maxCapacity;

        /// <summary>Read-only view of all cargo entries.</summary>
        public IReadOnlyList<CargoEntry> Items => _items;

        /// <summary>
        /// Returns the number of units of a specific good currently in the hold.
        /// </summary>
        public int GetQuantity(GoodsData goods)
        {
            if (goods == null) return 0;
            foreach (var entry in _items)
            {
                if (entry.goods == goods)
                    return entry.quantity;
            }
            return 0;
        }

        /// <summary>
        /// Attempt to add goods to the cargo hold.
        /// Fails if there is insufficient free capacity.
        /// </summary>
        /// <param name="goods">The good to add.</param>
        /// <param name="quantity">How many units to add.</param>
        /// <returns>True if the goods were added successfully.</returns>
        public bool AddGoods(GoodsData goods, int quantity)
        {
            if (goods == null || quantity <= 0)
                return false;

            int weightNeeded = quantity * goods.cargoWeight;
            if (weightNeeded > FreeCapacity)
            {
                Debug.Log($"[CargoInventory] Insufficient capacity. Need {weightNeeded}, " +
                          $"have {FreeCapacity}. Rejecting {goods.displayName} x{quantity}.");
                return false;
            }

            var entry = FindEntry(goods);
            if (entry != null)
            {
                entry.quantity += quantity;
            }
            else
            {
                _items.Add(new CargoEntry(goods, quantity));
            }

            Debug.Log($"[CargoInventory] +{quantity} {goods.displayName}. " +
                      $"Hold: {UsedCapacity}/{maxCapacity}");

            OnCargoChanged?.Invoke(goods, GetQuantity(goods));
            return true;
        }

        /// <summary>
        /// Attempt to remove goods from the cargo hold.
        /// Fails if the player does not hold the requested quantity.
        /// </summary>
        /// <param name="goods">The good to remove.</param>
        /// <param name="quantity">How many units to remove.</param>
        /// <returns>True if the goods were removed successfully.</returns>
        public bool RemoveGoods(GoodsData goods, int quantity)
        {
            if (goods == null || quantity <= 0)
                return false;

            var entry = FindEntry(goods);
            if (entry == null || entry.quantity < quantity)
            {
                Debug.Log($"[CargoInventory] Cannot remove {quantity} {goods?.displayName}. " +
                          $"Have: {entry?.quantity ?? 0}");
                return false;
            }

            entry.quantity -= quantity;
            if (entry.quantity == 0)
                _items.Remove(entry);

            Debug.Log($"[CargoInventory] -{quantity} {goods.displayName}. " +
                      $"Hold: {UsedCapacity}/{maxCapacity}");

            OnCargoChanged?.Invoke(goods, GetQuantity(goods));
            return true;
        }

        /// <summary>
        /// Clear all goods from the cargo hold. Used on ship destruction or debug reset.
        /// </summary>
        public void ClearAll()
        {
            _items.Clear();
            Debug.Log("[CargoInventory] Hold cleared.");
        }

        /// <summary>
        /// How many units of the given good can still fit in the hold.
        /// </summary>
        public int GetAffordableQuantity(GoodsData goods)
        {
            if (goods == null || goods.cargoWeight <= 0) return 0;
            return FreeCapacity / goods.cargoWeight;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Internals
        // ══════════════════════════════════════════════════════════════════

        private CargoEntry FindEntry(GoodsData goods)
        {
            foreach (var entry in _items)
            {
                if (entry.goods == goods)
                    return entry;
            }
            return null;
        }
    }
}
