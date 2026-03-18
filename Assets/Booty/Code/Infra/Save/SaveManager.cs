// ---------------------------------------------------------------------------
// SaveManager.cs — Multi-slot persistent save/load with AutoSave
// ---------------------------------------------------------------------------
// Provides Save(slot), Load(slot), Delete(slot), ListSlots() API on top of
// JsonUtility + Application.persistentDataPath (per S2-SAVE spec).
//
// AutoSave triggers:
//   (a) On port arrival / capture — via PortSystem.OnPortCaptured event
//   (b) Every 5 minutes            — via AutoSaveCoroutine
//   (c) On application quit        — via OnApplicationQuit()
//
// State captured from live systems:
//   - EconomySystem    : player gold
//   - CargoInventory   : goods held (by goodsId + quantity)
//   - QuestManager     : quest status + per-objective progress
//   - ShipController   : world position + rotation for respawn
//
// State restored into live systems via Load():
//   - EconomySystem.SetGold()
//   - CargoInventory.ClearAll() + AddGoods()
//   - QuestManager.StartQuest() / ForceComplete() / ForceFail()
//
// ---------------------------------------------------------------------------
// USAGE (called from BootyBootstrap):
//
//   var saveManagerGO = new GameObject("SaveManager");
//   _saveManager = saveManagerGO.AddComponent<SaveManager>();
//   // ... after all systems are created ...
//   _saveManager.Initialize(_economySystem, _cargoInventory, shipController, portSystem);
//   _saveManager.Load(0);   // restore auto-save slot on game start
//
// ---------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Booty.Economy;
using Booty.Quests;
using Booty.Ships;
using Booty.Ports;

namespace Booty.Save
{
    /// <summary>
    /// Multi-slot game save manager. Captures, persists, and restores full game state.
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Constants
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Save file name pattern. {0} = slot index.</summary>
        private const string SAVE_FILE_FORMAT = "booty_save_{0}.json";

        /// <summary>Periodic auto-save interval in seconds (5 minutes).</summary>
        private const float AUTO_SAVE_INTERVAL = 300f;

        private const string SAVE_VERSION = "1.0";

        /// <summary>Slot 0 is reserved for the auto-save. Slots 1-3 are manual.</summary>
        public const int AUTO_SAVE_SLOT = 0;

        // ══════════════════════════════════════════════════════════════════
        //  System References
        // ══════════════════════════════════════════════════════════════════

        private EconomySystem  _economySystem;
        private CargoInventory _cargoInventory;
        private ShipController _shipController;
        private PortSystem     _portSystem;

        // ══════════════════════════════════════════════════════════════════
        //  Runtime State
        // ══════════════════════════════════════════════════════════════════

        private string              _currentPortId   = "";
        private readonly HashSet<string> _discoveredPorts = new HashSet<string>();
        private bool                _initialized;

        // ══════════════════════════════════════════════════════════════════
        //  Events
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Fired after a successful save. Arg: slot index.</summary>
        public event Action<int> OnSaved;

        /// <summary>Fired after a successful load. Arg: slot index.</summary>
        public event Action<int> OnLoaded;

        // ══════════════════════════════════════════════════════════════════
        //  Initialisation
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Wire SaveManager to live gameplay systems.
        /// Call from BootyBootstrap AFTER all systems (EconomySystem, CargoInventory,
        /// QuestManager, ShipController, PortSystem) have been created and initialized.
        /// </summary>
        /// <param name="economySystem">Source of player gold.</param>
        /// <param name="cargoInventory">Player cargo hold to capture and restore.</param>
        /// <param name="shipController">Player ship for position/rotation.</param>
        /// <param name="portSystem">Port system — subscribes to OnPortCaptured for auto-save.</param>
        public void Initialize(
            EconomySystem  economySystem,
            CargoInventory cargoInventory,
            ShipController shipController,
            PortSystem     portSystem)
        {
            _economySystem  = economySystem;
            _cargoInventory = cargoInventory;
            _shipController = shipController;
            _portSystem     = portSystem;

            if (_portSystem != null)
                _portSystem.OnPortCaptured += OnPortCapturedHandler;

            _initialized = true;

            Debug.Log("[SaveManager] Initialized. Save dir: " + Application.persistentDataPath);

            // Start 5-minute periodic auto-save
            StartCoroutine(AutoSaveCoroutine());
        }

