// ---------------------------------------------------------------------------
// EnemySpawner.cs (Booty.AI) — Region-danger-level open-water enemy spawning
// ---------------------------------------------------------------------------
// Complements World.EnemySpawner (which spawns near hostile ports) with
// open-water patrols driven by region danger level rather than port ownership.
//
// Define RegionZone entries in the Inspector (or leave empty to use the
// five built-in Caribbean danger zones).  Each zone has:
//   - center / radius  — the circular patrol area
//   - dangerLevel (1–5) — scales spawn frequency and enemy HP
//
// Danger level affects:
//   - Per-zone enemy cap: baseEnemiesPerZone × dangerLevel
//   - Spawn probability per tick: dangerLevel / 5 (higher = spawns more often)
//   - Enemy HP: DefaultEnemyHP + (dangerLevel − 1) × 20
//   - EnemyMetadata.tier: 1 (danger 1–2), 2 (danger 3), 3 (danger 4–5)
//
// Wire via Initialize(playerTransform). Called by BootyBootstrap.
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;
using Booty.Ships;
using Booty.Combat;

namespace Booty.AI
{
    // ══════════════════════════════════════════════════════════════════════
    //  Data: RegionZone
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Defines a circular danger zone in the open sea.
    /// Assign one or more zones to <see cref="EnemySpawner.regions"/> in the
    /// Inspector, or leave empty to auto-populate Caribbean defaults.
    /// </summary>
    [System.Serializable]
    public class RegionZone
    {
        [Tooltip("Human-readable label for this zone (debug / Inspector).")]
        public string zoneName = "Open Waters";

        [Tooltip("World-space center of the zone.")]
        public Vector3 center;

        [Tooltip("Circular boundary radius (world units).")]
        [Min(10f)] public float radius = 80f;

        [Tooltip("Danger level 1 (calm) to 5 (deadly). Scales spawn count and enemy strength.")]
        [Range(1, 5)] public int dangerLevel = 1;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  EnemySpawner
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Spawns enemy ships in open-water danger zones based on region danger level.
    /// Creates <see cref="EnemyShipAI"/> instances — no port dependency.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────

        [Header("Danger Zones")]
        [Tooltip("Circular sea regions with individual danger ratings. " +
                 "Leave empty to use built-in Caribbean defaults.")]
        [SerializeField] public List<RegionZone> regions = new List<RegionZone>();

        [Header("Spawn Tuning")]
        [Tooltip("Base timer interval (seconds) between spawn attempts (for danger 5 zones).")]
        [SerializeField] private float spawnInterval = 45f;

        [Tooltip("Hard cap on total enemies this spawner controls across all zones.")]
        [SerializeField] private int maxTotalEnemies = 18;

        [Tooltip("Base per-zone enemy cap. Multiplied by dangerLevel at runtime.")]
        [SerializeField] private int baseEnemiesPerZone = 2;

        [Tooltip("Minimum distance from the player before an enemy may spawn.")]
        [SerializeField] private float minSpawnDistFromPlayer = 40f;

        [Header("Enemy Prefab")]
        [Tooltip("Prefab to instantiate as an enemy ship. " +
                 "If null a primitive placeholder is created.")]
        [SerializeField] private GameObject enemyShipPrefab;

        // ── Runtime ────────────────────────────────────────────────────

        private Transform             _playerTransform;
        private float                 _spawnTimer;
        private readonly List<GameObject> _activeEnemies = new List<GameObject>();

        // ══════════════════════════════════════════════════════════════════
        //  Public API
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Wire player reference and kick off the spawner.
        /// Called by BootyBootstrap during scene setup.
        /// </summary>
        public void Initialize(Transform playerTransform)
        {
            _playerTransform = playerTransform;

            // Trigger first spawn sooner (40 % of interval)
            _spawnTimer = spawnInterval * 0.4f;

            // Auto-populate zones when none are assigned in the Inspector
            if (regions.Count == 0)
                BuildDefaultCaribbean();

            Debug.Log($"[AI.EnemySpawner] Initialized with {regions.Count} zone(s).");
        }

        /// <summary>Current count of active enemy ships owned by this spawner.</summary>
        public int GetActiveEnemyCount()
        {
            CleanDestroyed();
            return _activeEnemies.Count;
        }

