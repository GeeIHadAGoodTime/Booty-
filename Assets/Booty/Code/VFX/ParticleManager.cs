// ---------------------------------------------------------------------------
// ParticleManager.cs — Centralised particle effects: wake trails, debris, rain
// ---------------------------------------------------------------------------
// Complements CombatVFX (combat-only bursts) with persistent per-ship effects
// and destruction debris. Uses only Unity's built-in ParticleSystem API — no
// imported assets required.
//
// Usage:
//   ParticleManager.Instance.RegisterShip(shipGO, controller);
//   ParticleManager.Instance.PlayDebrisExplosion(pos);
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;
using Booty.Ships;

namespace Booty.VFX
{
    /// <summary>
    /// Singleton that manages persistent per-ship particle effects (wake trails)
    /// and one-shot destruction debris.
    /// </summary>
    public class ParticleManager : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Singleton
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Global access point.</summary>
        public static ParticleManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Wake Trail Registry
        // ══════════════════════════════════════════════════════════════════

        private class WakeEntry
        {
            public ParticleSystem Particles;
            public ShipController Controller; // may be null for non-player ships
        }

        private readonly Dictionary<GameObject, WakeEntry> _wakeTrails =
            new Dictionary<GameObject, WakeEntry>();

        // ══════════════════════════════════════════════════════════════════
        //  Public API — Registration
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Attach a wake trail to <paramref name="ship"/>. Safe to call
        /// multiple times for the same ship (idempotent).
        /// </summary>
        /// <param name="ship">Ship root GameObject.</param>
        /// <param name="controller">Optional ShipController for speed sampling.</param>
        public void RegisterShip(GameObject ship, ShipController controller = null)
        {
            if (ship == null || _wakeTrails.ContainsKey(ship)) return;

            var ps = CreateWakeTrail(ship.transform);

            _wakeTrails[ship] = new WakeEntry
            {
                Particles  = ps,
                Controller = controller ?? ship.GetComponent<ShipController>(),
            };
        }

