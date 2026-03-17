// ---------------------------------------------------------------------------
// QuestFactory.cs — Programmatic builder for the 5 starter quests
// ---------------------------------------------------------------------------
// Used when no ScriptableObject .asset files are assigned to QuestManager.
// Also used by EditMode tests.
//
// Each starter quest is self-contained: objectives, rewards, flavour text.
// The 5 quests cover the core pillars of Booty! gameplay:
//   1. Treasure Hunt            — explore + collect
//   2. Escort Merchant Ship     — protect NPC
//   3. Defeat Pirate Captain    — elite combat
//   4. Deliver Cargo to Port    — trade + economy
//   5. Explore Unknown Island   — exploration
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace Booty.Quests
{
    /// <summary>
    /// Static factory that creates the 5 starter <see cref="QuestData"/> instances
    /// at runtime using <see cref="ScriptableObject.CreateInstance{T}"/>.
    /// </summary>
    public static class QuestFactory
    {
        // ══════════════════════════════════════════════════════════════════
        //  Public API
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Create and return all 5 starter quest instances.
        /// </summary>
        public static IReadOnlyList<QuestData> CreateStarterQuests()
        {
            return new List<QuestData>
            {
                CreateTreasureHunt(),
                CreateEscortMerchant(),
                CreateDefeatPirateCapitain(),
                CreateDeliverCargo(),
                CreateExploreIsland(),
            };
        }

        // ══════════════════════════════════════════════════════════════════
        //  Quest 1 — The Lost Treasure of El Diablo
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Quest 1: Treasure Hunt — sail to a hidden cove, collect 3 treasure chests.
        /// Reward: 350 gold + 15 renown.
        /// </summary>
        public static QuestData CreateTreasureHunt()
        {
            var q = ScriptableObject.CreateInstance<QuestData>();
            q.name                = "Q1_TreasureHunt";
            q.questId             = "quest_treasure_hunt";
            q.questName           = "The Lost Treasure of El Diablo";
            q.description         = "Old sea charts point to a hidden cove in the northern isles " +
                                    "where the legendary pirate El Diablo stashed his life's plunder. " +
                                    "Sail there, find the treasure chests, and bring home the gold.";
            q.offerText           = "A tattered map has fallen into your hands. The X is real — " +
                                    "you can feel it in your bones.";
            q.completionText      = "The legend is real! El Diablo's hoard is yours — every last " +
                                    "doubloon of it.";
            q.failureText         = "The treasure remained hidden. Another hunter must have " +
                                    "beaten you to it.";
            q.chapter             = 1;

            q.objectives = new System.Collections.Generic.List<QuestObjectiveDef>
            {
                new QuestObjectiveDef
                {
                    description     = "Sail to the Hidden Cove",
                    objectiveType   = ObjectiveType.ArriveAtLocation,
                    requiredCount   = 1,
                    targetId        = "port_hidden_cove",
                    arrivalRadius   = 20f,
                },
                new QuestObjectiveDef
                {
                    description     = "Collect treasure chests",
                    objectiveType   = ObjectiveType.CollectItems,
                    requiredCount   = 3,
                    targetId        = "item_treasure_chest",
                },
            };

            q.reward = new QuestReward
            {
                goldAmount   = 350f,
                renownAmount = 15f,
                displayText  = "350 gold + 15 renown",
            };

            return q;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Quest 2 — Safe Passage for the Merchant
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Quest 2: Escort a merchant ship to Port Royale.
        /// Reward: 200 gold + 20 renown.
        /// </summary>
        public static QuestData CreateEscortMerchant()
        {
            var q = ScriptableObject.CreateInstance<QuestData>();
            q.name                = "Q2_EscortMerchant";
            q.questId             = "quest_escort_merchant";
            q.questName           = "Safe Passage for the Merchant";
            q.description         = "Captain Marguerite Santos of the merchant brig 'Esperanza' " +
                                    "fears pirates in the straits. She'll pay handsomely if you " +
                                    "escort her ship safely to Port Royale.";
            q.offerText           = "The merchant captain wrings her hands nervously. 'The straits " +
                                    "are thick with pirates. I cannot make it alone.'";
            q.completionText      = "The Esperanza docks safely at Port Royale. Captain Santos " +
                                    "presses a pouch of gold into your hand with genuine gratitude.";
            q.failureText         = "The Esperanza was sunk. Her cargo is lost, her crew scattered. " +
                                    "The trade routes grow ever more dangerous.";
            q.chapter             = 1;
            q.timeLimitSeconds    = 300f; // 5-minute escort window

            q.objectives = new System.Collections.Generic.List<QuestObjectiveDef>
            {
                new QuestObjectiveDef
                {
                    description     = "Escort the Esperanza to Port Royale",
                    objectiveType   = ObjectiveType.EscortShip,
                    requiredCount   = 1,
                    targetId        = "ship_esperanza",
                },
            };

            q.reward = new QuestReward
            {
                goldAmount   = 200f,
                renownAmount = 20f,
                displayText  = "200 gold + 20 renown",
            };

            return q;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Quest 3 — Hunt the Dread Captain
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Quest 3: Defeat the pirate captain and 3 escort ships.
        /// Reward: 500 gold + 40 renown.
        /// </summary>
        public static QuestData CreateDefeatPirateCapitain()
        {
            var q = ScriptableObject.CreateInstance<QuestData>();
            q.name                = "Q3_DefeatCaptain";
            q.questId             = "quest_defeat_pirate_captain";
            q.questName           = "Hunt the Dread Captain";
            q.description         = "Captain Blackthorn has been terrorising these waters for too " +
                                    "long. The Governor of Port Nassau has put a bounty on his head. " +
                                    "Sink his fleet and send Blackthorn to Davy Jones.";
            q.offerText           = "A wanted poster is nailed to the dock. The reward is substantial. " +
                                    "The man is dangerous.";
            q.completionText      = "Blackthorn's flagship lists and sinks beneath the waves. " +
                                    "The Governor will hear of your heroism — and your reward.";
            q.failureText         = "Blackthorn has retreated into fog. He will return.";
            q.chapter             = 1;

            q.objectives = new System.Collections.Generic.List<QuestObjectiveDef>
            {
                new QuestObjectiveDef
                {
                    description     = "Sink Blackthorn's escort ships",
                    objectiveType   = ObjectiveType.KillEnemies,
                    requiredCount   = 3,
                    targetId        = "faction_blackthorn",
                },
                new QuestObjectiveDef
                {
                    description     = "Sink Captain Blackthorn's flagship",
                    objectiveType   = ObjectiveType.KillEnemies,
                    requiredCount   = 1,
                    targetId        = "ship_blackthorn_flagship",
                },
            };

            q.reward = new QuestReward
            {
                goldAmount   = 500f,
                renownAmount = 40f,
                displayText  = "500 gold + 40 renown",
            };

            return q;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Quest 4 — Merchant Run to San Cristobal
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Quest 4: Pick up cargo and deliver it to San Cristobal.
        /// Reward: 250 gold + 10 renown.
        /// </summary>
        public static QuestData CreateDeliverCargo()
        {
            var q = ScriptableObject.CreateInstance<QuestData>();
            q.name                = "Q4_DeliverCargo";
            q.questId             = "quest_deliver_cargo";
            q.questName           = "Merchant Run to San Cristobal";
            q.description         = "A merchant at Port Nassau has a shipment of spices that must " +
                                    "reach San Cristobal before the market opens. Time is money, " +
                                    "and he's offering good money for fast delivery.";
            q.offerText           = "'These spices are worth a fortune in San Cristobal!' the merchant " +
                                    "bellows. 'Get them there and I'll make it worth your while.'";
            q.completionText      = "The spices arrive in perfect condition. The merchant of " +
                                    "San Cristobal is delighted — and generous.";
            q.failureText         = "The cargo was lost. The merchant will find another courier.";
            q.chapter             = 1;
            q.timeLimitSeconds    = 600f; // 10-minute delivery window

            q.objectives = new System.Collections.Generic.List<QuestObjectiveDef>
            {
                new QuestObjectiveDef
                {
                    description     = "Pick up the cargo at Port Nassau",
                    objectiveType   = ObjectiveType.ArriveAtLocation,
                    requiredCount   = 1,
                    targetId        = "port_nassau",
                },
                new QuestObjectiveDef
                {
                    description     = "Deliver the cargo to San Cristobal",
                    objectiveType   = ObjectiveType.DeliverCargo,
                    requiredCount   = 1,
                    targetId        = "port_san_cristobal",
                },
            };

            q.reward = new QuestReward
            {
                goldAmount   = 250f,
                renownAmount = 10f,
                displayText  = "250 gold + 10 renown",
            };

            return q;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Quest 5 — The Uncharted Isle
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Quest 5: Explore an unknown island — arrive at the island location.
        /// Reward: 150 gold + 25 renown.
        /// </summary>
        public static QuestData CreateExploreIsland()
        {
            var q = ScriptableObject.CreateInstance<QuestData>();
            q.name                = "Q5_ExploreIsland";
            q.questId             = "quest_explore_island";
            q.questName           = "The Uncharted Isle";
            q.description         = "Fishermen speak of an island that doesn't appear on any chart, " +
                                    "surrounded by mist and guarded by treacherous reefs. Some say " +
                                    "it holds ruins older than the colonies. Sail there and see " +
                                    "what you find.";
            q.offerText           = "The fisherman traces a circle on your chart with a weathered " +
                                    "finger. 'Nobody goes there. That's exactly why you should.'";
            q.completionText      = "The mist parts to reveal the island's ancient shore. Whatever " +
                                    "secrets it holds, you're the first to find them in a generation.";
            q.failureText         = "The mists closed in and you lost your bearing. The island " +
                                    "remains a mystery.";
            q.chapter             = 1;

            q.objectives = new System.Collections.Generic.List<QuestObjectiveDef>
            {
                new QuestObjectiveDef
                {
                    description     = "Navigate to the uncharted island",
                    objectiveType   = ObjectiveType.ArriveAtLocation,
                    requiredCount   = 1,
                    targetId        = "island_uncharted",
                    targetPosition  = new Vector3(220f, 0f, 180f),
                    arrivalRadius   = 30f,
                },
                new QuestObjectiveDef
                {
                    description     = "Search the ruins for artefacts",
                    objectiveType   = ObjectiveType.CollectItems,
                    requiredCount   = 2,
                    targetId        = "item_ruin_artefact",
                },
            };

            q.reward = new QuestReward
            {
                goldAmount   = 150f,
                renownAmount = 25f,
                displayText  = "150 gold + 25 renown",
            };

            return q;
        }
    }
}
