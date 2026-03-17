// ---------------------------------------------------------------------------
// HPSystem.cs — Hull hit-point tracking, damage, and destruction
// ---------------------------------------------------------------------------
// Attached to every ship (player and enemy). Fires events on damage and
// death so other systems can react without tight coupling.
// ---------------------------------------------------------------------------

using System;
using UnityEngine;
using Booty.UI;

namespace Booty.Combat
{
    /// <summary>
    /// Manages hull HP for a single ship. Provides damage intake,
    /// death detection, and observable events.
    /// </summary>
    public class HPSystem : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Inspector
        // ══════════════════════════════════════════════════════════════════

        [Header("HP")]
        [SerializeField] private int maxHP = CombatConfig.DefaultPlayerHP;

        // ══════════════════════════════════════════════════════════════════
        //  Events
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Raised when damage is taken. Args: (currentHP, maxHP).</summary>
        public event Action<int, int> OnDamaged;

        /// <summary>Raised once when HP reaches zero.</summary>
        public event Action OnDestroyed;

        // ══════════════════════════════════════════════════════════════════
        //  Runtime State
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Current hull HP.</summary>
        public int CurrentHP { get; private set; }

        /// <summary>Maximum hull HP (for UI bars, etc.).</summary>
        public int MaxHP => maxHP;

        /// <summary>True after HP reaches zero.</summary>
        public bool IsDead { get; private set; }

        /// <summary>HP as a 0-1 fraction.</summary>
        public float HPNormalized => maxHP > 0 ? (float)CurrentHP / maxHP : 0f;

        // ══════════════════════════════════════════════════════════════════
        //  Public API
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Set max HP (e.g., from ShipData). Resets current HP to max.
        /// </summary>
        public void Configure(int maxHitPoints)
        {
            maxHP = maxHitPoints;
            CurrentHP = maxHP;
            IsDead = false;
        }

        /// <summary>
        /// Apply damage to this ship's hull.
        /// </summary>
        /// <param name="amount">Positive damage value.</param>
        public void TakeDamage(int amount)
        {
            if (IsDead) return;
            if (amount <= 0) return;

            CurrentHP = Mathf.Max(0, CurrentHP - amount);
            OnDamaged?.Invoke(CurrentHP, maxHP);

            // Spawn floating damage number
            bool isPlayerShip = CompareTag("Player");
            FloatingDamageNumber.Spawn(transform.position, amount, isPlayerShip);

            if (CurrentHP <= 0)
            {
                Die();
            }
        }

        /// <summary>
        /// Restore HP (repairs, etc.). Clamped to maxHP.
        /// </summary>
        public void Heal(int amount)
        {
            if (IsDead) return;
            CurrentHP = Mathf.Min(maxHP, CurrentHP + amount);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Awake()
        {
            CurrentHP = maxHP;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Internal
        // ══════════════════════════════════════════════════════════════════

        private void Die()
        {
            IsDead = true;
            OnDestroyed?.Invoke();

            // Simple sink: disable collider and start sinking coroutine
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            // Disable input / AI
            var ship = GetComponent<Ships.ShipController>();
            if (ship != null) ship.enabled = false;

            var ai = GetComponent<Ships.EnemyAI>();
            if (ai != null) ai.enabled = false;

            StartCoroutine(DeathEffect());
        }

        private System.Collections.IEnumerator DeathEffect()
        {
            float duration = 0.8f;
            float elapsed  = 0f;
            Vector3 startScale = transform.localScale;

            // Spawn a particle burst (via procedural sphere burst)
            SpawnDeathParticles();

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                transform.position += Vector3.down * 2f * Time.deltaTime;
                transform.rotation = Quaternion.Euler(t * 45f, transform.eulerAngles.y, t * 20f);
                yield return null;
            }

            Destroy(gameObject);
        }

        private void SpawnDeathParticles()
        {
            // Procedural particle burst — 8 small spheres fly outward
            for (int i = 0; i < 8; i++)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                p.transform.position   = transform.position + Vector3.up * 0.5f;
                p.transform.localScale = Vector3.one * 0.3f;

                var rend = p.GetComponent<Renderer>();
                if (rend != null)
                    rend.material.color = new Color(0.8f, 0.4f, 0.1f); // orange debris

                // Remove collider so debris doesn't interfere
                var col = p.GetComponent<Collider>();
                if (col != null) Destroy(col);

                // Random outward velocity via a simple mover
                float angle = i * 45f * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0.5f, Mathf.Sin(angle)).normalized;
                var mover = p.AddComponent<DebrisParticle>();
                mover.velocity = dir * 8f;

                Destroy(p, 1.0f);
            }
        }
    }

    /// <summary>
    /// Simple velocity-based mover for death debris particles.
    /// </summary>
    internal class DebrisParticle : MonoBehaviour
    {
        internal Vector3 velocity;
        private void Update()
        {
            velocity.y -= 9.8f * Time.deltaTime; // gravity
            transform.position += velocity * Time.deltaTime;
        }
    }
}