        // ══════════════════════════════════════════════════════════════════
        //  Public API — Effects
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Burst of wood planks, splinters, and smoke at <paramref name="position"/>.
        /// Call when a ship is destroyed.
        /// </summary>
        public void PlayDebrisExplosion(Vector3 position)
        {
            var root = new GameObject("VFX_ShipDebris");
            root.transform.position = position;

            // ── Planks (large, tumbling wood pieces) ────────────────────
            SpawnDebrisLayer(root.transform, "Planks",
                count:     20,
                lifetime:  new Vector2(1.5f, 2.5f),
                speed:     new Vector2(4f,  12f),
                size:      new Vector2(0.4f, 1.2f),
                start:     new Color(0.55f, 0.35f, 0.15f, 1.0f),
                end:       new Color(0.35f, 0.22f, 0.10f, 0.0f),
                gravity:   1.5f,
                radius:    1.2f);

            // ── Splinters (fast, tiny shards) ───────────────────────────
            SpawnDebrisLayer(root.transform, "Splinters",
                count:     40,
                lifetime:  new Vector2(0.4f, 1.0f),
                speed:     new Vector2(8f,  22f),
                size:      new Vector2(0.08f, 0.25f),
                start:     new Color(0.68f, 0.45f, 0.22f, 1.0f),
                end:       new Color(0.50f, 0.32f, 0.12f, 0.0f),
                gravity:   2.2f,
                radius:    0.6f);

            // ── Rope ends / sailcloth (slow, drifting pieces) ───────────
            SpawnDebrisLayer(root.transform, "Sailcloth",
                count:     12,
                lifetime:  new Vector2(2.0f, 3.5f),
                speed:     new Vector2(1f,   5f),
                size:      new Vector2(0.5f, 1.8f),
                start:     new Color(0.90f, 0.88f, 0.80f, 0.9f),
                end:       new Color(0.85f, 0.82f, 0.75f, 0.0f),
                gravity:   0.5f,
                radius:    1.5f);

            // ── Explosion smoke ──────────────────────────────────────────
            SpawnDebrisLayer(root.transform, "Smoke",
                count:     18,
                lifetime:  new Vector2(1.8f, 3.0f),
                speed:     new Vector2(0.5f, 2.5f),
                size:      new Vector2(1.2f, 3.0f),
                start:     new Color(0.28f, 0.28f, 0.28f, 0.85f),
                end:       new Color(0.12f, 0.12f, 0.12f, 0.0f),
                gravity:   -0.15f,
                radius:    1.0f);

            Destroy(root, 4.0f);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Update — Drive Wake Intensity
        // ══════════════════════════════════════════════════════════════════

        private void Update()
        {
            var deadShips = new List<GameObject>();

            foreach (var kvp in _wakeTrails)
            {
                if (kvp.Key == null)
                {
                    deadShips.Add(kvp.Key);
                    continue;
                }

                var entry = kvp.Value;
                if (entry.Particles == null)
                {
                    deadShips.Add(kvp.Key);
                    continue;
                }

                // Scale emission 0-30 particles/sec based on normalised speed
                float speed = entry.Controller != null ? entry.Controller.SpeedNormalized : 0.5f;
                var emiss        = entry.Particles.emission;
                emiss.rateOverTime = Mathf.Lerp(0f, 30f, speed);

                // Scale particle start size for more dramatic wake at high speed
                var mainMod      = entry.Particles.main;
                float sizeMin    = Mathf.Lerp(0.1f, 0.4f, speed);
                float sizeMax    = Mathf.Lerp(0.3f, 0.8f, speed);
                mainMod.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            }

            foreach (var ship in deadShips)
                _wakeTrails.Remove(ship);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Internal — Wake Trail Factory
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Build a foam/bubble wake trail attached to the stern of <paramref name="parent"/>.
        /// </summary>
        private static ParticleSystem CreateWakeTrail(Transform parent)
        {
            var go = new GameObject("VFX_WakeTrail");
            go.transform.SetParent(parent, worldPositionStays: false);

            // Place at stern (−Z in ship-local space), just at water level
            go.transform.localPosition = new Vector3(0f, 0.15f, -1.5f);

            // Emit downward → gravity brings particles flat against the water
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var ps   = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop             = true;
            main.playOnAwake      = true;
            main.duration         = 1f;
            main.startLifetime    = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startSpeed       = new ParticleSystem.MinMaxCurve(0.8f, 2.0f);
            main.startSize        = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.startColor       = new Color(0.88f, 0.94f, 1.0f, 0.65f);
            main.gravityModifier  = 0.08f;
            main.maxParticles     = 120;
            main.simulationSpace  = ParticleSystemSimulationSpace.World;

            // Fade out over lifetime
            var col  = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.9f, 0.95f, 1.0f), 0.0f),
                    new GradientColorKey(new Color(1.0f, 1.0f,  1.0f), 0.5f),
                    new GradientColorKey(new Color(1.0f, 1.0f,  1.0f), 1.0f),
                },
                new[]
                {
                    new GradientAlphaKey(0.65f, 0.0f),
                    new GradientAlphaKey(0.40f, 0.5f),
                    new GradientAlphaKey(0.00f, 1.0f),
                }
            );
            col.color = grad;

            // Slight size swell then fade
            var sizeOL  = ps.sizeOverLifetime;
            sizeOL.enabled = true;
            var sizeAC  = new AnimationCurve(
                new Keyframe(0f, 0.4f),
                new Keyframe(0.4f, 1.0f),
                new Keyframe(1f, 0.7f)
            );
            sizeOL.size = new ParticleSystem.MinMaxCurve(1f, sizeAC);

            // Emission rate set to 0 — driven by Update() based on speed
            var emiss = ps.emission;
            emiss.rateOverTime = 0f;

            // Box emitter spanning stern width
            var shape = ps.shape;
            shape.enabled   = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale     = new Vector3(0.8f, 0.1f, 0.2f);

            ps.Play();
            return ps;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Internal — Debris Layer Factory
        // ══════════════════════════════════════════════════════════════════

        private static void SpawnDebrisLayer(
            Transform parent, string layerName,
            int count, Vector2 lifetime, Vector2 speed, Vector2 size,
            Color start, Color end, float gravity, float radius)
        {
            var go = new GameObject($"VFX_Debris_{layerName}");
            go.transform.SetParent(parent, worldPositionStays: false);

            var ps   = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop          = false;
            main.playOnAwake   = false;
            main.duration      = 0.15f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed    = new ParticleSystem.MinMaxCurve(speed.x,    speed.y);
            main.startSize     = new ParticleSystem.MinMaxCurve(size.x,     size.y);
            main.startColor    = start;
            main.gravityModifier = gravity;
            main.maxParticles  = count + 10;

            var col  = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey((Color)start, 0f), new GradientColorKey((Color)end, 1f) },
                new[] { new GradientAlphaKey(start.a, 0f),      new GradientAlphaKey(0f, 1f) }
            );
            col.color = grad;

            var emiss = ps.emission;
            emiss.enabled = false;
            emiss.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });
            emiss.enabled = true;

            var shape = ps.shape;
            shape.enabled   = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius    = radius;

            ps.Play();
        }

        // ══════════════════════════════════════════════════════════════════
        //  Cleanup
        // ══════════════════════════════════════════════════════════════════

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
