// ---------------------------------------------------------------------------
// QuestManagerTests.cs — EditMode tests for the S3.3 Quest System
// ---------------------------------------------------------------------------
// Validates:
//   1. QuestFactory creates 5 starter quests with the correct IDs.
//   2. QuestManager initialises with 5 quests, all Available.
//   3. StartQuest transitions a quest from Available → Active.
//   4. ReportKill advances KillEnemies objectives and completes the quest.
//   5. ReportArrival + ReportCargoDelivered complete a multi-objective quest.
//   6. Completing a quest fires OnQuestCompleted event.
//   7. ForceComplete works regardless of objective progress.
//   8. ForceFail fires OnQuestFailed event.
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Booty.Quests;

namespace Booty.Tests
{
    [TestFixture]
    public class QuestManagerTests
    {
        // ── Helpers ──────────────────────────────────────────────────────────

        private QuestManager MakeManager(IEnumerable<QuestData> quests = null)
        {
            var go = new GameObject("TestQuestManager");
            var mgr = go.AddComponent<QuestManager>();
            mgr.Initialize(quests, economySystem: null, renownSystem: null);
            return mgr;
        }

        private void Teardown(QuestManager mgr)
        {
            if (mgr != null && mgr.gameObject != null)
                Object.DestroyImmediate(mgr.gameObject);
        }

        // ── Test 1: QuestFactory creates 5 quests ────────────────────────────

        [Test]
        public void QuestFactory_Creates_FiveStarterQuests()
        {
            var quests = QuestFactory.CreateStarterQuests();
            Assert.AreEqual(5, quests.Count,
                "QuestFactory.CreateStarterQuests() must return exactly 5 quests.");

            // Verify each has a non-empty questId and at least one objective
            foreach (var q in quests)
            {
                Assert.IsNotNull(q, "Quest must not be null");
                Assert.IsNotEmpty(q.questId, $"Quest '{q.name}' must have a questId");
                Assert.IsNotEmpty(q.questName, $"Quest '{q.questId}' must have a questName");
                Assert.IsNotEmpty(q.objectives,
                    $"Quest '{q.questId}' must have at least one objective");
                Assert.Greater(q.reward.goldAmount, 0f,
                    $"Quest '{q.questId}' must have a positive gold reward");
            }
        }

        [Test]
        public void QuestFactory_AllFive_HaveUniqueIds()
        {
            var quests = QuestFactory.CreateStarterQuests();
            var ids = new HashSet<string>();
            foreach (var q in quests)
            {
                Assert.IsTrue(ids.Add(q.questId),
                    $"Duplicate questId detected: '{q.questId}'");
            }
        }

        [Test]
        public void QuestFactory_ExpectedQuestIds_Present()
        {
            var quests = QuestFactory.CreateStarterQuests();
            var ids = new HashSet<string>();
            foreach (var q in quests) ids.Add(q.questId);

            Assert.Contains("quest_treasure_hunt",         ids, "Treasure hunt quest missing");
            Assert.Contains("quest_escort_merchant",        ids, "Escort quest missing");
            Assert.Contains("quest_defeat_pirate_captain",  ids, "Defeat captain quest missing");
            Assert.Contains("quest_deliver_cargo",          ids, "Deliver cargo quest missing");
            Assert.Contains("quest_explore_island",         ids, "Explore island quest missing");
        }

        // ── Test 2: QuestManager initialises all Available ──────────────────

        [Test]
        public void QuestManager_Initialize_AllQuestsAvailable()
        {
            var mgr = MakeManager();
            try
            {
                Assert.AreEqual(5, mgr.GetAvailableQuests().Count,
                    "All 5 starter quests should be Available after Initialize.");
                Assert.AreEqual(0, mgr.GetActiveQuests().Count,
                    "No quests should be Active immediately after Initialize.");
            }
            finally { Teardown(mgr); }
        }

        // ── Test 3: StartQuest transitions Available → Active ────────────────

        [Test]
        public void StartQuest_TransitionsAvailableToActive()
        {
            var mgr = MakeManager();
            try
            {
                bool result = mgr.StartQuest("quest_treasure_hunt");

                Assert.IsTrue(result, "StartQuest should return true for an Available quest.");
                Assert.AreEqual(QuestStatus.Active,
                    mgr.GetQuest("quest_treasure_hunt").Status,
                    "Quest should be Active after StartQuest.");
                Assert.AreEqual(4, mgr.GetAvailableQuests().Count,
                    "One quest moved to Active, leaving 4 Available.");
                Assert.AreEqual(1, mgr.GetActiveQuests().Count,
                    "Exactly one quest should now be Active.");
            }
            finally { Teardown(mgr); }
        }

        [Test]
        public void StartQuest_ReturnsFalse_ForUnknownId()
        {
            var mgr = MakeManager();
            try
            {
                bool result = mgr.StartQuest("quest_does_not_exist");
                Assert.IsFalse(result, "StartQuest should return false for unknown quest.");
            }
            finally { Teardown(mgr); }
        }

        // ── Test 4: Kill objectives advance and complete quest ───────────────

        [Test]
        public void ReportKill_AdvancesKillObjective_CompletesQuest()
        {
            // Defeat Captain quest: 3 escort kills + 1 flagship kill
            var mgr = MakeManager();
            try
            {
                mgr.StartQuest("quest_defeat_pirate_captain");
                var inst = mgr.GetQuest("quest_defeat_pirate_captain");

                // Kill 3 escort ships (faction_blackthorn)
                for (int i = 0; i < 3; i++)
                    mgr.ReportKill("faction_blackthorn");

                Assert.IsTrue(inst.ObjectiveProgress[0].IsComplete,
                    "First objective (3 escort kills) should be complete after 3 ReportKill calls.");
                Assert.IsFalse(inst.AllObjectivesComplete,
                    "Quest should not be complete until flagship is also sunk.");

                // Kill the flagship
                mgr.ReportKill("ship_blackthorn_flagship");

                Assert.AreEqual(QuestStatus.Completed, inst.Status,
                    "Quest should be Completed after all objectives are satisfied.");
            }
            finally { Teardown(mgr); }
        }

