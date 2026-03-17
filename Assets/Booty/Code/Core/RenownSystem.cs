using System;
using UnityEngine;
using Booty.Ports;
using Booty.Save;

namespace Booty.Core
{
    /// <summary>
    /// Manages the player's renown scalar -- the primary progression metric in P1.
    /// Renown increases from combat kills and port captures, is visible in the UI,
    /// and has at least one small gameplay effect (encounter difficulty scaling).
    /// Maps to PRD Appendix A3: player.renown.
    /// </summary>
    public class RenownSystem : MonoBehaviour
    {
        [Header("Renown Awards")]
        [SerializeField] private float renownPerKill = 5f;
        [SerializeField] private float renownPerKillTierBonus = 3f;
        [SerializeField] private float renownPerPortCapture = 25f;

        [Header("Renown Tiers")]
        [SerializeField] private float tierNotoriousThreshold = 50f;
        [SerializeField] private float tierFearedThreshold = 150f;
        [SerializeField] private float tierLegendaryThreshold = 400f;

        [Header("Difficulty Scaling")]
        [SerializeField] private float difficultyScalePerRenown = 0.002f;
        [SerializeField] private float maxDifficultyMultiplier = 2.0f;

        /// <summary>Fired when renown changes. Args: newRenown, delta.</summary>
        public event Action<float, float> OnRenownChanged;

        /// <summary>Fired when the player reaches a new renown tier. Args: tierName.</summary>
        public event Action<string> OnTierReached;

        private SaveSystem _saveSystem;
        private PortSystem _portSystem;
        private string _currentTier = "Unknown";

        /// <summary>Current renown value.</summary>
        public float Renown { get; private set; }

        /// <summary>Current renown tier label (e.g. "Unknown", "Notorious", "Feared").</summary>
        public string CurrentTier => _currentTier;

        /// <summary>
        /// Initialize the renown system. Called by GameRoot during bootstrap.
        /// </summary>
        /// <param name="saveSystem">Save system for persistence.</param>
        /// <param name="portSystem">Port system to listen for capture events.</param>
        public void Initialize(SaveSystem saveSystem, PortSystem portSystem)
        {
            _saveSystem = saveSystem;
            _portSystem = portSystem;

            // Load renown from save
            if (_saveSystem != null && _saveSystem.CurrentState != null)
            {
                Renown = _saveSystem.CurrentState.player.renown;
            }
            else
            {
                Renown = 0f;
            }

            _currentTier = CalculateTier(Renown);

            // Subscribe to port capture events
            if (_portSystem != null)
            {
                _portSystem.OnPortCaptured += HandlePortCaptured;
            }

            Debug.Log($"[RenownSystem] Initialized. Renown: {Renown:F0}, Tier: {_currentTier}");
        }

        /// <summary>
        /// Award renown for defeating an enemy ship.
        /// Bonus renown for higher-tier enemies.
        /// </summary>
        /// <param name="enemyTier">The tier of the defeated enemy (1-3).</param>
        /// <returns>The amount of renown awarded.</returns>
        public float AwardKillRenown(int enemyTier)
        {
            float amount = renownPerKill + (renownPerKillTierBonus * Mathf.Max(0, enemyTier - 1));
            AddRenown(amount);
            Debug.Log($"[RenownSystem] Kill renown: +{amount:F0} (tier {enemyTier}). Total: {Renown:F0}");
            return amount;
        }

        /// <summary>
        /// Add renown and check for tier changes. Used internally and for custom awards.
        /// </summary>
        /// <param name="amount">Amount of renown to add. Must be positive.</param>
        public void AddRenown(float amount)
        {
            if (amount <= 0f)
                return;

            string previousTier = _currentTier;
            Renown += amount;
            _currentTier = CalculateTier(Renown);

            SyncToSave();
            OnRenownChanged?.Invoke(Renown, amount);

            // Check for tier change
            if (_currentTier != previousTier)
            {
                OnTierReached?.Invoke(_currentTier);
                Debug.Log($"[RenownSystem] New tier reached: {_currentTier}!");
            }
        }

        /// <summary>
        /// Get the encounter difficulty multiplier based on current renown.
        /// Higher renown means tougher encounters. Used by EnemySpawner.
        /// </summary>
        /// <returns>Difficulty multiplier (1.0 = baseline, up to maxDifficultyMultiplier).</returns>
        public float GetDifficultyMultiplier()
        {
            float multiplier = 1f + (Renown * difficultyScalePerRenown);
            return Mathf.Min(multiplier, maxDifficultyMultiplier);
        }

        /// <summary>
        /// Get the renown tier label for a given renown value.
        /// </summary>
        /// <param name="renown">The renown value to evaluate.</param>
        /// <returns>Tier label string.</returns>
        public string CalculateTier(float renown)
        {
            if (renown >= tierLegendaryThreshold) return "Legendary";
            if (renown >= tierFearedThreshold)    return "Feared";
            if (renown >= tierNotoriousThreshold) return "Notorious";
            return "Unknown";
        }

        /// <summary>
        /// Get the renown threshold for the next tier above the current one.
        /// Returns -1 if already at max tier.
        /// </summary>
        /// <returns>Renown required for next tier, or -1 if maxed.</returns>
        public float GetNextTierThreshold()
        {
            if (Renown < tierNotoriousThreshold) return tierNotoriousThreshold;
            if (Renown < tierFearedThreshold)    return tierFearedThreshold;
            if (Renown < tierLegendaryThreshold) return tierLegendaryThreshold;
            return -1f;
        }

        /// <summary>
        /// Force-set renown to a specific value. Debug use only.
        /// </summary>
        /// <param name="value">The new renown value.</param>
        public void SetRenown(float value)
        {
            float delta = value - Renown;
            Renown = Mathf.Max(0f, value);
            _currentTier = CalculateTier(Renown);
            SyncToSave();
            OnRenownChanged?.Invoke(Renown, delta);
        }

        /// <summary>
        /// Handle a port capture event. Awards renown for capturing enemy ports.
        /// </summary>
        private void HandlePortCaptured(string portId, string newFaction)
        {
            if (newFaction == "player_pirates")
            {
                AddRenown(renownPerPortCapture);
                Debug.Log($"[RenownSystem] Port capture renown: +{renownPerPortCapture:F0}. Total: {Renown:F0}");
            }
        }

        /// <summary>
        /// Sync current renown to the save system's in-memory state.
        /// </summary>
        private void SyncToSave()
        {
            if (_saveSystem != null && _saveSystem.CurrentState != null)
            {
                _saveSystem.CurrentState.player.renown = Renown;
            }
        }

        private void OnDestroy()
        {
            if (_portSystem != null)
            {
                _portSystem.OnPortCaptured -= HandlePortCaptured;
            }
        }
    }
}
