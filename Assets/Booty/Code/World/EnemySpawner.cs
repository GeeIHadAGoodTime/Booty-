using System.Collections.Generic;
using UnityEngine;
using Booty.Ports;
using Booty.Core;
using Booty.Economy;
using Booty.Combat;
using Booty.UI;
using Booty.Balance;
using Booty.Faction;
using Booty.Ships;

namespace Booty.World
{
    /// <summary>
    /// Spawns enemy ships near enemy-owned ports. Simple timed spawns per SubPRD 4.1.
    /// Enemy count and difficulty scale with port defense_rating and player renown
    /// (via RenownSystem.GetDifficultyMultiplier).
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [SerializeField] private float spawnIntervalSeconds = 45f;
        [SerializeField] private int maxEnemiesPerPort = 3;
        [SerializeField] private int maxTotalEnemies = 10;
        [SerializeField] private float spawnRadiusFromPort = 30f;
        [SerializeField] private float minimumDistanceFromPlayer = 40f;

        [Header("Enemy Prefab")]
        [SerializeField] private GameObject enemyShipPrefab;

        // System references
        private PortSystem _portSystem;
        private RenownSystem _renownSystem;
        private EconomySystem _economySystem;
        private Transform _playerTransform;
        private GameBalance _balance;

        private float _spawnTimer;
        private readonly List<GameObject> _activeEnemies = new List<GameObject>();

        /// <summary>
        /// Initialize the enemy spawner with system references.
        /// Called by BootyBootstrap during scene setup.
        /// </summary>
        /// <param name="portSystem">Port system for ownership queries.</param>
        /// <param name="renownSystem">Renown system for difficulty scaling.</param>
        /// <param name="economySystem">Economy system for awarding combat spoils.</param>
        /// <param name="playerTransform">Player ship transform for distance checks.</param>
        /// <param name="balance">Active GameBalance for spawn and loot values (optional).</param>
        public void Initialize(PortSystem portSystem, RenownSystem renownSystem,
                               EconomySystem economySystem, Transform playerTransform,
                               GameBalance balance = null)
        {
            _portSystem = portSystem;
            _renownSystem = renownSystem;
            _economySystem = economySystem;
            _playerTransform = playerTransform;

            // Apply balance values if provided (overrides Inspector defaults)
            if (balance != null)
            {
                _balance = balance;
                spawnIntervalSeconds      = balance.spawnIntervalSeconds;
                maxEnemiesPerPort         = balance.maxEnemiesPerPort;
                maxTotalEnemies           = balance.maxTotalEnemies;
                spawnRadiusFromPort       = balance.spawnRadiusFromPort;
                minimumDistanceFromPlayer = balance.minimumDistanceFromPlayer;
            }

            _spawnTimer = spawnIntervalSeconds * 0.5f; // Spawn sooner on game start
            Debug.Log("[EnemySpawner] Initialized.");
        }

        private void Update()
        {
            CleanupDestroyedEnemies();

            _spawnTimer += Time.deltaTime;
            if (_spawnTimer >= spawnIntervalSeconds)
            {
                _spawnTimer = 0f;
                TrySpawnEnemies();
            }
        }

        /// <summary>
        /// Attempt to spawn enemy ships near hostile ports.
        /// Respects max enemy counts and minimum distance from player.
        /// </summary>
        private void TrySpawnEnemies()
        {
            if (_portSystem == null)
                return;

            if (_activeEnemies.Count >= maxTotalEnemies)
                return;

            var allPorts = _portSystem.GetAllPorts();
            foreach (var kvp in allPorts)
            {
                if (_activeEnemies.Count >= maxTotalEnemies)
                    break;

                var port = kvp.Value;

                // Only spawn near enemy ports
                if (!_portSystem.IsPortHostile(port.portId))
                    continue;

                // Count enemies already near this port
                int nearbyEnemies = CountEnemiesNearPort(port.worldPosition);
                if (nearbyEnemies >= maxEnemiesPerPort)
                    continue;

                // Check distance from player -- don't spawn on top of them
                if (_playerTransform != null)
                {
                    float distToPlayer = Vector3.Distance(port.worldPosition, _playerTransform.position);
                    if (distToPlayer < minimumDistanceFromPlayer)
                        continue;
                }

                SpawnEnemyNearPort(port);
            }
        }

