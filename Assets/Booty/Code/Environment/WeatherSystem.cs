// ---------------------------------------------------------------------------
// WeatherSystem.cs — Day/night cycle + random weather events
// ---------------------------------------------------------------------------
// Attaches to any persistent GameObject in the scene.
// Automatically finds the scene's directional light and drives its colour,
// rotation, and intensity across a full day/night cycle.
// Spawns a particle-based rain system and cycles through Clear→Foggy→Rainy
// →Stormy weather states on random intervals.
//
// SETUP: BootyBootstrap adds a "WeatherSystem" GO with this component.
// No further Inspector wiring is required.
// ---------------------------------------------------------------------------

using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Booty.World
{
    /// <summary>Weather states used by <see cref="WeatherSystem"/>.</summary>
    public enum WeatherState
    {
        Clear   = 0,
        Foggy   = 1,
        Rainy   = 2,
        Stormy  = 3,
    }

    /// <summary>
    /// Drives a day/night cycle on the directional light and randomly cycles
    /// through weather states, adjusting fog density and rain particles.
    /// </summary>
    public class WeatherSystem : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Inspector Tuning
        // ══════════════════════════════════════════════════════════════════

        [Header("Day / Night Cycle")]
        [Tooltip("Real-time seconds per full in-game day (0 = disabled).")]
        [SerializeField] private float dayDuration = 120f;

        [Header("Weather Events")]
        [SerializeField] private float minWeatherInterval = 30f;
        [SerializeField] private float maxWeatherInterval = 90f;
        [SerializeField] private bool  enableWeatherEvents = true;

        [Header("Fog (Clear State)")]
        [SerializeField] private float clearFogDensity   = 0.008f;
        [SerializeField] private float foggyFogDensity   = 0.025f;
        [SerializeField] private float rainyFogDensity   = 0.020f;
        [SerializeField] private float stormyFogDensity  = 0.040f;

        [Header("Debug")]
        [Tooltip("Start time in [0,1]: 0=midnight, 0.25=dawn, 0.5=noon, 0.75=dusk.")]
        [SerializeField] [Range(0f, 1f)] private float startTimeOfDay = 0.3f;

        // ══════════════════════════════════════════════════════════════════
        //  Runtime State
        // ══════════════════════════════════════════════════════════════════

        private Light          _sunLight;
        private float          _timeOfDay;          // [0, 1)
        private WeatherState   _currentWeather = WeatherState.Clear;
        private ParticleSystem _rainPS;

        // ══════════════════════════════════════════════════════════════════
        //  Day/Night Colour Keyframes
        // ══════════════════════════════════════════════════════════════════
        // 5 keys spaced evenly: midnight, dawn, noon, dusk, midnight
        //                          0        0.25  0.5   0.75  1.0

        private static readonly Color[] SunColors =
        {
            new Color(0.04f, 0.04f, 0.12f), // midnight — deep blue
            new Color(0.95f, 0.50f, 0.18f), // dawn      — warm orange
            new Color(1.00f, 0.97f, 0.82f), // noon      — white/warm
            new Color(1.00f, 0.60f, 0.22f), // dusk      — amber
            new Color(0.04f, 0.04f, 0.12f), // midnight  — deep blue again
        };

        private static readonly float[] SunIntensities =
        {
            0.04f,  // midnight
            0.75f,  // dawn
            1.50f,  // noon
            0.90f,  // dusk
            0.04f,  // midnight
        };

        private static readonly Color[] AmbientColors =
        {
            new Color(0.02f, 0.02f, 0.07f), // midnight
            new Color(0.22f, 0.16f, 0.10f), // dawn
            new Color(0.38f, 0.36f, 0.32f), // noon
            new Color(0.28f, 0.16f, 0.09f), // dusk
            new Color(0.02f, 0.02f, 0.07f), // midnight
        };

        private static readonly Color[] FogColors =
        {
            new Color(0.08f, 0.08f, 0.18f), // midnight
            new Color(0.75f, 0.55f, 0.40f), // dawn
            new Color(0.68f, 0.55f, 0.42f), // noon
            new Color(0.80f, 0.52f, 0.30f), // dusk
            new Color(0.08f, 0.08f, 0.18f), // midnight
        };

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Start()
        {
            _timeOfDay = Mathf.Clamp01(startTimeOfDay);
            _sunLight  = FindDirectionalLight();

            if (_sunLight == null)
                Debug.LogWarning("[WeatherSystem] No directional light found — day/night disabled.");

            // Build rain particle system (disabled until weather demands it)
            _rainPS = BuildRainSystem();

            // Kick off weather event loop
            if (enableWeatherEvents)
                StartCoroutine(WeatherCycleCoroutine());

            // Snap to start time immediately
            if (_sunLight != null)
                ApplyTimeOfDay(_timeOfDay);

            Debug.Log($"[WeatherSystem] Started. DayDuration={dayDuration}s WeatherEvents={enableWeatherEvents}");
        }

        private void Update()
        {
            if (_sunLight == null || dayDuration <= 0f) return;

            _timeOfDay += Time.deltaTime / dayDuration;
            if (_timeOfDay >= 1f) _timeOfDay -= 1f;

            ApplyTimeOfDay(_timeOfDay);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Public API
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Current weather state.</summary>
        public WeatherState CurrentWeather => _currentWeather;

        /// <summary>Normalised time of day in [0, 1). 0.5 = solar noon.</summary>
        public float TimeOfDay => _timeOfDay;

        /// <summary>
        /// Force a specific weather state immediately.
        /// </summary>
        public void SetWeather(WeatherState state)
        {
            _currentWeather = state;
            ApplyWeatherVisuals(state);
            Debug.Log($"[WeatherSystem] Weather → {state}");
        }

        // ══════════════════════════════════════════════════════════════════
        //  Day / Night
        // ══════════════════════════════════════════════════════════════════

        private void ApplyTimeOfDay(float t)
        {
            // Map t ∈ [0,1] into 4-segment piecewise lerp across 5 keyframes
            float segments  = SunColors.Length - 1; // 4
            float fi        = t * segments;
            int   i0        = Mathf.FloorToInt(fi);
            int   i1        = Mathf.Min(i0 + 1, SunColors.Length - 1);
            float lt        = fi - i0;

            if (_sunLight != null)
            {
                _sunLight.color     = Color.Lerp(SunColors[i0],      SunColors[i1],      lt);
                _sunLight.intensity = Mathf.Lerp(SunIntensities[i0], SunIntensities[i1], lt);

                // Rotate sun: full 360° revolution per day, rising from east (−X)
                float sunPitch = t * 360f - 90f; // -90° at midnight (below horizon)
                _sunLight.transform.rotation = Quaternion.Euler(sunPitch, -30f, 0f);
            }

            // Ambient light
            Color ambient = Color.Lerp(AmbientColors[i0], AmbientColors[i1], lt);
            RenderSettings.ambientLight = ambient;

            // Fog colour (always applied; density driven by weather state)
            RenderSettings.fogColor = Color.Lerp(FogColors[i0], FogColors[i1], lt);

            // Night-time fog density bonus (extra 0.004 at midnight)
            float nightBonus = Mathf.Clamp01(1f - Mathf.Sin(t * Mathf.PI)) * 0.004f;
            float baseFog    = GetBaseFogDensity(_currentWeather);
            RenderSettings.fogDensity = baseFog + nightBonus;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Weather Events
        // ══════════════════════════════════════════════════════════════════

        private IEnumerator WeatherCycleCoroutine()
        {
            while (true)
            {
                float delay = Random.Range(minWeatherInterval, maxWeatherInterval);
                yield return new WaitForSeconds(delay);

                WeatherState next = PickRandomWeather();
                if (next != _currentWeather)
                    SetWeather(next);

                // Weather event lasts a random duration then clears
                float duration = Random.Range(20f, 60f);
                yield return new WaitForSeconds(duration);

                if (_currentWeather != WeatherState.Clear)
                    SetWeather(WeatherState.Clear);
            }
        }

        private static WeatherState PickRandomWeather()
        {
            float r = Random.value;
            if (r < 0.45f) return WeatherState.Clear;
            if (r < 0.70f) return WeatherState.Foggy;
            if (r < 0.88f) return WeatherState.Rainy;
            return WeatherState.Stormy;
        }

        private void ApplyWeatherVisuals(WeatherState state)
        {
            RenderSettings.fogDensity = GetBaseFogDensity(state);

            if (_rainPS == null) return;

            switch (state)
            {
                case WeatherState.Clear:
                case WeatherState.Foggy:
                    _rainPS.Stop(withChildren: true, stopBehavior: ParticleSystemStopBehavior.StopEmitting);
                    break;

                case WeatherState.Rainy:
                {
                    _rainPS.Play();
                    var em = _rainPS.emission;
                    em.rateOverTime = 200f;
                    break;
                }

                case WeatherState.Stormy:
                {
                    _rainPS.Play();
                    var em = _rainPS.emission;
                    em.rateOverTime = 500f;
                    break;
                }
            }
        }

        private float GetBaseFogDensity(WeatherState state)
        {
            return state switch
            {
                WeatherState.Foggy  => foggyFogDensity,
                WeatherState.Rainy  => rainyFogDensity,
                WeatherState.Stormy => stormyFogDensity,
                _                   => clearFogDensity,
            };
        }

        // ══════════════════════════════════════════════════════════════════
        //  Rain Particle System
        // ══════════════════════════════════════════════════════════════════

        private ParticleSystem BuildRainSystem()
        {
            var go = new GameObject("VFX_Rain");
            go.transform.SetParent(transform, worldPositionStays: false);
            // Elevated emitter — particles fall into the scene
            go.transform.localPosition = new Vector3(0f, 30f, 0f);
            // Slight tilt for wind-driven rain look
            go.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);

            var ps   = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop            = true;
            main.playOnAwake     = false;
            main.duration        = 1f;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
            main.startSpeed      = new ParticleSystem.MinMaxCurve(16f, 26f);
            main.startSize       = new ParticleSystem.MinMaxCurve(0.04f, 0.10f);
            main.startColor      = new Color(0.70f, 0.80f, 1.00f, 0.50f);
            main.gravityModifier = 1.8f;
            main.maxParticles    = 3000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // Fade out near end of lifetime (splat effect)
            var col  = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.70f, 0.82f, 1.00f), 0f),
                    new GradientColorKey(new Color(0.80f, 0.88f, 1.00f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0.50f, 0.0f),
                    new GradientAlphaKey(0.30f, 0.8f),
                    new GradientAlphaKey(0.00f, 1.0f),
                }
            );
            col.color = grad;

            // Zero emission by default — driven by ApplyWeatherVisuals
            var emiss = ps.emission;
            emiss.rateOverTime = 0f;

            // Wide box emitter covering the play area
            var shape = ps.shape;
            shape.enabled   = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale     = new Vector3(320f, 1f, 320f);

            return ps;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Utility
        // ══════════════════════════════════════════════════════════════════

        private static Light FindDirectionalLight()
        {
#if UNITY_2023_1_OR_NEWER
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
#else
            var lights = FindObjectsOfType<Light>();
#endif
            foreach (var l in lights)
                if (l.type == LightType.Directional)
                    return l;
            return null;
        }
    }
}
