// ---------------------------------------------------------------------------
// Projectile.cs — Cannonball travel, hit detection, and self-cleanup
// ---------------------------------------------------------------------------
// Spawned by BroadsideSystem. Travels in a straight line on the XZ plane.
// On collision with a ship's collider, applies damage via HPSystem and
// destroys itself. Auto-destroys after CombatConfig.ProjectileLifetime.
// ---------------------------------------------------------------------------

using UnityEngine;
using Booty.VFX;

namespace Booty.Combat
{
    /// <summary>
    /// A single cannonball projectile. Moves forward, detects hits,
    /// applies damage, and self-destructs.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  State (set by spawner)
        // ══════════════════════════════════════════════════════════════════

        private Vector3 _direction;
        private float   _speed;
        private int     _damage;
        private float   _lifetime;
        private float   _elapsed;
        private GameObject _owner; // the ship that fired this projectile

        // ══════════════════════════════════════════════════════════════════
        //  Public API
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Configure the projectile right after instantiation.
        /// </summary>
        /// <param name="direction">Normalised XZ travel direction.</param>
        /// <param name="speed">Travel speed in world units/sec.</param>
        /// <param name="damage">Damage dealt on hit.</param>
        /// <param name="lifetime">Max seconds before auto-destroy.</param>
        /// <param name="owner">The ship that fired this (immune to self-hits).</param>
        public void Initialize(Vector3 direction, float speed, int damage, float lifetime, GameObject owner)
        {
            _direction = direction.normalized;
            _direction.y = 0f; // enforce XZ plane
            _speed    = speed;
            _damage   = damage;
            _lifetime = lifetime;
            _owner    = owner;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Update()
        {
            // ── Move ────────────────────────────────────────────────────
            float dt = Time.deltaTime;
            transform.position += _direction * (_speed * dt);

            // Keep on nav plane
            Vector3 pos = transform.position;
            float arcY = Mathf.Sin(_elapsed / _lifetime * Mathf.PI) * 1.0f + 0.5f;
            pos.y = arcY;
            transform.position = pos;

            // ── Lifetime ────────────────────────────────────────────────
            _elapsed += dt;
            if (_elapsed >= _lifetime)
            {
                // Cannonball hit water (no ship target) — splash where it lands
                CombatVFX.Instance?.PlayWaterSplash(transform.position);
                Destroy(gameObject);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Collision
        // ══════════════════════════════════════════════════════════════════

        private void OnTriggerEnter(Collider other)
        {
            // Ignore the ship that fired us
            if (other.gameObject == _owner) return;
            if (_owner != null && other.transform.IsChildOf(_owner.transform)) return;

            // Check for HPSystem on the hit object
            var hp = other.GetComponentInParent<HPSystem>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(_damage);
            }

            // Destroy self on any collision (terrain, ship, etc.)
            Destroy(gameObject);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Factory
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates a projectile with a trigger sphere collider and rigidbody.
        /// No prefab required — this builds the GO from scratch.
        /// </summary>
        public static Projectile Spawn(Vector3 position, Vector3 direction, int damage, GameObject owner)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Cannonball";
            go.transform.position = position;
            go.transform.localScale = Vector3.one * (CombatConfig.ProjectileRadius * 2f);

            // Tint dark grey
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.2f, 0.2f, 0.2f);
            }

            // Replace the default collider with a trigger
            var defaultCol = go.GetComponent<Collider>();
            if (defaultCol != null) Object.Destroy(defaultCol);

            var sphere = go.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 0.5f; // normalised (scale handles world size)

            // Kinematic rigidbody required for trigger events
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // Attach and configure projectile script
            var proj = go.AddComponent<Projectile>();
            proj.Initialize(
                direction,
                CombatConfig.ProjectileSpeed,
                damage,
                CombatConfig.ProjectileLifetime,
                owner
            );

            // Cannon smoke puff at the firing position
            CombatVFX.Instance?.PlayCannonSmoke(position);

            // Add TrailRenderer for visual trail
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.3f;                          // trail lasts 0.3 seconds
            trail.startWidth = 0.2f;                   // start of trail (at ball position)
            trail.endWidth = 0f;                       // taper to nothing
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.startColor = new Color(1f, 0.7f, 0.2f, 1f);  // orange/fire color
            trail.endColor   = new Color(0.5f, 0.3f, 0.1f, 0f); // fades to transparent brown
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;

            return proj;
        }
    }
}
