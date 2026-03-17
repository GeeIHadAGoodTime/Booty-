// ---------------------------------------------------------------------------
// BootyBootstrap.cs — Composition root: creates GameRoot & wires all systems
// ---------------------------------------------------------------------------
// Per ImplementationTopology.md section 3 and SubPRD section 4.7:
//   - This is the ONLY entry point for runtime wiring.
//   - No gameplay logic lives here — only object creation, initializer calls,
//     and camera/UI configuration.
//   - Attach this MonoBehaviour to a single GameObject in World_Main.unity.
// ---------------------------------------------------------------------------
//
// SCENE SETUP (World_Main.unity):
//   1. Create an empty GameObject named "Bootstrap".
//   2. Attach this script to it.
//   3. Assign the [SerializeField] references in the Inspector:
//        - playerShipPrefab: a prefab with ShipController + HPSystem
//        - enemyShipPrefab:  a prefab with EnemyAI + ShipController + HPSystem
//        - portsConfigJson / shipsConfigJson / factionsConfigJson
//   4. No other bootstrap or manager objects should exist in the scene.
// ---------------------------------------------------------------------------

using UnityEngine;
using Booty.Ships;
using Booty.World;
using Booty.Combat;
using Booty.Save;
using Booty.Config;
using Booty.Ports;
using Booty.Economy;
using Booty.Core;
using Booty.UI;
using Booty.Infra.Debug;
using Booty.VFX;
using Booty.Audio;
using Booty.Balance;
using Booty.Quests;

namespace Booty.Bootstrap
{
    /// <summary>
    /// Composition root. Creates GameRoot, spawns the player ship, wires the
    /// camera, and initialises all VS-1/2/3 systems.
    /// </summary>
    public class BootyBootstrap : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Inspector Wiring
        // ══════════════════════════════════════════════════════════════════

        [Header("Prefabs")]
        [SerializeField] private GameObject playerShipPrefab;
        [SerializeField] private GameObject enemyShipPrefab;

        [Header("Spawn Settings")]
        [SerializeField] private Vector3 playerSpawnPosition = Vector3.zero;
        [SerializeField] private int initialEnemyCount = 2;
        [SerializeField] private float enemySpawnRadius = 60f;

        [Header("Config JSON TextAssets")]
        [SerializeField] private TextAsset portsConfigJson;
        [SerializeField] private TextAsset shipsConfigJson;
        [SerializeField] private TextAsset factionsConfigJson;

        [Header("Balance & Difficulty")]
        [Tooltip("Assign the GameBalance ScriptableObject asset here. " +
                 "If null, DifficultyManager uses built-in defaults.")]
        [SerializeField] private GameBalance gameBalanceAsset;

        [Tooltip("Starting difficulty preset applied at game launch.")]
        [SerializeField] private Difficulty startingDifficulty = Difficulty.Normal;

        [Header("Quest System")]
        [Tooltip("Optional: assign QuestData ScriptableObject assets here. " +
                 "If empty, QuestManager auto-loads the 5 starter quests.")]
        [SerializeField] private System.Collections.Generic.List<QuestData> starterQuestAssets = new();

        [Header("Trade System")]
        [Tooltip("Assign all PortEconomy ScriptableObject assets here " +
                 "(one per port: Nassau, Havana, Tortuga, Kingston).")]
        [SerializeField] private System.Collections.Generic.List<PortEconomy> portEconomyAssets = new();

        [Tooltip("Maximum cargo weight the player's hold can carry.")]
        [SerializeField] private int playerCargoCapacity = 100;

        // ══════════════════════════════════════════════════════════════════
        //  Private State
        // ══════════════════════════════════════════════════════════════════

        private EconomySystem   _economySystem;
        private RenownSystem    _renownSystem;
        private QuestManager    _questManager;
        private TradeManager    _tradeManager;
        private CargoInventory  _cargoInventory;
        private CombatVFX       _combatVFX;
        private ParticleManager _particleManager;   // S3.2: wake trails + debris
        private GameOverUI      _gameOverUI;
        private PortScreenUI    _portScreenUI;
        private CapturePopup    _capturePopup;
        private AudioManager    _audioManager;

        // ══════════════════════════════════════════════════════════════════
        //  Boot Sequence
        // ══════════════════════════════════════════════════════════════════