        private void OnDestroy()
        {
            if (_portSystem != null)
                _portSystem.OnPortCaptured -= OnPortCapturedHandler;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Public API
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Capture current game state from all wired systems and write it to disk.
        /// </summary>
        /// <param name="slot">Slot index (0 = auto-save, 1-3 = manual slots).</param>
        public void Save(int slot)
        {
            if (!_initialized)
            {
                Debug.LogWarning("[SaveManager] Save called before Initialize() — skipping.");
                return;
            }

            SaveData data = CaptureState(slot);
            string   path = GetSavePath(slot);

            try
            {
                string json = JsonUtility.ToJson(data, prettyPrint: true);
                File.WriteAllText(path, json);
                Debug.Log($"[SaveManager] Saved slot {slot} → {path}");
                OnSaved?.Invoke(slot);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Save(slot={slot}) failed: {e.Message}");
            }
        }

        /// <summary>
        /// Load a save file from disk and restore game state into the wired systems.
        /// </summary>
        /// <param name="slot">Slot index to load.</param>
        /// <returns>The SaveData loaded, or null if no save exists for this slot.</returns>
        public SaveData Load(int slot)
        {
            string path = GetSavePath(slot);

            if (!File.Exists(path))
            {
                Debug.Log($"[SaveManager] No save file at slot {slot} ({path}).");
                return null;
            }

            try
            {
                string   json = File.ReadAllText(path);
                SaveData data = JsonUtility.FromJson<SaveData>(json);

                if (data == null)
                {
                    Debug.LogWarning($"[SaveManager] Deserialisation returned null for slot {slot}.");
                    return null;
                }

                if (_initialized)
                    RestoreState(data);

                Debug.Log($"[SaveManager] Loaded slot {slot} (timestamp: {data.timestamp}).");
                OnLoaded?.Invoke(slot);
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Load(slot={slot}) failed: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Delete the save file for the given slot. No-op if no file exists.
        /// </summary>
        public void Delete(int slot)
        {
            string path = GetSavePath(slot);

            if (File.Exists(path))
            {
                File.Delete(path);
                Debug.Log($"[SaveManager] Deleted save at slot {slot}.");
            }
            else
            {
                Debug.Log($"[SaveManager] Delete(slot={slot}): nothing to delete.");
            }
        }

        /// <summary>
        /// Return the indices of all slots that have save files on disk (slots 0–9).
        /// </summary>
        public int[] ListSlots()
        {
            var result = new List<int>();
            string dir = Application.persistentDataPath;

            for (int i = 0; i <= 9; i++)
            {
                if (File.Exists(Path.Combine(dir, string.Format(SAVE_FILE_FORMAT, i))))
                    result.Add(i);
            }

            return result.ToArray();
        }

        /// <summary>
        /// Trigger an immediate auto-save to slot 0.
        /// </summary>
        public void AutoSave()
        {
            Debug.Log("[SaveManager] Auto-saving...");
            Save(AUTO_SAVE_SLOT);
        }

        /// <summary>
        /// Record a port arrival (or capture) and trigger an auto-save.
        /// Called by BootyBootstrap when portSystem.OnPortCaptured fires or
        /// when PortInteraction signals an arrival.
        /// </summary>
        /// <param name="portId">ID of the port arrived at or captured.</param>
        public void NotifyPortArrival(string portId)
        {
            _currentPortId = portId;
            _discoveredPorts.Add(portId);
            Debug.Log($"[SaveManager] Port arrival recorded: {portId} — triggering auto-save.");
            AutoSave();
        }

        // ══════════════════════════════════════════════════════════════════
        //  State Capture (Save → disk)
        // ══════════════════════════════════════════════════════════════════

        private SaveData CaptureState(int slot)
        {
            var data = new SaveData
            {
                saveVersion   = SAVE_VERSION,
                timestamp     = DateTime.UtcNow.ToString("o"),
                slotIndex     = slot,
                gold          = _economySystem  != null ? _economySystem.Gold : 0f,
                crewCount     = 0,
                currentPortId = _currentPortId,
                shipTierId    = CaptureShipTier(),
            };

            data.discoveredPorts = new List<string>(_discoveredPorts);
            data.cargo           = CaptureCargoState();
            data.quests          = CaptureQuestState();

            return data;
        }

        private string CaptureShipTier()
        {
            // TODO: read from HPSystem / ShipData when the tier upgrade system is built
            return "sloop";
        }

        private List<CargoSaveEntry> CaptureCargoState()
        {
            var entries = new List<CargoSaveEntry>();

            if (_cargoInventory == null)
                return entries;

            foreach (var item in _cargoInventory.Items)
            {
                if (item.goods == null || item.quantity <= 0)
                    continue;

                if (string.IsNullOrEmpty(item.goods.goodsId))
                {
                    Debug.LogWarning($"[SaveManager] Cargo item '{item.goods.displayName}' has no goodsId — skipping.");
                    continue;
                }

                entries.Add(new CargoSaveEntry(item.goods.goodsId, item.quantity));
            }

            return entries;
        }

        private List<QuestSaveEntry> CaptureQuestState()
        {
            var entries = new List<QuestSaveEntry>();

            if (QuestManager.Instance == null)
                return entries;

            // Capture all non-Locked quests (Available, Active, Completed, Failed)
            var allQuests = new List<QuestInstance>();
            allQuests.AddRange(QuestManager.Instance.GetAvailableQuests());
            allQuests.AddRange(QuestManager.Instance.GetActiveQuests());
            allQuests.AddRange(QuestManager.Instance.GetCompletedQuests());
            allQuests.AddRange(QuestManager.Instance.GetFailedQuests());

            foreach (var inst in allQuests)
            {
                var counts = new List<int>();
                foreach (var obj in inst.ObjectiveProgress)
                    counts.Add(obj.CurrentCount);

                entries.Add(new QuestSaveEntry(
                    inst.Data.questId,
                    (int)inst.Status,
                    inst.ElapsedSeconds,
                    counts));
            }

            return entries;
        }

        // ══════════════════════════════════════════════════════════════════
        //  State Restoration (disk → live systems)
        // ══════════════════════════════════════════════════════════════════

        private void RestoreState(SaveData data)
        {
            // Gold
            if (_economySystem != null)
                _economySystem.SetGold(data.gold);

            // Current port + discovery history
            _currentPortId = data.currentPortId ?? "";
            _discoveredPorts.Clear();
            if (data.discoveredPorts != null)
                foreach (var p in data.discoveredPorts)
                    _discoveredPorts.Add(p);

            // Cargo hold
            RestoreCargoState(data.cargo);

            // Quest log
            RestoreQuestState(data.quests);
        }

        private void RestoreCargoState(List<CargoSaveEntry> entries)
        {
            if (_cargoInventory == null || entries == null || entries.Count == 0)
                return;

            _cargoInventory.ClearAll();

            // Resolve ScriptableObject references by goodsId at load time
            var allGoods = Resources.FindObjectsOfTypeAll<GoodsData>();

            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.goodsId) || entry.quantity <= 0)
                    continue;

                GoodsData goods = FindGoodsById(allGoods, entry.goodsId);

                if (goods == null)
                {
                    Debug.LogWarning($"[SaveManager] Restore cargo: no GoodsData found for id='{entry.goodsId}' — skipping.");
                    continue;
                }

                bool added = _cargoInventory.AddGoods(goods, entry.quantity);
                if (!added)
                    Debug.LogWarning($"[SaveManager] Restore cargo: AddGoods({entry.goodsId} x{entry.quantity}) rejected (capacity?).");
            }
        }

