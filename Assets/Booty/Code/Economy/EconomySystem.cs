using System;
using UnityEngine;
using Booty.Ports;
using Booty.Save;
using Booty.Balance;

namespace Booty.Economy
{
    /// <summary>
    /// Manages the game economy: gold tracking, port income ticks, and combat spoils.
    /// P1 scope: base_price only, income = base_income_per_port * owned_port_count,
    /// ticking every incomeIntervalSeconds.
    /// Schema owner: economy.* fields per PRD C3.
    /// </summary>
    public class EconomySystem : MonoBehaviour
    {
        [Header("Income Settings")]
        [SerializeField] private float incomeIntervalSeconds = 60f;
        [SerializeField] private float globalIncomeScalar = 1.0f;
        [SerializeField] private float startingGold = 300f;

        [Header("Combat Spoils")]
        [SerializeField] private float baseCombatReward = 80f;
        [SerializeField] private float combatRewardPerTier = 35f;

        /// <summary>Fired when gold changes. Args: newGoldTotal, delta.</summary>
        public event Action<float, float> OnGoldChanged;

        /// <summary>Fired when income is collected. Args: incomeAmount, portCount.</summary>
        public event Action<float, int> OnIncomeCollected;

        private PortSystem _portSystem;
        private SaveSystem _saveSystem;
        private float _incomeTimer;

        /// <summary>Current player gold total.</summary>
        public float Gold { get; private set; }

        /// <summary>
        /// Apply balance values from a GameBalance asset.
        /// Call this before or after Initialize() — values are read dynamically.
        /// </summary>
        /// <param name="balance">The active GameBalance (from DifficultyManager).</param>
        public void ConfigureBalance(GameBalance balance)
        {
            if (balance == null) return;
            incomeIntervalSeconds = balance.incomeIntervalSeconds;
            globalIncomeScalar    = balance.globalIncomeScalar;
            baseCombatReward      = balance.baseCombatReward;
            combatRewardPerTier   = balance.combatRewardPerTier;
            startingGold          = balance.startingGold;
            Debug.Log($"[EconomySystem] Balance configured: " +
                      $"income={incomeIntervalSeconds}s scalar={globalIncomeScalar} " +
                      $"combatReward={baseCombatReward}+{combatRewardPerTier}/tier");
        }

        /// <summary>
        /// Initialize the economy system with references to other systems.
        /// Called by GameRoot during bootstrap.
        /// </summary>
        /// <param name="portSystem">The port system for ownership queries.</param>
        /// <param name="saveSystem">The save system for state persistence.</param>
        public void Initialize(PortSystem portSystem, SaveSystem saveSystem)
        {
            _portSystem = portSystem;
            _saveSystem = saveSystem;

            // Load gold from save state
            if (_saveSystem != null && _saveSystem.CurrentState != null)
            {
                Gold = _saveSystem.CurrentState.player.gold;
                _incomeTimer = _saveSystem.CurrentState.economy.incomeTimer;
            }
            else
            {
                Gold = startingGold;
                _incomeTimer = 0f;
            }

            Debug.Log($"[EconomySystem] Initialized. Gold: {Gold}, Income interval: {incomeIntervalSeconds}s");
        }

        private void Update()
        {
            TickIncome();
        }

        /// <summary>
        /// Process income ticks. Accumulates time and awards port income when
        /// the interval is reached.
        /// </summary>
        private void TickIncome()
        {
            if (_portSystem == null)
                return;

            _incomeTimer += Time.deltaTime;

            if (_incomeTimer >= incomeIntervalSeconds)
            {
                _incomeTimer -= incomeIntervalSeconds;
                CollectPortIncome();
            }

            // Sync timer to save state
            if (_saveSystem != null && _saveSystem.CurrentState != null)
            {
                _saveSystem.CurrentState.economy.incomeTimer = _incomeTimer;
            }
        }