        private void Awake()
        {
            // ── 0. Difficulty Manager (must be first — all other systems read balance) ──
            var diffGO            = new GameObject("DifficultyManager");
            var difficultyManager = diffGO.AddComponent<DifficultyManager>();
            difficultyManager.Initialize(gameBalanceAsset, startingDifficulty);
            var balance = difficultyManager.ActiveBalance;

            // ── 1. GameRoot ──────────────────────────────────────────────────
            var rootGO   = new GameObject("GameRoot");
            var gameRoot = rootGO.AddComponent<GameRoot>();

            // ── 2. Save System ───────────────────────────────────────────────
            var saveGO     = new GameObject("SaveSystem");
            var saveSystem = saveGO.AddComponent<SaveSystem>();
            saveSystem.Initialize();

            // ── 3. Config Service ────────────────────────────────────────────
            var configGO      = new GameObject("ConfigService");
            var configService = configGO.AddComponent<ConfigService>();
            configService.Configure(portsConfigJson, shipsConfigJson, factionsConfigJson);

            // ── 4. Port System ───────────────────────────────────────────────
            var portSystemGO = new GameObject("PortSystem");
            var portSystem   = portSystemGO.AddComponent<PortSystem>();
            // PortSystem.Initialize() is called inside RegionSetup.BuildRegion()

            // ── 5. Economy System ────────────────────────────────────────────
            var econGO       = new GameObject("EconomySystem");
            _economySystem   = econGO.AddComponent<EconomySystem>();
            _economySystem.ConfigureBalance(balance);            // S3.6: apply difficulty
            _economySystem.Initialize(portSystem, saveSystem);

            // ── 5b. Cargo Inventory ──────────────────────────────────────────
            // Tracks goods the player is carrying; used by TradeManager.
            var cargoGO        = new GameObject("CargoInventory");
            _cargoInventory    = cargoGO.AddComponent<CargoInventory>();
            _cargoInventory.Initialize(playerCargoCapacity);

            // ── 5c. Trade Manager ────────────────────────────────────────────
            // Handles buy/sell at ports; drives supply/demand drift per port.
            // Assign PortEconomy assets to portEconomyAssets in the Inspector.
            var tradeGO     = new GameObject("TradeManager");
            _tradeManager   = tradeGO.AddComponent<TradeManager>();
            _tradeManager.portEconomies.AddRange(portEconomyAssets);
            _tradeManager.Initialize(_economySystem, _cargoInventory);

            // ── 6. Renown System ─────────────────────────────────────────────
            var renownGO   = new GameObject("RenownSystem");
            _renownSystem  = renownGO.AddComponent<RenownSystem>();
            _renownSystem.Initialize(saveSystem, portSystem);

            // ── 6b. Quest Manager (S3.3) ─────────────────────────────────────
            // QuestManager must exist before portSystem events are wired so it can
            // receive ReportPortCaptured and ReportKill callbacks in step 12+.
            var questGO    = new GameObject("QuestManager");
            _questManager  = questGO.AddComponent<QuestManager>();
            _questManager.Initialize(starterQuestAssets, _economySystem, _renownSystem);

            // ── 7. Repair Shop ───────────────────────────────────────────────
            var repairGO   = new GameObject("RepairShop");
            var repairShop = repairGO.AddComponent<RepairShop>();
            repairShop.ConfigureBalance(balance);                // S3.6: apply difficulty
            repairShop.Initialize(_economySystem, saveSystem);

            // ── 8. Region Setup (also inits PortSystem + spawns port GOs) ────
            var regionGO    = new GameObject("RegionSetup");
            var regionSetup = regionGO.AddComponent<RegionSetup>();
            regionSetup.SetPortsConfig(portsConfigJson);
            regionSetup.Initialize(portSystem, _economySystem, repairShop, saveSystem);
            regionSetup.BuildRegion();

            // ── 8b. Port Battle Tracker ──────────────────────────────────────
            var trackerGO         = new GameObject("PortBattleTracker");
            var portBattleTracker = trackerGO.AddComponent<PortBattleTracker>();
            portBattleTracker.Initialize(portSystem);

            // ── 9. HUD Manager ───────────────────────────────────────────────
            // HUDManager self-wires via FindObjectOfType in Start(); just AddComponent.
            var hudGO      = new GameObject("HUDManager");
            hudGO.AddComponent<HUDManager>();

            // ── 10. Port Interaction UI ──────────────────────────────────────
            // PortInteractionUI self-wires; no Initialize() signature.
            var piuiGO = new GameObject("PortInteractionUI");
            piuiGO.AddComponent<PortInteractionUI>();

            // ── 10b. Port Screen UI (full-screen tabs) ───────────────────────
            var portScreenGO = new GameObject("PortScreenUI");
            _portScreenUI = portScreenGO.AddComponent<PortScreenUI>();

            // ── 10e. Capture Popup (full-screen on port capture) ─────────────
            var captureGO = new GameObject("CapturePopup");
            _capturePopup = captureGO.AddComponent<CapturePopup>();

            // ── 10c. Pause Menu UI ────────────────────────────────────────────
            var pauseGO = new GameObject("PauseMenuUI");
            pauseGO.AddComponent<PauseMenuUI>();

            // ── 10d. Game Over UI ─────────────────────────────────────────────
            var gameOverGO = new GameObject("GameOverUI");
            _gameOverUI = gameOverGO.AddComponent<GameOverUI>();

            // ── 11. Debug Console ────────────────────────────────────────────
            var debugGO = new GameObject("DebugConsole");
            debugGO.AddComponent<DebugConsole>();

            // ── 11b. Combat VFX ──────────────────────────────────────────────
            _combatVFX = gameObject.AddComponent<CombatVFX>();

            // ── 11b-S32. Particle Manager + Weather System (S3.2) ───────────
            _particleManager = gameObject.AddComponent<ParticleManager>();
            new GameObject("WeatherSystem").AddComponent<WeatherSystem>();

            // ── 11c. Audio System (S3.4) ──────────────────────────────────────
            // AudioManager provides SFX + music channels. CombatAudio subscribes to
            // BroadsideSystem and HPSystem events in its Start(). AmbientAudio manages
            // ocean/wind ambient loops and sailing↔combat music transitions.
            var audioGO = new GameObject("AudioManager");
            _audioManager = audioGO.AddComponent<AudioManager>();

            var combatAudioGO = new GameObject("CombatAudio");
            combatAudioGO.AddComponent<CombatAudio>();

            var ambientAudioGO = new GameObject("AmbientAudio");
            ambientAudioGO.AddComponent<AmbientAudio>();

            // S3.4: Wire income collection → gold coin SFX
            _economySystem.OnIncomeCollected += (amount, portCount) =>
                _audioManager?.PlayGoldCoin();

            // ── 12. Player Ship ──────────────────────────────────────────────
            GameObject playerGO = playerShipPrefab != null
                ? Instantiate(playerShipPrefab, playerSpawnPosition, Quaternion.identity)
                : CreateFallbackPlayerShip();

            playerGO.name = "PlayerShip";
            playerGO.tag  = "Player"; // S2.4: required for PortInteraction.OnTriggerEnter

            var shipController = playerGO.GetComponent<ShipController>()
                                 ?? playerGO.AddComponent<ShipController>();
            var hpSystem       = playerGO.GetComponent<HPSystem>()
                                 ?? playerGO.AddComponent<HPSystem>();
            var broadside      = playerGO.GetComponent<BroadsideSystem>()
                                 ?? playerGO.AddComponent<BroadsideSystem>();

            // S3.6: Apply balance to player ship movement and HP
            shipController.Configure(balance.playerMaxSpeed, balance.playerTurnRate, balance.playerAcceleration);
            hpSystem.Configure(balance.playerMaxHP);

            // S3.2: Visual layer — procedural ship mesh
            var playerVisual = playerGO.AddComponent<ShipVisual>();
            playerVisual.Initialize();
            playerVisual.Configure("player_pirates", ShipTier.Sloop);

            // S3.2: Register player ship for combat VFX (fire, explosion)
            _combatVFX.RegisterShip(playerGO);
            // S3.2: Register player for wake trail + debris
            _particleManager?.RegisterShip(playerGO, shipController);

            // S3.1: Ship damage states — hull damage reduces speed, sail damage reduces turn
            var playerDamageState = playerGO.AddComponent<ShipDamageState>();
            playerDamageState.Initialize(hpSystem, shipController);

            // S3.1: Boarding system — player presses B to board nearby enemies
            var boardingSystem = playerGO.AddComponent<BoardingSystem>();
            boardingSystem.Initialize(hpSystem);

            // S3.1: Player HP bar (green, hidden until first hit)
            var playerHPBar = playerGO.AddComponent<ShipHPBar>();
            playerHPBar.Initialize(hpSystem, isPlayer: true);

            // S3.3: Wire player death → GameOverUI (scene-reload respawn replaces broken teleport)
            hpSystem.OnDestroyed += () =>
            {
                _gameOverUI?.Show();
                Debug.Log("[BootyBootstrap] Player destroyed — showing GameOverUI.");
            };

            // S3.3: Wire port capture → CapturePopup celebration + PortScreenUI tabs
            // S3.4: Play victory chime on capture
            portSystem.OnPortCaptured += (portId, newFaction) =>
            {
                if (newFaction == "player_pirates")
                {
                    _capturePopup?.ShowCapture(portId, "enemy fleet", 200f);
                    _portScreenUI?.ShowPortScreen(portId, justCaptured: true);
                    _audioManager?.PlayChime();
                    _questManager?.ReportPortCaptured(portId);  // S3.3: quest progress
                    _questManager?.ReportArrival(portId);        // S3.3: arrival objectives
                    Debug.Log("[BootyBootstrap] Port captured by player — showing CapturePopup+PortScreenUI: " + portId);
                }
            };

            // ── 13. Isometric Camera ─────────────────────────────────────────
            var cameraGO = new GameObject("IsometricCamera");
            var cam      = cameraGO.AddComponent<Camera>();
            cam.orthographic     = true;
            cam.orthographicSize = 20f;
            cam.clearFlags       = CameraClearFlags.SolidColor;
            cam.backgroundColor  = new Color(0.15f, 0.3f, 0.5f);

            var isoCam = cameraGO.AddComponent<IsometricCamera>();
            isoCam.Initialize(playerGO.transform);

            var mainCam = Camera.main;
            if (mainCam != null && mainCam != cam)
                mainCam.gameObject.SetActive(false);

            // ── 14. Wire GameRoot & Broadside ────────────────────────────────
            broadside.Initialize(shipController);
            gameRoot.Initialize(shipController, isoCam, broadside, hpSystem);

            // ── 15. Enemy Spawner ────────────────────────────────────────────
            var spawnerGO    = new GameObject("EnemySpawner");
            var enemySpawner = spawnerGO.AddComponent<EnemySpawner>();
            enemySpawner.Initialize(portSystem, _renownSystem, _economySystem, playerGO.transform, balance); // S3.6

            // ── 16. Initial Enemy Spawn ──────────────────────────────────────
            SpawnEnemies(playerGO.transform);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Enemy Spawning
        // ══════════════════════════════════════════════════════════════════

        private void SpawnEnemies(Transform playerTransform)
        {
            // Resolve active balance (DifficultyManager may already be created)
            var activeBalance = DifficultyManager.Instance != null
                ? DifficultyManager.Instance.ActiveBalance
                : null;

            for (int i = 0; i < initialEnemyCount; i++)
            {
                Vector3 offset = Random.insideUnitSphere * enemySpawnRadius;
                offset.y = 0f; // keep on nav plane
                Vector3 spawnPos = playerTransform.position + offset;

                GameObject enemyGO = enemyShipPrefab != null
                    ? Instantiate(enemyShipPrefab, spawnPos, Quaternion.identity)
                    : CreateFallbackEnemyShip(spawnPos);

                enemyGO.name = $"EnemyShip_{i}";

                // Ensure required components
                var ai = enemyGO.GetComponent<EnemyAI>();
                if (ai == null) ai = enemyGO.AddComponent<EnemyAI>();

                var sc = enemyGO.GetComponent<ShipController>();
                if (sc == null) sc = enemyGO.AddComponent<ShipController>();

                var hp = enemyGO.GetComponent<HPSystem>();
                if (hp == null) hp = enemyGO.AddComponent<HPSystem>();

                var bs = enemyGO.GetComponent<BroadsideSystem>();
                if (bs == null) bs = enemyGO.AddComponent<BroadsideSystem>();

                // S3.2: Visual layer — procedural ship mesh
                var enemyVisual = enemyGO.AddComponent<ShipVisual>();
                enemyVisual.Initialize();
                enemyVisual.Configure("default", ShipTier.Sloop);

                // S3.2: Register enemy ship for combat VFX (fire, explosion)
                _combatVFX?.RegisterShip(enemyGO);
                // S3.2: Register enemy for wake trail + debris
                _particleManager?.RegisterShip(enemyGO, sc);

                // S3.6: Configure enemy movement and HP from active balance
                if (activeBalance != null)
                {
                    sc.Configure(activeBalance.enemyMaxSpeed, activeBalance.enemyTurnRate, activeBalance.enemyAcceleration);
                    hp.Configure(activeBalance.enemyBaseHP);
                }

                // Wire AI → other components
                ai.Initialize(playerTransform, sc, bs, hp);
                bs.Initialize(sc);

                // S2.2: Wire enemy death → combat rewards
                var meta = enemyGO.GetComponent<EnemyMetadata>();
                if (meta == null) meta = enemyGO.AddComponent<EnemyMetadata>();
                if (meta.tier <= 0) meta.tier = 1;
                int    enemyTier    = meta.tier;
                string enemyFaction = meta.sourceFaction ?? "";
                hp.OnDestroyed += () =>
                {
                    if (_economySystem != null) _economySystem.AwardCombatSpoils(enemyTier);
                    if (_renownSystem  != null) _renownSystem.AwardKillRenown(enemyTier);
                    if (_questManager  != null) _questManager.ReportKill(enemyFaction); // S3.3: quest progress
                };
                var lootPopup = enemyGO.AddComponent<Booty.UI.LootPopup>();
                // S3.6: Use balance values for loot popup if available
                int lootBase    = activeBalance != null ? activeBalance.baseLootPopupGold    : 50;
                int lootPerTier = activeBalance != null ? activeBalance.lootPopupGoldPerTier : 25;
                lootPopup.Configure(lootBase + (enemyTier - 1) * lootPerTier);

                // S3.1: Ship damage states — hull damage reduces speed, sail reduces turn
                var enemyDamageState = enemyGO.AddComponent<ShipDamageState>();
                enemyDamageState.Initialize(hp, sc);

                // S3.1: Physical loot drop — collectible crates scattered at death position
                var lootDrop = enemyGO.AddComponent<LootDrop>();
                lootDrop.Initialize(hp, enemyTier);

                // S3.1: Enemy HP bar (red, shown when hit)
                var enemyHPBar = enemyGO.AddComponent<ShipHPBar>();
                enemyHPBar.Initialize(hp, isPlayer: false);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Fallback Prefab Builders (no-art dev bootstrap)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates a minimal player ship when no prefab is assigned.
        /// Uses a primitive capsule so the ship is visible during dev.
        /// </summary>
        private GameObject CreateFallbackPlayerShip()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.position = playerSpawnPosition;
            go.transform.localScale = new Vector3(1.5f, 0.5f, 3f);

            // Tint player ship green
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.2f, 0.7f, 0.3f);
            }

            go.AddComponent<ShipController>();
            go.AddComponent<HPSystem>();
            go.AddComponent<BroadsideSystem>();

            // Add a Rigidbody for physics interactions — no gravity on nav plane
            var rb = go.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezePositionY
                           | RigidbodyConstraints.FreezeRotationX
                           | RigidbodyConstraints.FreezeRotationZ;

            return go;
        }

        /// <summary>
        /// Creates a minimal enemy ship when no prefab is assigned.
        /// </summary>
        private GameObject CreateFallbackEnemyShip(Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.position = position;
            go.transform.localScale = new Vector3(1.2f, 0.5f, 2.5f);

            // Tint enemy ship red
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.8f, 0.2f, 0.2f);
            }

            go.AddComponent<ShipController>();
            go.AddComponent<HPSystem>();
            go.AddComponent<BroadsideSystem>();
            go.AddComponent<EnemyAI>();

            var rb = go.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezePositionY
                           | RigidbodyConstraints.FreezeRotationX
                           | RigidbodyConstraints.FreezeRotationZ;

            return go;
        }
    }
}