        /// <summary>Destroy all active enemies. Debug / scene-reset use.</summary>
        public void ClearAll()
        {
            foreach (var e in _activeEnemies)
                if (e != null) Destroy(e);
            _activeEnemies.Clear();
        }

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Update()
        {
            CleanDestroyed();

            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer <= 0f)
            {
                _spawnTimer = spawnInterval;
                TrySpawn();
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Spawning
        // ══════════════════════════════════════════════════════════════════

        private void TrySpawn()
        {
            if (_activeEnemies.Count >= maxTotalEnemies) return;

            foreach (var zone in regions)
            {
                if (_activeEnemies.Count >= maxTotalEnemies) break;

                int cap = baseEnemiesPerZone * zone.dangerLevel;
                if (CountInZone(zone) >= cap) continue;

                // Probabilistic gate: higher danger spawns more aggressively
                // dangerLevel 1 → 20 % chance, dangerLevel 5 → 100 % chance
                if (Random.value > zone.dangerLevel / 5f) continue;

                SpawnInZone(zone);
            }
        }

        private void SpawnInZone(RegionZone zone)
        {
            // Random position inside the zone circle (XZ plane)
            Vector2 rnd      = Random.insideUnitCircle * zone.radius;
            Vector3 spawnPos = zone.center + new Vector3(rnd.x, 0f, rnd.y);

            // Don't spawn directly on the player
            if (_playerTransform != null &&
                Vector3.Distance(spawnPos, _playerTransform.position) < minSpawnDistFromPlayer)
                return;

            // Instantiate ship
            Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            GameObject go  = enemyShipPrefab != null
                ? Instantiate(enemyShipPrefab, spawnPos, rot)
                : BuildFallbackShip(spawnPos, rot);

            go.name = $"RegionEnemy_{zone.zoneName}_{_activeEnemies.Count}";
            go.tag  = "Enemy";

            // Ensure required components
            var sc  = go.GetComponent<ShipController>()  ?? go.AddComponent<ShipController>();
            var hp  = go.GetComponent<HPSystem>()         ?? go.AddComponent<HPSystem>();
            var brd = go.GetComponent<BroadsideSystem>()  ?? go.AddComponent<BroadsideSystem>();
            var ai  = go.GetComponent<EnemyShipAI>()      ?? go.AddComponent<EnemyShipAI>();

            // Scale HP with danger level: +20 HP per level above 1
            int scaledHP = CombatConfig.DefaultEnemyHP + (zone.dangerLevel - 1) * 20;
            hp.Configure(scaledHP);

            brd.Initialize(sc);
            ai.Initialize(_playerTransform, sc, brd, hp);

            // Enemy metadata (used by EconomySystem, QuestManager, etc.)
            var meta = go.GetComponent<Booty.World.EnemyMetadata>()
                       ?? go.AddComponent<Booty.World.EnemyMetadata>();
            meta.tier                 = DangerLevelToTier(zone.dangerLevel);
            meta.difficultyMultiplier = 1f + (zone.dangerLevel - 1) * 0.2f;
            meta.sourceFaction        = "open_water_pirates";
            meta.sourcePortId         = zone.zoneName;

            _activeEnemies.Add(go);
            Debug.Log($"[AI.EnemySpawner] Spawned in '{zone.zoneName}' " +
                      $"(danger={zone.dangerLevel}, tier={meta.tier}, HP={scaledHP})");
        }

        // ══════════════════════════════════════════════════════════════════
        //  Helpers
        // ══════════════════════════════════════════════════════════════════

        private int CountInZone(RegionZone zone)
        {
            int count = 0;
            foreach (var e in _activeEnemies)
            {
                if (e == null) continue;
                if (Vector3.Distance(e.transform.position, zone.center) <= zone.radius)
                    count++;
            }
            return count;
        }

        private void CleanDestroyed()
            => _activeEnemies.RemoveAll(e => e == null);

        /// <summary>
        /// Maps danger level (1–5) to EnemyMetadata.tier (1–3).
        /// Tier 1 = sloop, Tier 2 = brig, Tier 3 = galleon-class.
        /// </summary>
        private static int DangerLevelToTier(int dangerLevel)
        {
            if (dangerLevel >= 4) return 3;
            if (dangerLevel >= 2) return 2;
            return 1;
        }

        private static GameObject BuildFallbackShip(Vector3 pos, Quaternion rot)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.position   = pos;
            go.transform.rotation   = rot;
            go.transform.localScale = new Vector3(1.2f, 0.5f, 2.5f);

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
                rend.material.color = new Color(0.65f, 0.12f, 0.12f); // dark red

            var rb = go.AddComponent<Rigidbody>();
            rb.useGravity  = false;
            rb.constraints = RigidbodyConstraints.FreezePositionY
                           | RigidbodyConstraints.FreezeRotationX
                           | RigidbodyConstraints.FreezeRotationZ;

            return go;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Default Caribbean Zones
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Populates five built-in Caribbean open-water danger zones.
        /// Automatically called when no zones are assigned in the Inspector.
        /// </summary>
        private void BuildDefaultCaribbean()
        {
            regions.Add(new RegionZone
            {
                zoneName   = "Safe Passage",
                center     = new Vector3(   0, 0,   0),
                radius     = 80f,
                dangerLevel = 1,
            });
            regions.Add(new RegionZone
            {
                zoneName   = "Nassau Approaches",
                center     = new Vector3(  80, 0,  50),
                radius     = 65f,
                dangerLevel = 2,
            });
            regions.Add(new RegionZone
            {
                zoneName   = "Pirate Straits",
                center     = new Vector3( -60, 0,  90),
                radius     = 75f,
                dangerLevel = 3,
            });
            regions.Add(new RegionZone
            {
                zoneName   = "Spanish Main",
                center     = new Vector3( 130, 0, -50),
                radius     = 90f,
                dangerLevel = 4,
            });
            regions.Add(new RegionZone
            {
                zoneName   = "Tortuga Death Reef",
                center     = new Vector3(-110, 0, -85),
                radius     = 55f,
                dangerLevel = 5,
            });

            Debug.Log("[AI.EnemySpawner] Loaded 5 default Caribbean danger zones.");
        }
    }
}
