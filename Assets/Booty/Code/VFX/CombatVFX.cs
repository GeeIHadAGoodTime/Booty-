// ---------------------------------------------------------------------------
// CombatVFX.cs — Runtime combat particle effects (no imported assets)
// ---------------------------------------------------------------------------
// Singleton MonoBehaviour that manages all combat VFX via Unity's built-in
// ParticleSystem API. No external assets required — all particle systems are
// configured entirely in code.
//
// Usage:
//   CombatVFX.Instance.PlayCannonSmoke(pos);
//   CombatVFX.Instance.PlayWaterSplash(pos);
//   CombatVFX.Instance.RegisterShip(shipGO);   // call once per ship
// ---------------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Booty.Combat;

namespace Booty.VFX
{
    /// <summary>
    /// Manages all combat particle effects as a singleton.
    /// Attach to a persistent GameObject in the bootstrap scene.
    /// </summary>
    public class CombatVFX : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Singleton
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Global access point for combat VFX.</summary>
        public static CombatVFX Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Ship Fire tracking
        // ══════════════════════════════════════════════════════════════════

        // Maps ship GameObject → its fire particle system child
        private readonly Dictionary<GameObject, ParticleSystem> _activeFires =
            new Dictionary<GameObject, ParticleSystem>();

        // ══════════════════════════════════════════════════════════════════
        //  Public API — Effects
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Play a brief smoke puff at <paramref name="position"/> (cannon fire).
        /// </summary>
        public void PlayCannonSmoke(Vector3 position)
        {
            var go = new GameObject("VFX_CannonSmoke");
            go.transform.position = position;

            var ps  = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop             = false;
            main.playOnAwake      = false;
            main.duration         = 0.5f;
            main.startLifetime    = new ParticleSystem.MinMaxCurve(0.6f, 0.8f);
            main.startSpeed       = new ParticleSystem.MinMaxCurve(1.0f, 2.5f);
            main.startSize        = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.gravityModifier  = -0.1f; // slight upward drift
            main.maxParticles     = 20;

            // White → transparent grey gradient over lifetime
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.5f, 0.5f, 0.5f), 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f),        new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;

            // Size grows over lifetime (smoke expands)
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            var sizeCurve = new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(1f, 1.0f));
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Single burst: 8-12 particles
            var emission = ps.emission;
            emission.enabled = false;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8, 12) });
            emission.enabled = true;

            // Sphere emitter shape
            var shape = ps.shape;
            shape.enabled      = true;
            shape.shapeType    = ParticleSystemShapeType.Sphere;
            shape.radius       = 0.2f;

            ps.Play();
            Destroy(go, 1.5f);
        }

        /// <summary>
        /// Play a water spray where a cannonball hits the sea surface.
        /// </summary>
        public void PlayWaterSplash(Vector3 position)
        {
            var go = new GameObject("VFX_WaterSplash");
            go.transform.position = position;

            var ps   = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop          = false;
            main.playOnAwake   = false;
            main.duration      = 0.4f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.6f);
            main.startSpeed    = new ParticleSystem.MinMaxCurve(3.0f, 6.0f);
            main.startSize     = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startColor    = new Color(0.7f, 0.85f, 1.0f, 0.9f);
            main.gravityModifier = 0.8f;
            main.maxParticles  = 25;

            // Fade out over lifetime
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(0.7f, 0.85f, 1.0f), 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;

            // Burst of 15-20 droplets
            var emission = ps.emission;
            emission.enabled = false;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 15, 20) });
            emission.enabled = true;

            // Cone shape — spray outward/upward
            var shape = ps.shape;
            shape.enabled   = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle     = 60f;
            shape.radius    = 0.1f;
            // Aim cone upward
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            ps.Play();
            Destroy(go, 1.5f);
        }

        /// <summary>
        /// Attach a looping fire effect to <paramref name="ship"/>.
        /// Replaces any existing fire on the same ship.
        /// </summary>
        public void StartFire(GameObject ship)
        {
            if (ship == null) return;

            // Don't double-spawn
            if (_activeFires.ContainsKey(ship) && _activeFires[ship] != null)
                return;

            var fireGO = new GameObject("VFX_ShipFire");
            fireGO.transform.SetParent(ship.transform, worldPositionStays: false);
            fireGO.transform.localPosition = new Vector3(0f, 1.0f, 0f);

            var ps   = fireGO.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop          = true;
            main.playOnAwake   = false;
            main.duration      = 1.0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);
            main.startSpeed    = new ParticleSystem.MinMaxCurve(1.5f, 3.0f);
            main.startSize     = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.gravityModifier = -0.3f;
            main.maxParticles  = 60;

            // Fire → smoke color gradient over particle lifetime
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1.0f, 0.5f, 0.0f), 0.0f),
                    new GradientColorKey(new Color(0.8f, 0.2f, 0.0f), 0.5f),
                    new GradientColorKey(new Color(0.3f, 0.3f, 0.3f), 1.0f)
                },
                new[]
                {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(0.9f, 0.5f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            colorOverLifetime.color = gradient;

            // Continuous emission — 8 particles/sec
            var emission = ps.emission;
            emission.enabled  = true;
            emission.rateOverTime = 8f;

            // Small cone shape (fire rises from a point)
            var shape = ps.shape;
            shape.enabled   = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle     = 15f;
            shape.radius    = 0.2f;

            ps.Play();
            _activeFires[ship] = ps;
        }

        /// <summary>
        /// Stop and destroy the fire effect attached to <paramref name="ship"/>.
        /// </summary>
        public void StopFire(GameObject ship)
        {
            if (ship == null) return;

            if (_activeFires.TryGetValue(ship, out var ps))
            {
                _activeFires.Remove(ship);
                if (ps != null)
                {
                    ps.Stop(withChildren: true, stopBehavior: ParticleSystemStopBehavior.StopEmitting);
                    Destroy(ps.gameObject, 1.5f); // let existing particles finish
                }
            }
        }

        /// <summary>
        /// Play a multi-phase explosion at <paramref name="position"/> (ship destroyed).
        /// </summary>
        public void PlayExplosion(Vector3 position)
        {
            var root = new GameObject("VFX_Explosion");
            root.transform.position = position;

            // Phase 1 — initial white flash
            CreateExplosionPhase(root.transform, "Flash",
                startColor:   Color.white,
                endColor:     new Color(1f, 1f, 1f, 0f),
                count:        15,
                startDelay:   0.0f,
                lifetime:     0.1f,
                speed:        new Vector2(6f, 12f),
                size:         new Vector2(0.5f, 1.5f));

            // Phase 2 — orange fireball
            CreateExplosionPhase(root.transform, "Fireball",
                startColor:   new Color(1.0f, 0.5f, 0.0f),
                endColor:     new Color(0.8f, 0.2f, 0.0f, 0f),
                count:        25,
                startDelay:   0.05f,
                lifetime:     0.5f,
                speed:        new Vector2(4f, 8f),
                size:         new Vector2(0.8f, 2.0f));

            // Phase 3 — dark smoke
            CreateExplosionPhase(root.transform, "Smoke",
                startColor:   new Color(0.25f, 0.25f, 0.25f),
                endColor:     new Color(0.15f, 0.15f, 0.15f, 0f),
                count:        20,
                startDelay:   0.1f,
                lifetime:     0.9f,
                speed:        new Vector2(1.5f, 4f),
                size:         new Vector2(1.0f, 2.5f));

            // Brief light flash
            StartCoroutine(FlashLight(position));

            Destroy(root, 3.0f);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Ship Registration
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Subscribe to a ship's HP events so fire and explosions trigger automatically.
        /// Call once per ship after creation.
        /// </summary>
        public void RegisterShip(GameObject ship)
        {
            if (ship == null) return;

            var hp = ship.GetComponent<HPSystem>();
            if (hp == null) return;

            hp.OnDamaged += (currentHP, maxHP) =>
            {
                if (ship == null) return; // ship may have been destroyed
                float ratio = maxHP > 0 ? (float)currentHP / maxHP : 0f;
                if (ratio < 0.35f)
                    StartFire(ship);
            };

            hp.OnDestroyed += () =>
            {
                Vector3 pos = ship != null ? ship.transform.position : Vector3.zero;
                PlayExplosion(pos);
                StopFire(ship);
                // HPSystem.SinkAndDestroy() already calls Destroy(gameObject) after 2s.
                // We call it again only if IsDead guard hasn't triggered destruction yet,
                // using a longer delay to ensure our explosion plays first.
                if (ship != null && !IsBeingDestroyed(ship))
                {
                    Destroy(ship, 2.5f);
                }
            };
        }

        // ══════════════════════════════════════════════════════════════════
        //  Internal Helpers
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Create a single phase particle system as a child of <paramref name="parent"/>.
        /// </summary>
        private static void CreateExplosionPhase(
            Transform parent,
            string phaseName,
            Color startColor,
            Color endColor,
            int count,
            float startDelay,
            float lifetime,
            Vector2 speed,
            Vector2 size)
        {
            var go = new GameObject($"Phase_{phaseName}");
            go.transform.SetParent(parent, worldPositionStays: false);

            var ps   = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop             = false;
            main.playOnAwake      = false;
            main.duration         = 0.2f;
            main.startDelay       = startDelay;
            main.startLifetime    = new ParticleSystem.MinMaxCurve(lifetime * 0.8f, lifetime);
            main.startSpeed       = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize        = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor       = startColor;
            main.gravityModifier  = -0.05f;
            main.maxParticles     = count + 10;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(startColor, 0f), new GradientColorKey(endColor, 1f) },
                new[] { new GradientAlphaKey(startColor.a, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;

            var emission = ps.emission;
            emission.enabled = false;
            emission.SetBursts(new[] { new ParticleSystem.Burst(startDelay, count) });
            emission.enabled = true;

            var shape = ps.shape;
            shape.enabled   = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius    = 0.5f;

            ps.Play();
        }

        /// <summary>
        /// Creates a point light at <paramref name="position"/>, flashes at intensity 5,
        /// then fades to 0 over 0.3 s before destroying itself.
        /// </summary>
        private IEnumerator FlashLight(Vector3 position)
        {
            var lightGO = new GameObject("VFX_ExplosionLight");
            lightGO.transform.position = position;
            var light = lightGO.AddComponent<Light>();
            light.type      = LightType.Point;
            light.color     = new Color(1.0f, 0.5f, 0.1f); // orange
            light.range     = 25f;
            light.intensity = 5f;

            float elapsed  = 0f;
            float duration = 0.3f;

            while (elapsed < duration)
            {
                elapsed       += Time.deltaTime;
                light.intensity = Mathf.Lerp(5f, 0f, elapsed / duration);
                yield return null;
            }

            Destroy(lightGO);
        }

        /// <summary>
        /// Heuristic: returns true if the ship is already scheduled for destruction
        /// by HPSystem (which calls Destroy after a 2-second sink coroutine).
        /// We check IsDead — if true, HPSystem already issued the Destroy call.
        /// </summary>
        private static bool IsBeingDestroyed(GameObject ship)
        {
            if (ship == null) return true;
            var hp = ship.GetComponent<HPSystem>();
            return hp != null && hp.IsDead;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