        [Test]
        public void ReportKill_EmptyFaction_MatchesAnyFactionObjective()
        {
            // Create a quest with empty-faction kill objective
            var data = QuestFactory.CreateTreasureHunt();
            // Manufacture a simple kill quest with no faction filter
            var simpleKillData = ScriptableObject.CreateInstance<QuestData>();
            simpleKillData.questId   = "test_kill";
            simpleKillData.questName = "Test Kill Quest";
            simpleKillData.objectives = new System.Collections.Generic.List<QuestObjectiveDef>
            {
                new QuestObjectiveDef
                {
                    description   = "Kill any enemy",
                    objectiveType = ObjectiveType.KillEnemies,
                    requiredCount = 2,
                    targetId      = "",  // empty = any faction
                }
            };
            simpleKillData.reward = new QuestReward { goldAmount = 100f };

            var mgr = MakeManager(new[] { simpleKillData });
            try
            {
                mgr.StartQuest("test_kill");
                mgr.ReportKill("random_faction");
                mgr.ReportKill("another_faction");

                Assert.AreEqual(QuestStatus.Completed, mgr.GetQuest("test_kill").Status,
                    "Quest with empty faction filter should accept kills from any faction.");
            }
            finally { Teardown(mgr); }
        }

        // ── Test 5: Multi-objective quest (arrive + deliver) ─────────────────

        [Test]
        public void ReportArrival_ThenDeliverCargo_CompletesDeliverQuest()
        {
            var mgr = MakeManager();
            try
            {
                mgr.StartQuest("quest_deliver_cargo");

                // Step 1: arrive at nassau (pickup)
                mgr.ReportArrival("port_nassau");
                var inst = mgr.GetQuest("quest_deliver_cargo");
                Assert.IsTrue(inst.ObjectiveProgress[0].IsComplete,
                    "Arrival at port_nassau should complete the first objective.");
                Assert.AreEqual(QuestStatus.Active, inst.Status,
                    "Quest should still be Active after first objective.");

                // Step 2: deliver to san_cristobal
                mgr.ReportCargoDelivered("port_san_cristobal");
                Assert.AreEqual(QuestStatus.Completed, inst.Status,
                    "Quest should be Completed after both objectives.");
            }
            finally { Teardown(mgr); }
        }

        // ── Test 6: OnQuestCompleted event fires ─────────────────────────────

        [Test]
        public void OnQuestCompleted_FiredOnce_WhenQuestFinishes()
        {
            var mgr = MakeManager();
            try
            {
                int fired = 0;
                mgr.OnQuestCompleted += _ => fired++;

                mgr.StartQuest("quest_explore_island");
                mgr.ReportArrival("island_uncharted");
                mgr.ReportItemCollected("item_ruin_artefact", 2);

                Assert.AreEqual(1, fired, "OnQuestCompleted should fire exactly once.");
            }
            finally { Teardown(mgr); }
        }

        // ── Test 7: ForceComplete ─────────────────────────────────────────────

        [Test]
        public void ForceComplete_CompletesQuest_IgnoringObjectives()
        {
            var mgr = MakeManager();
            try
            {
                mgr.StartQuest("quest_treasure_hunt");
                mgr.ForceComplete("quest_treasure_hunt");

                Assert.AreEqual(QuestStatus.Completed,
                    mgr.GetQuest("quest_treasure_hunt").Status,
                    "ForceComplete should set status to Completed.");
            }
            finally { Teardown(mgr); }
        }

        // ── Test 8: ForceFail ─────────────────────────────────────────────────

        [Test]
        public void ForceFail_FailsQuest_AndFiresEvent()
        {
            var mgr = MakeManager();
            try
            {
                int failFired = 0;
                mgr.OnQuestFailed += _ => failFired++;

                mgr.StartQuest("quest_escort_merchant");
                mgr.ForceFail("quest_escort_merchant");

                Assert.AreEqual(QuestStatus.Failed,
                    mgr.GetQuest("quest_escort_merchant").Status,
                    "ForceFail should set status to Failed.");
                Assert.AreEqual(1, failFired, "OnQuestFailed should fire exactly once.");
            }
            finally { Teardown(mgr); }
        }

        // ── Test 9: GetCompletedQuests ────────────────────────────────────────

        [Test]
        public void GetCompletedQuests_ReturnsOnlyCompleted()
        {
            var mgr = MakeManager();
            try
            {
                mgr.StartQuest("quest_treasure_hunt");
                mgr.ForceComplete("quest_treasure_hunt");

                var completed = mgr.GetCompletedQuests();
                Assert.AreEqual(1, completed.Count, "Exactly one quest completed.");
                Assert.AreEqual("quest_treasure_hunt", completed[0].Data.questId);
            }
            finally { Teardown(mgr); }
        }

        // ── Test 10: 5 starter quests each have non-empty completionText ──────

        [Test]
        public void StarterQuests_HaveCompletionAndFailureText()
        {
            foreach (var q in QuestFactory.CreateStarterQuests())
            {
                Assert.IsNotEmpty(q.completionText,
                    $"Quest '{q.questId}' must have completionText");
                Assert.IsNotEmpty(q.failureText,
                    $"Quest '{q.questId}' must have failureText");
            }
        }
    }
}
