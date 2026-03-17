// ---------------------------------------------------------------------------
// QuestManager.cs — Quest lifecycle: available → active → complete / failed
// ---------------------------------------------------------------------------
// Attach to a GameObject (created by BootyBootstrap) or drag into the scene.
// Assign quest assets in the Inspector, or call Initialize() from BootyBootstrap.
//
// Quest Log UI reads quest state via:
//   QuestManager.Instance.GetActiveQuests()
//   QuestManager.Instance.GetAvailableQuests()
//   QuestManager.Instance.GetCompletedQuests()
//
// Other systems report progress via:
//   QuestManager.Instance.ReportKill(faction)
//   QuestManager.Instance.ReportArrival(portId)
//   QuestManager.Instance.ReportItemCollected(itemId)
//   QuestManager.Instance.ReportPortCaptured(portId)
//   QuestManager.Instance.ReportCargoDelivered(portId)
// ---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Booty.Core;
using Booty.Economy;

namespace Booty.Quests
{
    /// <summary>
    /// Possible states of a quest during a play session.
    /// </summary>
    public enum QuestStatus
    {
        /// <summary>Quest exists but prerequisites not met, or chapter not reached.</summary>
        Locked,

        /// <summary>Quest can be started — prerequisites met and chapter unlocked.</summary>
        Available,

        /// <summary>Quest is currently in progress.</summary>
        Active,

        /// <summary>All objectives have been satisfied.</summary>
        Completed,

        /// <summary>Quest timed out or a failure condition was triggered.</summary>
        Failed,
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Runtime Quest Instance
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Live runtime state of a single quest: status, objective progress, timer.
    /// </summary>
    public class QuestInstance
    {
        public QuestData Data { get; }
        public QuestStatus Status { get; internal set; }
        public List<QuestObjectiveProgress> ObjectiveProgress { get; }
        public float ElapsedSeconds { get; internal set; }

        /// <summary>True when every objective is satisfied.</summary>
        public bool AllObjectivesComplete =>
            ObjectiveProgress.All(o => o.IsComplete);

        public QuestInstance(QuestData data)
        {
            Data   = data;
            Status = QuestStatus.Available;

            ObjectiveProgress = data.objectives
                .Select(def => new QuestObjectiveProgress(def))
                .ToList();
        }

        /// <summary>Get first incomplete objective for HUD tip display.</summary>
        public QuestObjectiveProgress CurrentObjective =>
            ObjectiveProgress.FirstOrDefault(o => !o.IsComplete);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  QuestManager MonoBehaviour
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Singleton MonoBehaviour that owns the quest lifecycle.
    /// </summary>
    public class QuestManager : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Inspector
        // ══════════════════════════════════════════════════════════════════

        [Header("Quest Catalogue")]
        [Tooltip("All QuestData assets available in this session. " +
                 "Leave empty to auto-populate with starter quests at runtime.")]
        [SerializeField] private List<QuestData> availableQuestAssets = new();

        // ══════════════════════════════════════════════════════════════════
        //  Singleton
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Global access — set during Awake.</summary>
        public static QuestManager Instance { get; private set; }

        // ══════════════════════════════════════════════════════════════════
        //  Events
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Fired when a quest becomes Active. Arg: the quest instance.</summary>
        public event Action<QuestInstance> OnQuestStarted;

        /// <summary>Fired when an objective makes progress. Args: quest, objective.</summary>
        public event Action<QuestInstance, QuestObjectiveProgress> OnObjectiveProgress;

        /// <summary>Fired when a quest completes successfully.</summary>
        public event Action<QuestInstance> OnQuestCompleted;

        /// <summary>Fired when a quest fails.</summary>
        public event Action<QuestInstance> OnQuestFailed;

        // ══════════════════════════════════════════════════════════════════
        //  State
        // ══════════════════════════════════════════════════════════════════

