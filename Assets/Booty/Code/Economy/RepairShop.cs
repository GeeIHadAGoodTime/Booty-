using UnityEngine;
using Booty.Save;

namespace Booty.Economy
{
    /// <summary>
    /// Gold sink: spend gold to repair hull HP at friendly or player-owned ports.
    /// Cost scales with the amount of missing HP.
    /// Per SubPRD 4.3: "Repair Ship" restores hull HP to full,
    /// cost scales with missing HP and a global scalar.
    /// </summary>
    public class RepairShop : MonoBehaviour
    {
        [Header("Repair Cost Settings")]
        [SerializeField] private float costPerHpPoint = 1.5f;
        [SerializeField] private float repairCostScalar = 1.0f;
        [SerializeField] private float minimumRepairCost = 5f;

        private EconomySystem _economySystem;
        private SaveSystem _saveSystem;

        /// <summary>
        /// Initialize the repair shop with references to game systems.
        /// Called by GameRoot during bootstrap.
        /// </summary>
        /// <param name="economySystem">The economy system for gold transactions.</param>
        /// <param name="saveSystem">The save system for ship state.</param>
        public void Initialize(EconomySystem economySystem, SaveSystem saveSystem)
        {
            _economySystem = economySystem;
            _saveSystem = saveSystem;
            Debug.Log("[RepairShop] Initialized.");
        }

        /// <summary>
        /// Calculate the cost to fully repair the player's ship.
        /// Formula: max(minimumCost, missingHP * costPerHpPoint * repairCostScalar).
        /// </summary>
        /// <returns>The gold cost for full repair, or 0 if ship is at full health.</returns>
        public float GetRepairCost()
        {
            if (_saveSystem == null || _saveSystem.CurrentState == null)
                return 0f;

            var ship = _saveSystem.CurrentState.playerShip;
            int missingHp = ship.maxHull - ship.currentHull;

            if (missingHp <= 0)
                return 0f;

            float cost = missingHp * costPerHpPoint * repairCostScalar;
            return Mathf.Max(minimumRepairCost, cost);
        }

        /// <summary>
        /// Get the current and max hull HP of the player's ship.
        /// </summary>
        /// <param name="currentHull">Output: current hull HP.</param>
        /// <param name="maxHull">Output: maximum hull HP.</param>
        /// <returns>True if ship data is available.</returns>
        public bool GetShipHullStatus(out int currentHull, out int maxHull)
        {
            currentHull = 0;
            maxHull = 0;

            if (_saveSystem == null || _saveSystem.CurrentState == null)
                return false;

            var ship = _saveSystem.CurrentState.playerShip;
            currentHull = ship.currentHull;
            maxHull = ship.maxHull;
            return true;
        }

        /// <summary>
        /// Attempt to repair the player's ship to full hull HP.
        /// Deducts gold if the player can afford it.
        /// </summary>
        /// <returns>True if repair was performed, false if insufficient gold or no damage.</returns>
        public bool RepairShip()
        {
            if (_saveSystem == null || _saveSystem.CurrentState == null || _economySystem == null)
            {
                Debug.LogWarning("[RepairShop] Not initialized properly.");
                return false;
            }

            var ship = _saveSystem.CurrentState.playerShip;
            int missingHp = ship.maxHull - ship.currentHull;

            if (missingHp <= 0)
            {
                Debug.Log("[RepairShop] Ship is already at full hull.");
                return false;
            }

            float cost = GetRepairCost();

            if (!_economySystem.SpendGold(cost))
            {
                Debug.Log($"[RepairShop] Cannot afford repair. Cost: {cost:F0}, Gold: {_economySystem.Gold:F0}");
                return false;
            }

            ship.currentHull = ship.maxHull;

            Debug.Log($"[RepairShop] Ship repaired! Restored {missingHp} HP for {cost:F0} gold. " +
                      $"Hull: {ship.currentHull}/{ship.maxHull}");

            return true;
        }

        /// <summary>
        /// Apply damage to the player's ship. Called by the combat system.
        /// </summary>
        /// <param name="damage">Amount of hull damage to apply.</param>
        /// <returns>Remaining hull HP after damage.</returns>
        public int ApplyDamage(int damage)
        {
            if (_saveSystem == null || _saveSystem.CurrentState == null)
                return 0;

            var ship = _saveSystem.CurrentState.playerShip;
            ship.currentHull = Mathf.Max(0, ship.currentHull - damage);
            return ship.currentHull;
        }

        /// <summary>
        /// Check if the player's ship is destroyed (hull at 0).
        /// </summary>
        /// <returns>True if hull HP is zero or less.</returns>
        public bool IsShipDestroyed()
        {
            if (_saveSystem == null || _saveSystem.CurrentState == null)
                return false;

            return _saveSystem.CurrentState.playerShip.currentHull <= 0;
        }
    }
}
