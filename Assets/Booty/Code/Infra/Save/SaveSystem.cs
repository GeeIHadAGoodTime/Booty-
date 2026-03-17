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

        /// <summary>
        /// Capture full runtime state from live systems into CurrentState.
        /// Call before SaveCurrent() to ensure all live data is persisted.
        /// </summary>
        /// <param name="playerHp">Player HPSystem — persists currentHull + maxHull.</param>
        /// <param name="ship">Player ShipController — persists world position + rotation.</param>
        /// <param name="portSystem">PortSystem — persists list of player-owned port IDs.</param>
        /// <param name="difficultyLevel">Current difficulty level (0 = normal).</param>
        /// <param name="enemySpawnSeed">Seed used by EnemySpawner for reproducible waves.</param>
        public void CaptureFromSystems(
            Booty.Combat.HPSystem playerHp,
            Booty.Ships.ShipController ship,
            Booty.Ports.PortSystem portSystem,
            int difficultyLevel = 0,
            int enemySpawnSeed = 0)
        {
            if (CurrentState == null)
                CurrentState = CreateNewState();

            if (ship != null)
            {
                CurrentState.player.positionX = ship.transform.position.x;
                CurrentState.player.positionZ = ship.transform.position.z;
                CurrentState.player.rotationY = ship.transform.eulerAngles.y;
            }

            if (playerHp != null)
            {
                CurrentState.playerShip.currentHull = playerHp.CurrentHP;
                CurrentState.playerShip.maxHull     = playerHp.MaxHP;
            }

            if (portSystem != null)
            {
                CurrentState.capturedPortIds = new System.Collections.Generic.List<string>();
                foreach (var kvp in portSystem.GetAllPorts())
                {
                    if (kvp.Value.factionOwner == "player_pirates")
                        CurrentState.capturedPortIds.Add(kvp.Key);
                }
            }

            CurrentState.difficultyLevel = difficultyLevel;
            CurrentState.enemySpawnSeed  = enemySpawnSeed;

            Debug.Log("[SaveSystem] CaptureFromSystems complete.");
        }

        /// <summary>
        /// Restore CurrentState into live systems (position, HP, port ownership).
        /// Call after LoadOrNew() to apply loaded state to the scene.
        /// </summary>
        /// <param name="playerHp">Player HPSystem — restores currentHull + maxHull.</param>
        /// <param name="ship">Player ShipController — restores world position + rotation.</param>
        /// <param name="portSystem">PortSystem — restores player-owned port ownership.</param>
        public void RestoreToSystems(
            Booty.Combat.HPSystem playerHp,
            Booty.Ships.ShipController ship,
            Booty.Ports.PortSystem portSystem)
        {
            if (CurrentState == null)
            {
                Debug.LogWarning("[SaveSystem] RestoreToSystems: CurrentState is null.");
                return;
            }

            if (ship != null)
            {
                ship.transform.position = new Vector3(
                    CurrentState.player.positionX,
                    ship.transform.position.y,
                    CurrentState.player.positionZ);
                ship.transform.rotation = Quaternion.Euler(
                    0f, CurrentState.player.rotationY, 0f);
            }

            if (playerHp != null)
            {
                int targetMax = CurrentState.playerShip.maxHull;
                int targetCur = CurrentState.playerShip.currentHull;
                if (targetMax > 0)
                {
                    playerHp.Configure(targetMax);   // resets CurrentHP to maxHull
                    int deficit = targetMax - targetCur;
                    if (deficit > 0)
                        playerHp.TakeDamage(deficit); // reduce to saved currentHull
                }
            }

            if (portSystem != null && CurrentState.capturedPortIds != null)
            {
                foreach (string portId in CurrentState.capturedPortIds)
                    portSystem.SetPortOwner(portId, "player_pirates");
            }

            Debug.Log("[SaveSystem] RestoreToSystems complete.");
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