        private readonly Dictionary<string, QuestInstance> _instances = new();
        private EconomySystem _economySystem;
        private RenownSystem  _renownSystem;

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// Initialize QuestManager with optional system references (for auto-reward).
        /// Called by BootyBootstrap during composition.
        /// </summary>
        /// <param name="quests">Quest assets to register. Pass null to use Inspector list.</param>
        /// <param name="economySystem">Economy system for gold rewards. May be null.</param>
        /// <param name="renownSystem">Renown system for renown rewards. May be null.</param>
        public void Initialize(
            IEnumerable<QuestData> quests,
            EconomySystem economySystem,
            RenownSystem  renownSystem)
        {
            _economySystem = economySystem;
            _renownSystem  = renownSystem;

            // Merge inspector list + code-supplied list
            var combined = new List<QuestData>(availableQuestAssets);
            if (quests != null)
                combined.AddRange(quests);

            // Fall back to starter quests if nothing was wired up
            if (combined.Count == 0)
            {
                combined.AddRange(QuestFactory.CreateStarterQuests());
                Debug.LogWarning("[QuestManager] No quest assets assigned — " +
                                 "loaded starter quests from QuestFactory.");
            }

            foreach (var data in combined)
            {
                if (data == null) continue;
                if (_instances.ContainsKey(data.questId))
                {
                    Debug.LogWarning($"[QuestManager] Duplicate questId '{data.questId}' — skipping.");
                    continue;
                }
                var instance = new QuestInstance(data)
                {
                    Status = EvaluateLockStatus(data)
                };
                _instances[data.questId] = instance;
            }

            Debug.Log($"[QuestManager] Initialized with {_instances.Count} quest(s).");
        }