        private static GoodsData FindGoodsById(GoodsData[] allGoods, string goodsId)
        {
            foreach (var g in allGoods)
            {
                if (g != null && g.goodsId == goodsId)
                    return g;
            }
            return null;
        }

        private void RestoreQuestState(List<QuestSaveEntry> entries)
        {
            if (QuestManager.Instance == null || entries == null)
                return;

            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.questId))
                    continue;

                var inst = QuestManager.Instance.GetQuest(entry.questId);
                if (inst == null)
                {
                    Debug.LogWarning($"[SaveManager] Restore quests: questId='{entry.questId}' not found in QuestManager.");
                    continue;
                }

                var savedStatus = (QuestStatus)entry.statusInt;

                switch (savedStatus)
                {
                    case QuestStatus.Active:
                        if (inst.Status == QuestStatus.Available)
                            QuestManager.Instance.StartQuest(entry.questId);
                        break;

                    case QuestStatus.Completed:
                        QuestManager.Instance.ForceComplete(entry.questId);
                        break;

                    case QuestStatus.Failed:
                        QuestManager.Instance.ForceFail(entry.questId);
                        break;

                    // QuestStatus.Available and QuestStatus.Locked: leave as-is
                    // (QuestManager sets initial status based on prerequisites)
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  AutoSave Coroutine (5-minute timer)
        // ══════════════════════════════════════════════════════════════════

        private IEnumerator AutoSaveCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(AUTO_SAVE_INTERVAL);
                Debug.Log("[SaveManager] Periodic auto-save triggered (5-min interval).");
                Save(AUTO_SAVE_SLOT);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Event Handlers
        // ══════════════════════════════════════════════════════════════════

        private void OnPortCapturedHandler(string portId, string newFaction)
        {
            if (newFaction == "player_pirates")
                NotifyPortArrival(portId);
        }

        private void OnApplicationQuit()
        {
            if (_initialized)
            {
                Debug.Log("[SaveManager] Auto-saving on application quit.");
                Save(AUTO_SAVE_SLOT);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Helpers
        // ══════════════════════════════════════════════════════════════════

        private static string GetSavePath(int slot) =>
            Path.Combine(Application.persistentDataPath,
                         string.Format(SAVE_FILE_FORMAT, slot));
    }
}
