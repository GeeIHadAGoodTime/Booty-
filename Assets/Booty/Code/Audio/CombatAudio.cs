// ---------------------------------------------------------------------------
// CombatAudio.cs — Procedural combat sound effects
// ---------------------------------------------------------------------------
// Per PRD A6.x: No WAV imports. All AudioClips generated at runtime via
// AudioClip.Create() + SetData(). Subscribes to static and instance events
// from BroadsideSystem and HPSystem.
//
// SCENE SETUP:
//   Attach to an empty GameObject in the scene. AudioManager is located via
//   FindObjectOfType<AudioManager>() in Start() — no Inspector wiring needed.
// ---------------------------------------------------------------------------

using UnityEngine;
using Booty.Combat;

namespace Booty.Audio
{
    /// <summary>
    /// Listens to combat events (broadside fired, ship damaged, ship destroyed)
    /// and plays procedurally-generated audio clips through AudioManager.
    /// </summary>
    public class CombatAudio : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Private State
        // ══════════════════════════════════════════════════════════════════

        private AudioManager _audio;

        private AudioClip _cannonFireClip;
        private AudioClip _impactClip;
        private AudioClip _splashClip;
        private AudioClip _creakClip;
        private AudioClip _explosionClip;

        private HPSystem[] _trackedHPSystems;

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Start()
        {
            // Locate or create AudioManager
            _audio = FindObjectOfType<AudioManager>();
            if (_audio == null)
            {
                _audio = new GameObject("AudioManager").AddComponent<AudioManager>();
            }

            // Generate all procedural clips
            _cannonFireClip = CreateCannonFireClip();
            _impactClip     = CreateImpactClip();
            _splashClip     = CreateSplashClip();
            _creakClip      = CreateCreakClip();
            _explosionClip  = CreateExplosionClip();

            // Subscribe to broadside static event
            BroadsideSystem.OnBroadsideFired += OnCannonFired;

            // Subscribe to per-ship HP events
            _trackedHPSystems = FindObjectsOfType<HPSystem>();
            foreach (HPSystem hp in _trackedHPSystems)
            {
                hp.OnDamaged   += OnShipDamaged;
                hp.OnDestroyed += OnShipDestroyed;
            }
        }

        private void OnDestroy()
        {
            BroadsideSystem.OnBroadsideFired -= OnCannonFired;

            if (_trackedHPSystems != null)
            {
                foreach (HPSystem hp in _trackedHPSystems)
                {
                    if (hp == null) continue;
                    hp.OnDamaged   -= OnShipDamaged;
                    hp.OnDestroyed -= OnShipDestroyed;
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Event Handlers
        // ══════════════════════════════════════════════════════════════════

        private void OnCannonFired(Vector3 position)
            => _audio?.PlaySFX(_cannonFireClip, position);

        private void OnShipDamaged(int current, int max)
            => _audio?.PlaySFX(_impactClip, transform.position, 0.8f);

        private void OnShipDestroyed()
            => _audio?.PlaySFX(_explosionClip, transform.position);

        // ══════════════════════════════════════════════════════════════════
        //  Procedural Clip Generators
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Low cannon-boom: 60% sine at 80 Hz + 40% white noise, 0.5 s, exponential decay.
        /// </summary>
        private static AudioClip CreateCannonFireClip()
        {
            const int   frequency   = 44100;
            const float duration    = 0.5f;
            const float sineFreq    = 80f;
            const float decayRate   = 8f;

            int     sampleCount = Mathf.RoundToInt(frequency * duration);
            float[] samples     = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t         = (float)i / frequency;
                float envelope  = Mathf.Exp(-t * decayRate);
                float sine      = Mathf.Sin(2f * Mathf.PI * sineFreq * t) * 0.8f;
                float noise     = (Random.value * 2f - 1f) * 0.3f;
                samples[i]      = (sine * 0.6f + noise * 0.4f) * envelope;
            }

            AudioClip clip = AudioClip.Create("CannonFire", sampleCount, 1, frequency, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Mid-thud impact: damped sine at 200 Hz, 0.3 s.
        /// </summary>
        private static AudioClip CreateImpactClip()
        {
            const int   frequency   = 44100;
            const float duration    = 0.3f;
            const float sineFreq    = 200f;
            const float decayRate   = 20f;

            int     sampleCount = Mathf.RoundToInt(frequency * duration);
            float[] samples     = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t      = (float)i / frequency;
                samples[i]   = Mathf.Sin(2f * Mathf.PI * sineFreq * t) * Mathf.Exp(-t * decayRate);
            }

            AudioClip clip = AudioClip.Create("Impact", sampleCount, 1, frequency, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// White-noise splash: ramp up 0-0.1 s, then exponential decay 0.1-0.4 s.
        /// </summary>
        private static AudioClip CreateSplashClip()
        {
            const int   frequency   = 44100;
            const float duration    = 0.4f;
            const float rampEnd     = 0.1f;
            const float decayRate   = 5f;

            int     sampleCount = Mathf.RoundToInt(frequency * duration);
            float[] samples     = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t        = (float)i / frequency;
                float ramp     = Mathf.Min(t / rampEnd, 1f);
                float decay    = Mathf.Exp(-(t - rampEnd) * decayRate);
                float envelope = ramp * decay;
                samples[i]     = (Random.value * 2f - 1f) * envelope;
            }

            AudioClip clip = AudioClip.Create("Splash", sampleCount, 1, frequency, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Low hull-creak: 40 Hz sine + 3 Hz noise modulation, gentle 2 s^-1 decay, 1.0 s.
        /// </summary>
        private static AudioClip CreateCreakClip()
        {
            const int   frequency   = 44100;
            const float duration    = 1.0f;
            const float baseFreq    = 40f;
            const float noiseFreq   = 3f;
            const float decayRate   = 2f;

            int     sampleCount = Mathf.RoundToInt(frequency * duration);
            float[] samples     = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t      = (float)i / frequency;
                float sample = Mathf.Sin(2f * Mathf.PI * baseFreq  * t) * 0.4f
                             + Mathf.Sin(2f * Mathf.PI * noiseFreq  * t) * 0.3f;
                sample      *= Mathf.Exp(-t * decayRate);
                samples[i]   = sample;
            }

            AudioClip clip = AudioClip.Create("Creak", sampleCount, 1, frequency, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Explosion: layered white noise + 60 Hz bass, attack-decay envelope, 1.0 s. Clamped to [-1, 1].
        /// </summary>
        private static AudioClip CreateExplosionClip()
        {
            const int   frequency   = 44100;
            const float duration    = 1.0f;
            const float bassFreq    = 60f;
            const float decayRate   = 4f;

            int     sampleCount = Mathf.RoundToInt(frequency * duration);
            float[] samples     = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t        = (float)i / frequency;
                float noise    = (Random.value * 2f - 1f) * 0.5f;
                float bass     = Mathf.Sin(2f * Mathf.PI * bassFreq * t) * 0.5f;
                float envelope = Mathf.Exp(-t * decayRate);
                samples[i]     = Mathf.Clamp((noise + bass) * envelope, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Explosion", sampleCount, 1, frequency, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