        private void Update()
        {
            // Tick time limits for active quests
            foreach (var inst in _instances.Values)
            {
                if (inst.Status != QuestStatus.Active) continue;
                if (inst.Data.timeLimitSeconds <= 0f)  continue;

                inst.ElapsedSeconds += Time.deltaTime;
                if (inst.ElapsedSeconds >= inst.Data.timeLimitSeconds)
                {
                    FailQuest(inst);
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Public API — Quest Log UI data source
        // ══════════════════════════════════════════════════════════════════

        /// <summary>All quests currently in Active status.</summary>
        public IReadOnlyList<QuestInstance> GetActiveQuests() =>
            _instances.Values.Where(q => q.Status == QuestStatus.Active).ToList();

        /// <summary>All quests in Available status (can be started).</summary>
        public IReadOnlyList<QuestInstance> GetAvailableQuests() =>
            _instances.Values.Where(q => q.Status == QuestStatus.Available).ToList();

        /// <summary>All quests in Completed status.</summary>
        public IReadOnlyList<QuestInstance> GetCompletedQuests() =>
            _instances.Values.Where(q => q.Status == QuestStatus.Completed).ToList();

        /// <summary>All quests in Failed status.</summary>
        public IReadOnlyList<QuestInstance> GetFailedQuests() =>
            _instances.Values.Where(q => q.Status == QuestStatus.Failed).ToList();

        /// <summary>Get a specific quest instance by ID, or null if not found.</summary>
        public QuestInstance GetQuest(string questId) =>
            _instances.TryGetValue(questId, out var inst) ? inst : null;

        // ══════════════════════════════════════════════════════════════════
        //  Public API — Quest Lifecycle
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Move a quest from Available to Active.
        /// </summary>
        /// <returns>True if the quest was started successfully.</returns>
        public bool StartQuest(string questId)
        {
            if (!_instances.TryGetValue(questId, out var inst))
            {
                Debug.LogWarning($"[QuestManager] StartQuest: questId '{questId}' not found.");
                return false;
            }
            if (inst.Status != QuestStatus.Available)
            {
                Debug.LogWarning($"[QuestManager] StartQuest: quest '{questId}' is not Available " +
                                 $"(current: {inst.Status}).");
                return false;
            }

            inst.Status = QuestStatus.Active;
            Debug.Log($"[QuestManager] Quest started: '{inst.Data.questName}' [{questId}]");
            OnQuestStarted?.Invoke(inst);
            return true;
        }

        /// <summary>
        /// Force-complete a quest regardless of objective progress. (Debug / cutscene use.)
        /// </summary>
        public void ForceComplete(string questId)
        {
            if (_instances.TryGetValue(questId, out var inst))
                CompleteQuest(inst);
        }

        /// <summary>
        /// Force-fail a quest. (Debug / narrative use.)
        /// </summary>
        public void ForceFail(string questId)
        {
            if (_instances.TryGetValue(questId, out var inst))
                FailQuest(inst);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Progress Reporting (called by other gameplay systems)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Report that an enemy ship was killed.
        /// Advances KillEnemies objectives whose faction filter matches.
        /// </summary>
        /// <param name="faction">Faction ID of the killed ship (may be empty/any).</param>
        public void ReportKill(string faction = "")
        {
            AdvanceObjectives(ObjectiveType.KillEnemies, faction, 1);
        }

        /// <summary>
        /// Report that the player has arrived at a location / port.
        /// Advances ArriveAtLocation objectives whose targetId matches portId.
        /// </summary>
        /// <param name="portId">The ID of the port or waypoint reached.</param>
        public void ReportArrival(string portId)
        {
            AdvanceObjectives(ObjectiveType.ArriveAtLocation, portId, 1);
        }

        /// <summary>
        /// Report that an item was collected.
        /// Advances CollectItems objectives whose targetId matches itemId.
        /// </summary>
        public void ReportItemCollected(string itemId, int count = 1)
        {
            AdvanceObjectives(ObjectiveType.CollectItems, itemId, count);
        }

        /// <summary>
        /// Report that a port was captured.
        /// Advances CapturePort objectives whose targetId matches portId.
        /// </summary>
        public void ReportPortCaptured(string portId)
        {
            AdvanceObjectives(ObjectiveType.CapturePort, portId, 1);
        }

        /// <summary>
        /// Report that cargo was delivered to a port.
        /// Advances DeliverCargo objectives whose targetId matches portId.
        /// </summary>
        public void ReportCargoDelivered(string portId)
        {
            AdvanceObjectives(ObjectiveType.DeliverCargo, portId, 1);
        }

        /// <summary>
        /// Report that an escort target has been successfully delivered.
        /// Advances EscortShip objectives.
        /// </summary>
        public void ReportEscortComplete(string targetId = "")
        {
            AdvanceObjectives(ObjectiveType.EscortShip, targetId, 1);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Internal
        // ══════════════════════════════════════════════════════════════════

        private void AdvanceObjectives(ObjectiveType type, string targetId, int amount)
        {
            foreach (var inst in _instances.Values)
            {
                if (inst.Status != QuestStatus.Active) continue;

                bool progressed = false;
                foreach (var obj in inst.ObjectiveProgress)
                {
                    if (obj.IsComplete) continue;
                    if (obj.Definition.objectiveType != type) continue;

                    // Filter by targetId: empty filter = matches anything
                    bool filterMatches = string.IsNullOrEmpty(obj.Definition.targetId)
                                     || obj.Definition.targetId.Equals(targetId,
                                            StringComparison.OrdinalIgnoreCase);
                    if (!filterMatches) continue;

                    obj.Advance(amount);
                    progressed = true;
                    OnObjectiveProgress?.Invoke(inst, obj);
                    Debug.Log($"[QuestManager] [{inst.Data.questName}] {obj}");
                }

                if (progressed && inst.AllObjectivesComplete)
                    CompleteQuest(inst);
            }
        }

        private void CompleteQuest(QuestInstance inst)
        {
            if (inst.Status == QuestStatus.Completed) return;

            inst.Status = QuestStatus.Completed;
            Debug.Log($"[QuestManager] Quest COMPLETED: '{inst.Data.questName}' — " +
                      $"{inst.Data.completionText}");

            GrantRewards(inst);
            OnQuestCompleted?.Invoke(inst);

            // Re-evaluate locked quests — prerequisites may now be met
            RefreshAvailability();
        }

        private void FailQuest(QuestInstance inst)
        {
            if (inst.Status == QuestStatus.Failed) return;

            inst.Status = QuestStatus.Failed;
            Debug.Log($"[QuestManager] Quest FAILED: '{inst.Data.questName}' — " +
                      $"{inst.Data.failureText}");

            OnQuestFailed?.Invoke(inst);
        }

        private void GrantRewards(QuestInstance inst)
        {
            var reward = inst.Data.reward;
            if (reward == null) return;

            if (reward.goldAmount > 0f && _economySystem != null)
            {
                _economySystem.AddGold(reward.goldAmount);
                Debug.Log($"[QuestManager] Quest reward: +{reward.goldAmount:F0} gold.");
            }

            if (reward.renownAmount > 0f && _renownSystem != null)
            {
                // RenownSystem exposes AddRenown(float) added by the renown system
                _renownSystem.AddRenown(reward.renownAmount);
                Debug.Log($"[QuestManager] Quest reward: +{reward.renownAmount:F0} renown.");
            }
        }

        /// <summary>
        /// Re-check Locked quests to see if any have become Available (e.g. after
        /// completing a prerequisite).
        /// </summary>
        private void RefreshAvailability()
        {
            foreach (var inst in _instances.Values)
            {
                if (inst.Status != QuestStatus.Locked) continue;
                inst.Status = EvaluateLockStatus(inst.Data);
            }
        }

        private QuestStatus EvaluateLockStatus(QuestData data)
        {
            if (data.prerequisiteQuestIds == null || data.prerequisiteQuestIds.Count == 0)
                return QuestStatus.Available;

            foreach (var preReqId in data.prerequisiteQuestIds)
            {
                if (!_instances.TryGetValue(preReqId, out var preReq) ||
                    preReq.Status != QuestStatus.Completed)
                    return QuestStatus.Locked;
            }

            return QuestStatus.Available;
        }
    }
}