        /// <summary>
        /// Spawn a single enemy ship at a random position near the given port.
        /// </summary>
        /// <param name="port">The port to spawn near.</param>
        private void SpawnEnemyNearPort(PortRuntimeData port)
        {
            // Random position around port
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadiusFromPort;
            Vector3 spawnPos = port.worldPosition + new Vector3(randomCircle.x, 0f, randomCircle.y);

            GameObject enemy;
            if (enemyShipPrefab != null)
            {
                enemy = Instantiate(enemyShipPrefab, spawnPos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            }
            else
            {
                // Placeholder enemy ship if no prefab assigned
                enemy = CreatePlaceholderEnemy(spawnPos);
            }

            enemy.name = $"EnemyShip_{port.portId}_{_activeEnemies.Count}";
            enemy.tag = "Enemy";

            // Apply difficulty scaling from renown
            if (_renownSystem != null)
            {
                float difficultyMult = _renownSystem.GetDifficultyMultiplier();
                // The combat system (VS-3) will read difficulty from a component.
                // For now, store it as a simple metadata approach.
                var meta = enemy.AddComponent<EnemyMetadata>();
                meta.sourceFaction = port.factionOwner;
                meta.sourcePortId = port.portId;
                meta.difficultyMultiplier = difficultyMult;
                // Port defense sets base tier (1-3 based on defense_rating 1-5)
                int portTier = Mathf.Clamp((int)port.defenseRating / 2 + 1, 1, 3);
                // Renown promotes minimum tier: unknown=1, notorious(50+)=2, feared(150+)=3
                int renownTier = 1;
                {
                    float renown = _renownSystem.Renown;
                    if (renown >= 150f)     renownTier = 3;
                    else if (renown >= 50f) renownTier = 2;
                }
                // Use the higher of port tier or renown tier
                meta.tier = Mathf.Max(portTier, renownTier);

                // Wire loot popup — use balance values when available
                var lootPopup = enemy.GetComponent<LootPopup>();
                if (lootPopup == null) lootPopup = enemy.AddComponent<LootPopup>();
                int lootBase    = _balance != null ? _balance.baseLootPopupGold    : CombatConfig.GoldRewardPerKill;
                int lootPerTier = _balance != null ? _balance.lootPopupGoldPerTier : 25;
                lootPopup.Configure(lootBase + (meta.tier - 1) * lootPerTier);

                // Wire kill rewards — configure HP with tier scaling and renown difficulty
                var enemyHP = enemy.GetComponent<HPSystem>();
                if (enemyHP == null) enemyHP = enemy.AddComponent<HPSystem>();
                int enemyBaseHP  = _balance != null ? _balance.enemyBaseHP    : CombatConfig.DefaultEnemyHP;
                int enemyHPTier  = _balance != null ? _balance.enemyHPPerTier : 20;
                int baseHP       = enemyBaseHP + (meta.tier - 1) * enemyHPTier;
                int scaledHP     = Mathf.RoundToInt(baseHP * difficultyMult);
                enemyHP.Configure(scaledHP);
                // Wire AI faction so reputation system governs whether this ship aggros
                var ai = enemy.GetComponent<EnemyAI>();
                if (ai != null) ai.FactionId = meta.sourceFaction;

                int    tier       = meta.tier;
                string factionId  = meta.sourceFaction; // capture for lambda
                enemyHP.OnDestroyed += () =>
                {
                    if (_economySystem != null) _economySystem.AwardCombatSpoils(tier);
                    if (_renownSystem  != null) _renownSystem.AwardKillRenown(tier);
                    // Attacking a faction's ships lowers reputation with that faction
                    ReputationManager.Instance?.ModifyReputation(factionId, -10f);
                };
            }

            _activeEnemies.Add(enemy);

            Debug.Log($"[EnemySpawner] Spawned enemy near '{port.portName}' at {spawnPos}");
        }

        /// <summary>
        /// Create a placeholder enemy ship when no prefab is assigned.
        /// Uses a simple capsule with enemy coloring.
        /// </summary>
        /// <param name="position">World position to place the enemy.</param>
        /// <returns>The created enemy GameObject.</returns>
        private GameObject CreatePlaceholderEnemy(Vector3 position)
        {
            var enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemy.transform.position = position;
            enemy.transform.localScale = new Vector3(1.5f, 0.5f, 3f);
            enemy.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            var renderer = enemy.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.6f, 0.1f, 0.1f); // Dark red
            }

            // Add rigidbody for physics interactions
            var rb = enemy.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezePositionY |
                             RigidbodyConstraints.FreezeRotationX |
                             RigidbodyConstraints.FreezeRotationZ;

            return enemy;
        }

        /// <summary>
        /// Count how many active enemy ships are near a given position.
        /// </summary>
        /// <param name="portPosition">The center position to check around.</param>
        /// <returns>Number of enemies within spawn radius of the position.</returns>
        private int CountEnemiesNearPort(Vector3 portPosition)
        {
            int count = 0;
            foreach (var enemy in _activeEnemies)
            {
                if (enemy == null) continue;
                if (Vector3.Distance(enemy.transform.position, portPosition) <= spawnRadiusFromPort * 1.5f)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Remove null references from the active enemies list (destroyed ships).
        /// </summary>
        private void CleanupDestroyedEnemies()
        {
            _activeEnemies.RemoveAll(e => e == null);
        }

        /// <summary>
        /// Get the current count of active enemy ships.
        /// </summary>
        /// <returns>Number of alive enemy ships in the world.</returns>
        public int GetActiveEnemyCount()
        {
            CleanupDestroyedEnemies();
            return _activeEnemies.Count;
        }

        /// <summary>
        /// Destroy all active enemy ships. Debug use.
        /// </summary>
        public void ClearAllEnemies()
        {
            foreach (var enemy in _activeEnemies)
            {
                if (enemy != null)
                    Destroy(enemy);
            }
            _activeEnemies.Clear();
            Debug.Log("[EnemySpawner] All enemies cleared.");
        }
    }

    /// <summary>
    /// Simple metadata component attached to spawned enemy ships.
    /// Stores faction, source port, tier, and difficulty multiplier
    /// for use by the combat system (VS-3).
    /// </summary>
    public class EnemyMetadata : MonoBehaviour
    {
        /// <summary>The faction this enemy belongs to.</summary>
        public string sourceFaction;

        /// <summary>The port this enemy was spawned from.</summary>
        public string sourcePortId;

        /// <summary>Difficulty multiplier based on player renown.</summary>
        public float difficultyMultiplier = 1f;

        /// <summary>Enemy ship tier (1-3). Affects combat reward and stats.</summary>
        public int tier = 1;
    }
}