        /// <summary>
        /// Collect income from all player-owned ports.
        /// Formula: sum(port.base_income) * globalIncomeScalar for each player-owned port.
        /// </summary>
        private void CollectPortIncome()
        {
            var playerPorts = _portSystem.GetPortsByFaction("player_pirates");
            if (playerPorts.Count == 0)
                return;

            float totalIncome = 0f;
            foreach (var port in playerPorts)
            {
                totalIncome += port.baseIncome * globalIncomeScalar;
            }

            if (totalIncome > 0f)
            {
                AddGold(totalIncome);
                OnIncomeCollected?.Invoke(totalIncome, playerPorts.Count);
                Debug.Log($"[EconomySystem] Port income collected: +{totalIncome:F0} gold " +
                          $"from {playerPorts.Count} port(s). Total gold: {Gold:F0}");
            }
        }

        /// <summary>
        /// Add gold to the player's balance. Used for income, combat rewards, etc.
        /// </summary>
        /// <param name="amount">Amount of gold to add. Must be positive.</param>
        public void AddGold(float amount)
        {
            if (amount <= 0f)
                return;

            Gold += amount;
            SyncGoldToSave();
            OnGoldChanged?.Invoke(Gold, amount);
        }

        /// <summary>
        /// Spend gold from the player's balance. Fails if insufficient funds.
        /// </summary>
        /// <param name="amount">Amount of gold to spend. Must be positive.</param>
        /// <returns>True if the transaction succeeded, false if insufficient gold.</returns>
        public bool SpendGold(float amount)
        {
            if (amount <= 0f)
                return true;

            if (Gold < amount)
            {
                Debug.Log($"[EconomySystem] Insufficient gold. Have: {Gold:F0}, Need: {amount:F0}");
                return false;
            }

            Gold -= amount;
            SyncGoldToSave();
            OnGoldChanged?.Invoke(Gold, -amount);
            return true;
        }

        /// <summary>
        /// Award combat spoils for defeating an enemy ship.
        /// Reward scales with enemy tier.
        /// </summary>
        /// <param name="enemyTier">The tier of the defeated enemy ship (1-3).</param>
        /// <returns>The gold amount awarded.</returns>
        public float AwardCombatSpoils(int enemyTier)
        {
            float reward = baseCombatReward + (combatRewardPerTier * Mathf.Max(0, enemyTier - 1));
            AddGold(reward);
            Debug.Log($"[EconomySystem] Combat spoils: +{reward:F0} gold (tier {enemyTier}).");
            return reward;
        }

        /// <summary>
        /// Get the total income per tick from all player-owned ports.
        /// Used by UI to display expected income.
        /// </summary>
        /// <returns>Total gold per income tick.</returns>
        public float GetTotalIncomePerTick()
        {
            if (_portSystem == null)
                return 0f;

            var playerPorts = _portSystem.GetPortsByFaction("player_pirates");
            float total = 0f;
            foreach (var port in playerPorts)
            {
                total += port.baseIncome * globalIncomeScalar;
            }
            return total;
        }

        /// <summary>
        /// Get the time remaining until the next income tick.
        /// </summary>
        /// <returns>Seconds until next income collection.</returns>
        public float GetTimeToNextIncome()
        {
            return Mathf.Max(0f, incomeIntervalSeconds - _incomeTimer);
        }

        /// <summary>
        /// Force-set the player's gold to a specific value. Debug use only.
        /// </summary>
        /// <param name="amount">The new gold total.</param>
        public void SetGold(float amount)
        {
            float delta = amount - Gold;
            Gold = amount;
            SyncGoldToSave();
            OnGoldChanged?.Invoke(Gold, delta);
        }

        /// <summary>
        /// Sync the current gold value to the save system's in-memory state.
        /// </summary>
        private void SyncGoldToSave()
        {
            if (_saveSystem != null && _saveSystem.CurrentState != null)
            {
                _saveSystem.CurrentState.player.gold = Gold;
            }
        }
    }
}
