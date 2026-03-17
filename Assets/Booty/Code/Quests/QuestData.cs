// ---------------------------------------------------------------------------
// QuestData.cs — ScriptableObject: defines a quest's static data
// ---------------------------------------------------------------------------
// Create quest assets via the Unity menu:
//   Assets → Create → Booty → Quest
//
// Assign assets to QuestManager.availableQuests in the Inspector, or rely on
// QuestFactory.CreateStarterQuests() for code-only bootstrap.
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace Booty.Quests
{
    /// <summary>
    /// Reward granted upon quest completion.
    /// </summary>
    [System.Serializable]
    public class QuestReward
    {
        [Tooltip("Gold awarded on completion (0 = none).")]
        public float goldAmount = 0f;

        [Tooltip("Renown awarded on completion (0 = none).")]
        public float renownAmount = 0f;

        [Tooltip("Optional item / unlock ID granted (empty = none).")]
        public string itemId = "";

        [Tooltip("Human-readable reward summary, e.g. '200 gold + 10 renown'.")]
        public string displayText = "";
    }

    /// <summary>
    /// Immutable definition of a quest: metadata, objectives, and rewards.
    /// Runtime state (status, progress) is tracked by <see cref="QuestManager"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "NewQuest", menuName = "Booty/Quest")]
    public class QuestData : ScriptableObject
    {
        // ══════════════════════════════════════════════════════════════════
        //  Identity
        // ══════════════════════════════════════════════════════════════════

        [Header("Identity")]
        [Tooltip("Unique machine-readable ID, e.g. 'quest_treasure_hunt'. Must be unique across all quests.")]
        public string questId = "quest_new";

        [Tooltip("Player-visible quest title, e.g. 'The Lost Treasure of El Diablo'.")]
        public string questName = "New Quest";

        [Tooltip("Narrative description shown in the quest log.")]
        [TextArea(3, 6)]
        public string description = "A new adventure awaits.";

        [Tooltip("Short flavour text shown when the quest is first offered.")]
        [TextArea(2, 4)]
        public string offerText = "A quest has been found.";

        [Tooltip("Text shown when the quest completes successfully.")]
        [TextArea(2, 4)]
        public string completionText = "Quest complete!";

        [Tooltip("Text shown when the quest fails.")]
        [TextArea(2, 4)]
        public string failureText = "The quest has failed.";

        // ══════════════════════════════════════════════════════════════════
        //  Categorisation
        // ══════════════════════════════════════════════════════════════════

        [Header("Categorisation")]
        [Tooltip("Quest chapter / act number — used to control when quests become available.")]
        public int chapter = 1;

        [Tooltip("Optional prerequisite quest IDs — all must be Completed before this quest is Available.")]
        public List<string> prerequisiteQuestIds = new();

        [Tooltip("Optional time limit in seconds (0 = no limit).")]
        public float timeLimitSeconds = 0f;

        // ══════════════════════════════════════════════════════════════════
        //  Objectives
        // ══════════════════════════════════════════════════════════════════

        [Header("Objectives")]
        [Tooltip("Ordered list of objectives. All must be complete for the quest to succeed.")]
        public List<QuestObjectiveDef> objectives = new();

        // ══════════════════════════════════════════════════════════════════
        //  Reward
        // ══════════════════════════════════════════════════════════════════

        [Header("Reward")]
        [Tooltip("What the player earns upon completion.")]
        public QuestReward reward = new();
    }
}
