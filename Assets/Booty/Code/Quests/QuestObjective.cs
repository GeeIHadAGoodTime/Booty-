// ---------------------------------------------------------------------------
// QuestObjective.cs — Data struct for a single quest objective + progress tracking
// ---------------------------------------------------------------------------
// Objectives are embedded within QuestData ScriptableObjects.
// QuestManager owns the runtime progress counters — these are the definitions.
// ---------------------------------------------------------------------------

using System;
using UnityEngine;

namespace Booty.Quests
{
    /// <summary>
    /// The kind of activity required to satisfy an objective.
    /// </summary>
    public enum ObjectiveType
    {
        /// <summary>Kill a specified number of enemy ships (optionally of a named faction).</summary>
        KillEnemies,

        /// <summary>Arrive at a named port or within a radius of a world position.</summary>
        ArriveAtLocation,

        /// <summary>Collect a number of items (cargo, treasure, etc.).</summary>
        CollectItems,

        /// <summary>Capture a specific port for the player faction.</summary>
        CapturePort,

        /// <summary>Deliver cargo to a specific port.</summary>
        DeliverCargo,

        /// <summary>Escort a merchant ship to its destination (survive until arrival).</summary>
        EscortShip,
    }

    /// <summary>
    /// Serializable data that describes one objective within a quest.
    /// Stored in <see cref="QuestData.objectives"/> and shared across all instances
    /// of that quest.  Runtime mutable state (current progress) is stored
    /// separately in <see cref="QuestObjectiveProgress"/>.
    /// </summary>
    [Serializable]
    public class QuestObjectiveDef
    {
        [Tooltip("Human-readable label, e.g. 'Sink 3 enemy sloops'.")]
        public string description = "Complete the objective";

        [Tooltip("What kind of activity fulfils this objective.")]
        public ObjectiveType objectiveType = ObjectiveType.KillEnemies;

        [Tooltip("How many actions are needed (kills, items collected, etc.). " +
                 "For arrive/capture objectives, set to 1.")]
        public int requiredCount = 1;

        [Tooltip("Optional faction / item / port ID filter. " +
                 "KillEnemies: faction tag (empty = any). " +
                 "ArriveAtLocation / CapturePort / DeliverCargo: portId. " +
                 "CollectItems: item tag.")]
        public string targetId = "";

        [Tooltip("Optional world position used by ArriveAtLocation when no portId is set.")]
        public Vector3 targetPosition = Vector3.zero;

        [Tooltip("Radius around targetPosition that counts as 'arrived' (world units).")]
        public float arrivalRadius = 15f;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Runtime progress — one per active objective, owned by QuestManager
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Mutable runtime state for one objective of an in-progress quest.
    /// QuestManager creates these when a quest becomes Active and updates them
    /// as events fire.
    /// </summary>
    public class QuestObjectiveProgress
    {
        /// <summary>Reference to the immutable definition.</summary>
        public QuestObjectiveDef Definition { get; }

        /// <summary>How many actions have been completed so far.</summary>
        public int CurrentCount { get; private set; }

        /// <summary>True once CurrentCount >= Definition.requiredCount.</summary>
        public bool IsComplete => CurrentCount >= Definition.requiredCount;

        public QuestObjectiveProgress(QuestObjectiveDef definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        /// <summary>
        /// Increment the progress counter by <paramref name="amount"/>.
        /// Clamps to requiredCount.
        /// </summary>
        public void Advance(int amount = 1)
        {
            CurrentCount = Math.Min(CurrentCount + amount, Definition.requiredCount);
        }

        /// <summary>Set progress to a specific value (used for location checks).</summary>
        public void SetCount(int value)
        {
            CurrentCount = Math.Clamp(value, 0, Definition.requiredCount);
        }

        /// <summary>Friendly display string: "Sink 2 / 3 ships".</summary>
        public override string ToString() =>
            $"{Definition.description} ({CurrentCount}/{Definition.requiredCount})";
    }
}
