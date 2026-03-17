// ---------------------------------------------------------------------------
// CrewManager.cs — Manages player crew: hiring, dismissal, and stat effects
// ---------------------------------------------------------------------------
// Crew affects two stats:
//   Speed     — an undermanned ship (< 10 crew) sails slower; at full complement
//               speed is at 100%. This is applied via ShipController.SetCrewSpeedMultiplier.
//   Combat    — crew strength multiplier (0.7 at min crew, 1.3 at full) used by
//               boarding/combat systems via CrewCombatMultiplier.
//
// Crew are hired/dismissed at ports. Each hire costs costPerHead gold (deducted
// from EconomySystem). Dismissal is free (pirates choose their fate).
//
// WIRING (BootyBootstrap):
//   var crewMgr = crewGO.AddComponent<CrewManager>();
//   crewMgr.Initialize(economy, shipController, startingCrew: 5);
// ---------------------------------------------------------------------------

using System;
using UnityEngine;
using Booty.Economy;

namespace Booty.Ships
{
    /// <summary>
    /// Manages crew count for the player's ship. Crew count affects:
    /// <list type="bullet">
    ///   <item>Sailing speed (via <see cref="ShipController.SetCrewSpeedMultiplier"/>)</item>
    ///   <item>Combat boarding strength (via <see cref="CrewCombatMultiplier"/>)</item>
    /// </list>
    /// </summary>
    public class CrewManager : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Inspector
        // ══════════════════════════════════════════════════════════════════

        [Header("Crew Limits")]
        [Tooltip("Minimum crew required to sail. Cannot dismiss below this.")]
        [Min(1)]
        [SerializeField] private int minCrew = 1;

        [Tooltip("Maximum crew the ship can accommodate.")]
        [Min(1)]
        [SerializeField] private int maxCrew = 20;

        [Header("Crew Economics")]
        [Tooltip("Gold cost per crew member hired.")]
        [Min(1f)]
        [SerializeField] private float costPerHead = 25f;

        [Header("Speed Scaling")]
        [Tooltip("Crew count at which the ship reaches 100% speed efficiency. " +
                 "Below this, speed is penalised linearly down to minSpeedFraction.")]
        [Min(1)]
        [SerializeField] private int optimalCrew = 10;

        [Tooltip("Speed fraction when crew is at minCrew (e.g. 0.75 = 75% speed).")]
        [Range(0.1f, 1f)]
        [SerializeField] private float minSpeedFraction = 0.75f;

        [Header("Combat Scaling")]
        [Tooltip("Combat multiplier at minCrew (e.g. 0.7 = 70% boarding strength).")]
        [Range(0.1f, 1f)]
        [SerializeField] private float minCombatMultiplier = 0.7f;

        [Tooltip("Combat multiplier at maxCrew (e.g. 1.3 = 130% boarding strength).")]
        [Range(1f, 3f)]
        [SerializeField] private float maxCombatMultiplier = 1.3f;

        // ══════════════════════════════════════════════════════════════════
        //  Events
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Fired when crew count changes. Arg: new crew count.
        /// </summary>
        public event Action<int> OnCrewChanged;

        // ══════════════════════════════════════════════════════════════════
        //  Runtime State
        // ══════════════════════════════════════════════════════════════════

        private EconomySystem  _economy;
        private ShipController _shipController;

        /// <summary>Current number of crew members.</summary>
        public int CurrentCrew { get; private set; }

        /// <summary>Maximum crew the ship can accommodate.</summary>
        public int MaxCrew => maxCrew;

        /// <summary>Minimum crew needed to sail.</summary>
        public int MinCrew => minCrew;

        /// <summary>Gold cost per crew member.</summary>
        public float CostPerHead => costPerHead;

        /// <summary>
        /// Speed fraction based on current crew count (0.75..1.0).
        /// Used by ShipController to scale movement speed.
        /// </summary>
        public float CrewSpeedMultiplier
        {
            get
            {
                if (CurrentCrew >= optimalCrew) return 1f;
                float t = Mathf.Clamp01((float)(CurrentCrew - minCrew) /
                                        Mathf.Max(1, optimalCrew - minCrew));
                return Mathf.Lerp(minSpeedFraction, 1f, t);
            }
        }

