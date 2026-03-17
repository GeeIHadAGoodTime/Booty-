using System;
using System.IO;
using UnityEngine;

namespace Booty.Save
{
    /// <summary>
    /// Single JSON-based save pipeline as required by Implementation Topology (section 4.3).
    /// Save location: Application.persistentDataPath + "/booty_save.json".
    /// Only SaveSystem may perform disk I/O for game saves.
    /// </summary>
    public class SaveSystem : MonoBehaviour
    {
        private const string SAVE_FILENAME = "booty_save.json";

        private string SavePath => Path.Combine(Application.persistentDataPath, SAVE_FILENAME);

        /// <summary>
        /// The current in-memory game state. Systems read from and write to this
        /// during gameplay; SaveSystem handles serialization to/from disk.
        /// </summary>
        public GameState CurrentState { get; private set; }

        /// <summary>
        /// Initialize the save system. Called by GameRoot during bootstrap.
        /// Loads existing save or creates a fresh game state.
        /// </summary>
        public void Initialize()
        {
            CurrentState = LoadOrNew();
            Debug.Log($"[SaveSystem] Initialized. Save path: {SavePath}");
        }

        /// <summary>
        /// Load an existing save file from disk, or create a new default GameState
        /// if no save exists or deserialization fails.
        /// </summary>
        /// <returns>The loaded or newly created GameState.</returns>
        public GameState LoadOrNew()
        {
            if (File.Exists(SavePath))
            {
                try
                {
                    string json = File.ReadAllText(SavePath);
                    GameState loaded = JsonUtility.FromJson<GameState>(json);
                    if (loaded != null)
                    {
                        Debug.Log("[SaveSystem] Save loaded successfully.");
                        return loaded;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SaveSystem] Failed to load save: {e.Message}. Creating new state.");
                }
            }

            Debug.Log("[SaveSystem] No save found. Creating new game state.");
            return CreateNewState();
        }

        /// <summary>
        /// Serialize the given game state to disk as JSON.
        /// </summary>
        /// <param name="state">The GameState to persist.</param>
        public void Save(GameState state)
        {
            if (state == null)
            {
                Debug.LogError("[SaveSystem] Cannot save null state.");
                return;
            }

            state.timestamp = DateTime.UtcNow.ToString("o");

            try
            {
                string json = JsonUtility.ToJson(state, prettyPrint: true);
                File.WriteAllText(SavePath, json);
                Debug.Log("[SaveSystem] Game saved successfully.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Save failed: {e.Message}");
            }
        }

        /// <summary>
        /// Save the current in-memory state to disk.
        /// </summary>
        public void SaveCurrent()
        {
            Save(CurrentState);
        }

        /// <summary>
        /// Check whether a save file exists on disk.
        /// </summary>
        /// <returns>True if a save file exists at the expected path.</returns>
        public bool HasSave()
        {
            return File.Exists(SavePath);
        }

        /// <summary>
        /// Delete the save file from disk if it exists.
        /// </summary>
        public void DeleteSave()
        {
            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
                Debug.Log("[SaveSystem] Save deleted.");
            }
        }

        /// <summary>
        /// Create a fresh game state with default values for a new game.
        /// Port ownership defaults are applied later by RegionSetup using config data.
        /// </summary>
        private GameState CreateNewState()
        {
            var state = new GameState
            {
                saveVersion = "1.0",
                timestamp = DateTime.UtcNow.ToString("o"),
                player = new PlayerSaveData
                {
                    name = "Captain",
                    gold = 200f,
                    renown = 0f,
                    rank = "nobody",
                    positionX = -40f,
                    positionZ = 30f,
                    rotationY = 0f
                },
                playerShip = new ShipSaveData
                {
                    shipClassId = "sloop",
                    currentHull = 80,
                    maxHull = 80
                },
                economy = new EconomySaveData
                {
                    incomeTimer = 0f
                }
            };

            return state;
        }

        /// <summary>
        /// Auto-save hook. Call this after key beats (port capture, exit, etc.).
        /// </summary>
        public void AutoSave()
        {
            Debug.Log("[SaveSystem] Auto-saving...");
            SaveCurrent();
        }

        private void OnApplicationQuit()
        {
            if (CurrentState != null)
            {
                Debug.Log("[SaveSystem] Saving on application quit.");
                SaveCurrent();
            }
        }
    }
}
