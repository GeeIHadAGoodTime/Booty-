// ---------------------------------------------------------------------------
// TradeManager.cs — buy/sell goods at ports with supply/demand pricing
// ---------------------------------------------------------------------------
// Attach to a persistent manager GameObject.  Initialise via Initialize().
//
// Price formula:
//   buyPrice  = baseValue * priceMultiplier(supply, demand) * distanceFactor
//   sellPrice = buyPrice * sellMargin (typically 0.85)
// where:
//   priceMultiplier = demandMultiplier / max(0.1, supplyLevel), clamped [0.25, 4]
//   distanceFactor  = 1 + distanceFromHomePort * distancePriceCoefficient
//
// When the player buys N units the port's supply drops by supplyDepletionRate*N,
// making repeat buying less profitable (resets over time via drift ticks).
// When the player sells N units the port's supply rises by the same amount.
// ---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Booty.Economy
{
    /// <summary>
    /// Result of a trade attempt — carries outcome info for UI display.
    /// </summary>
    public class TradeResult
    {
        public bool    Success;
        public string  FailReason;
        public float   TotalPrice;
        public int     QuantityTraded;
        public GoodsData Goods;

        public static TradeResult Ok(GoodsData g, int qty, float price) =>
            new TradeResult { Success = true, Goods = g, QuantityTraded = qty, TotalPrice = price };

        public static TradeResult Fail(string reason) =>
            new TradeResult { Success = false, FailReason = reason };
    }

    /// <summary>
    /// Manages all trade transactions between the player and port economies.
    /// Handles price calculation, supply/demand effects, and drift ticks.
    /// </summary>
    public class TradeManager : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Events
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Fired after a successful buy or sell. Args: result.</summary>
        public event Action<TradeResult> OnTradeCompleted;

        // ══════════════════════════════════════════════════════════════════
        //  Inspector
        // ══════════════════════════════════════════════════════════════════

        [Header("Trade Settings")]
        [Tooltip("What fraction of the buy price the player receives when selling (0–1).")]
        [Range(0.3f, 1f)]
        public float sellMargin = 0.85f;

        [Tooltip("Per-unit reduction in port supply when the player buys goods. " +
                 "Simulates goods leaving the market.")]
        [Range(0.001f, 0.05f)]
        public float supplyDepletionPerUnit = 0.01f;

        [Tooltip("Coefficient for the distance-based price bonus. " +
                 "0 = no distance factor; 0.005 = +0.5% per world unit.")]
        [Range(0f, 0.01f)]
        public float distancePriceCoefficient = 0.003f;

        [Header("Port Economy Assets")]
        [Tooltip("All PortEconomy assets — one per tradeable port. Assign in Inspector.")]
        public List<PortEconomy> portEconomies = new List<PortEconomy>();

        // ══════════════════════════════════════════════════════════════════
        //  Private State
        // ══════════════════════════════════════════════════════════════════

        private EconomySystem    _economySystem;
        private CargoInventory   _cargoInventory;

        private readonly Dictionary<string, PortEconomy> _portLookup =
            new Dictionary<string, PortEconomy>();

        private readonly Dictionary<string, float> _driftTimers =
            new Dictionary<string, float>();

        // ══════════════════════════════════════════════════════════════════
        //  Initialisation
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Initialise the trade manager.
        /// Called by BootyBootstrap during startup.
        /// </summary>
        public void Initialize(EconomySystem economySystem, CargoInventory cargoInventory)
        {
            _economySystem  = economySystem;
            _cargoInventory = cargoInventory;

            _portLookup.Clear();
            _driftTimers.Clear();

            foreach (var pe in portEconomies)
            {
                if (pe == null) continue;
                pe.InitialiseRuntime();
                _portLookup[pe.portId] = pe;
                _driftTimers[pe.portId] = 0f;
                Debug.Log($"[TradeManager] Registered port economy: {pe.portId}");
            }

            Debug.Log($"[TradeManager] Initialised with {_portLookup.Count} port economies.");
        }

        /// <summary>
        /// Register an additional port economy at runtime
        /// (used when ports are loaded dynamically from config).
        /// </summary>
        public void RegisterPort(PortEconomy portEconomy)
        {
            if (portEconomy == null) return;
            portEconomy.InitialiseRuntime();
            _portLookup[portEconomy.portId] = portEconomy;
            _driftTimers[portEconomy.portId] = 0f;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Unity Loop
        // ══════════════════════════════════════════════════════════════════

        private void Update()
        {
            TickDrift();
        }

        // ══════════════════════════════════════════════════════════════════
        //  Price Queries
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns the per-unit buy price for a good at a given port.
        /// Returns 0 if the good is not available at the port.
        /// </summary>
        /// <param name="portId">The port's runtime ID.</param>
        /// <param name="goods">The good to price.</param>
        /// <param name="playerPosition">Player ship position (for distance factor).</param>
        public float GetBuyPrice(string portId, GoodsData goods, Vector3 playerPosition = default)
        {
            var portEntry = GetPortEntry(portId, goods);
            if (portEntry == null) return 0f;

            float multiplier   = PortEconomy.ComputePriceMultiplier(portEntry);
            float distFactor   = ComputeDistanceFactor(portId, playerPosition);

            return Mathf.Round(goods.baseValue * multiplier * distFactor);
        }

        /// <summary>
        /// Returns the per-unit sell price for a good at a given port.
        /// Always lower than the buy price by <see cref="sellMargin"/>.
        /// </summary>
        public float GetSellPrice(string portId, GoodsData goods, Vector3 playerPosition = default)
        {
            float buyPrice = GetBuyPrice(portId, goods, playerPosition);
            return Mathf.Round(buyPrice * sellMargin);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Transactions
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// The player buys <paramref name="quantity"/> units of <paramref name="goods"/>
        /// at <paramref name="portId"/>.  Deducts gold, adds cargo, depresses supply.
        /// </summary>
        /// <returns>TradeResult indicating success or failure reason.</returns>
        public TradeResult BuyGoods(string portId, GoodsData goods, int quantity,
                                    Vector3 playerPosition = default)
        {
            if (!ValidateInputs(portId, goods, quantity, out var portEntry))
                return TradeResult.Fail("Good not available at this port.");

            float unitPrice  = GetBuyPrice(portId, goods, playerPosition);
            float totalCost  = unitPrice * quantity;

            if (_economySystem.Gold < totalCost)
                return TradeResult.Fail($"Not enough gold. Need {totalCost:F0}, have {_economySystem.Gold:F0}.");

            if (!_cargoInventory.GetAffordableQuantity(goods).Equals(0) &&
                _cargoInventory.GetAffordableQuantity(goods) < quantity)
                return TradeResult.Fail($"Cargo hold too full for {quantity} units of {goods.displayName}.");

            if (!_economySystem.SpendGold(totalCost))
                return TradeResult.Fail("Gold transaction failed.");

            if (!_cargoInventory.AddGoods(goods, quantity))
            {
                // Refund gold if cargo failed
                _economySystem.AddGold(totalCost);
                return TradeResult.Fail("Cargo hold is full.");
            }

            // Buying reduces port supply
            portEntry.RuntimeSupplyLevel = Mathf.Clamp01(
                portEntry.RuntimeSupplyLevel - supplyDepletionPerUnit * quantity);

            var result = TradeResult.Ok(goods, quantity, totalCost);
            OnTradeCompleted?.Invoke(result);

            Debug.Log($"[TradeManager] BOUGHT {quantity}x {goods.displayName} at {portId} " +
                      $"for {totalCost:F0}g ({unitPrice:F0}g each). " +
                      $"New supply: {portEntry.RuntimeSupplyLevel:F2}");
            return result;
        }

        /// <summary>
        /// The player sells <paramref name="quantity"/> units of <paramref name="goods"/>
        /// at <paramref name="portId"/>.  Adds gold, removes cargo, raises supply.
        /// </summary>
        /// <returns>TradeResult indicating success or failure reason.</returns>
        public TradeResult SellGoods(string portId, GoodsData goods, int quantity,
                                     Vector3 playerPosition = default)
        {
            if (!ValidateInputs(portId, goods, quantity, out var portEntry))
                return TradeResult.Fail("This port does not trade in that good.");

            int held = _cargoInventory.GetQuantity(goods);
            if (held < quantity)
                return TradeResult.Fail($"Not enough cargo. Have {held}, need {quantity}.");

            float unitPrice  = GetSellPrice(portId, goods, playerPosition);
            float totalGold  = unitPrice * quantity;

            if (!_cargoInventory.RemoveGoods(goods, quantity))
                return TradeResult.Fail("Failed to remove goods from cargo hold.");

            _economySystem.AddGold(totalGold);

            // Selling raises port supply
            portEntry.RuntimeSupplyLevel = Mathf.Clamp01(
                portEntry.RuntimeSupplyLevel + supplyDepletionPerUnit * quantity);

            var result = TradeResult.Ok(goods, quantity, totalGold);
            OnTradeCompleted?.Invoke(result);

            Debug.Log($"[TradeManager] SOLD {quantity}x {goods.displayName} at {portId} " +
                      $"for {totalGold:F0}g ({unitPrice:F0}g each). " +
                      $"New supply: {portEntry.RuntimeSupplyLevel:F2}");
            return result;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Port Economy Access
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns the PortEconomy asset for a port, or null if not registered.
        /// </summary>
        public PortEconomy GetPortEconomy(string portId)
        {
            _portLookup.TryGetValue(portId, out var pe);
            return pe;
        }

        /// <summary>
        /// Returns the list of goods entries for a port (for trade UI population).
        /// </summary>
        public IReadOnlyList<PortGoodsEntry> GetPortGoods(string portId)
        {
            if (_portLookup.TryGetValue(portId, out var pe))
                return pe.goods;
            return null;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Private Helpers
        // ══════════════════════════════════════════════════════════════════

        private void TickDrift()
        {
            // Drift all registered port economies on their individual timers
            var keys = new List<string>(_driftTimers.Keys);
            foreach (var portId in keys)
            {
                _driftTimers[portId] += Time.deltaTime;

                if (!_portLookup.TryGetValue(portId, out var pe)) continue;

                if (_driftTimers[portId] >= pe.driftIntervalSeconds)
                {
                    _driftTimers[portId] -= pe.driftIntervalSeconds;
                    pe.ApplyDriftTick();
                }
            }
        }

        private bool ValidateInputs(string portId, GoodsData goods, int quantity,
                                    out PortGoodsEntry portEntry)
        {
            portEntry = null;
            if (string.IsNullOrEmpty(portId) || goods == null || quantity <= 0)
                return false;

            if (!_portLookup.TryGetValue(portId, out var pe))
                return false;

            portEntry = pe.FindEntry(goods);
            return portEntry != null;
        }

        private PortGoodsEntry GetPortEntry(string portId, GoodsData goods)
        {
            if (!_portLookup.TryGetValue(portId, out var pe)) return null;
            return pe.FindEntry(goods);
        }

        private float ComputeDistanceFactor(string portId, Vector3 playerPosition)
        {
            if (distancePriceCoefficient <= 0f) return 1f;

            if (!_portLookup.TryGetValue(portId, out _)) return 1f;

            // Distance bonus: goods sold far from their origin port are worth more.
            // We use the player's world-space distance to the port as a proxy.
            // In the future this could use actual port world positions.
            float dist = playerPosition.magnitude; // fallback: dist from world origin
            return 1f + dist * distancePriceCoefficient;
        }
    }
}