        /// <summary>
        /// Combat strength multiplier based on current crew count (0.7..1.3).
        /// Consumed by boarding / combat systems.
        /// </summary>
        public float CrewCombatMultiplier
        {
            get
            {
                float t = Mathf.Clamp01((float)(CurrentCrew - minCrew) /
                                        Mathf.Max(1, maxCrew - minCrew));
                return Mathf.Lerp(minCombatMultiplier, maxCombatMultiplier, t);
            }
        }

        /// <summary>Number of crew slots still available for hiring.</summary>
        public int VacantSlots => Mathf.Max(0, maxCrew - CurrentCrew);

        // ══════════════════════════════════════════════════════════════════
        //  Initialization
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Wire dependencies and set the starting crew.
        /// Call from BootyBootstrap after the player ship has been created.
        /// </summary>
        /// <param name="economy">The shared EconomySystem (gold source).</param>
        /// <param name="shipController">The player's ShipController.</param>
        /// <param name="startingCrew">Crew count at game start.</param>
        public void Initialize(
            EconomySystem  economy,
            ShipController shipController,
            int            startingCrew = 5)
        {
            _economy        = economy;
            _shipController = shipController;

            CurrentCrew = Mathf.Clamp(startingCrew, minCrew, maxCrew);
            ApplyCrewStats();

            Debug.Log($"[CrewManager] Initialized. Crew: {CurrentCrew}/{maxCrew}. " +
                      $"SpeedMult: {CrewSpeedMultiplier:F2} CombatMult: {CrewCombatMultiplier:F2}");
        }

        // ══════════════════════════════════════════════════════════════════
        //  Public API
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Hire the given number of crew members.
        /// Deducts <c>count × costPerHead</c> gold. Clamps to available slots.
        /// </summary>
        /// <param name="count">Number of crew to hire. Clamped to free slots.</param>
        /// <returns>True if at least one crew member was hired.</returns>
        public bool HireCrew(int count)
        {
            if (count <= 0) return false;

            // Clamp to available berths
            int maxHirable = Mathf.Max(0, maxCrew - CurrentCrew);
            count = Mathf.Min(count, maxHirable);
            if (count <= 0)
            {
                Debug.Log("[CrewManager] Cannot hire — crew quarters full.");
                return false;
            }

            float totalCost = count * costPerHead;
            if (_economy == null || !_economy.SpendGold(totalCost))
            {
                Debug.Log($"[CrewManager] Insufficient gold to hire {count} crew " +
                           $"(need {totalCost:F0}g).");
                return false;
            }

            CurrentCrew += count;
            ApplyCrewStats();
            OnCrewChanged?.Invoke(CurrentCrew);

            Debug.Log($"[CrewManager] Hired {count} crew for {totalCost:F0}g. " +
                      $"Crew now: {CurrentCrew}/{maxCrew}.");
            return true;
        }

        /// <summary>
        /// Dismiss the given number of crew members (no gold cost).
        /// Cannot dismiss below minCrew.
        /// </summary>
        /// <param name="count">Number of crew to dismiss. Clamped to dismissable count.</param>
        /// <returns>True if at least one crew member was dismissed.</returns>
        public bool DismissCrew(int count)
        {
            if (count <= 0) return false;

            int maxDismissable = Mathf.Max(0, CurrentCrew - minCrew);
            count = Mathf.Min(count, maxDismissable);
            if (count <= 0)
            {
                Debug.Log($"[CrewManager] Cannot dismiss — already at minimum crew ({minCrew}).");
                return false;
            }

            CurrentCrew -= count;
            ApplyCrewStats();
            OnCrewChanged?.Invoke(CurrentCrew);

            Debug.Log($"[CrewManager] Dismissed {count} crew. " +
                      $"Crew now: {CurrentCrew}/{maxCrew}.");
            return true;
        }

        /// <summary>
        /// Get a summary string for UI display.
        /// </summary>
        public string GetCrewSummary()
        {
            return string.Format(
                "Crew: {0}/{1}  Speed: {2}%  Combat: {3}%",
                CurrentCrew, maxCrew,
                Mathf.RoundToInt(CrewSpeedMultiplier * 100f),
                Mathf.RoundToInt(CrewCombatMultiplier * 100f));
        }

        // ══════════════════════════════════════════════════════════════════
        //  Internal
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Push current crew-derived multipliers to ShipController.
        /// </summary>
        private void ApplyCrewStats()
        {
            _shipController?.SetCrewSpeedMultiplier(CrewSpeedMultiplier);
        }
    }
}
