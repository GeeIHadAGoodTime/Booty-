// ---------------------------------------------------------------------------
// AudioManager.cs — Central audio manager: SFX and music playback
// ---------------------------------------------------------------------------
// Per PRD A6.x: AudioManager is a regular MonoBehaviour — no static Instance.
// CombatAudio and AmbientAudio components locate this via FindObjectOfType<AudioManager>()
// in their Start() methods.
//
// Provides two AudioSource channels:
//   _sfxSource   — one-shot spatial SFX (3D, spatial blend = 1)
//   _musicSource — looping background music (2D, spatial blend = 0)
//
// SCENE SETUP:
//   Add an empty GameObject named "AudioManager" to the scene and attach this
//   script. No Inspector wiring required — both AudioSources are created at runtime.
// ---------------------------------------------------------------------------

using System.Collections;
using UnityEngine;

namespace Booty.Audio
{
    /// <summary>
    /// Central audio manager providing SFX and music playback for the entire scene.
    /// Exposes two AudioSource channels: one for spatial one-shot SFX and one for
    /// looping 2D background music. Located at runtime via
    /// <c>FindObjectOfType&lt;AudioManager&gt;()</c> — no static Instance.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Audio Channels
        // ══════════════════════════════════════════════════════════════════

        private AudioSource _sfxSource;
        private AudioSource _musicSource;

        // ── UI Clip cache ──────────────────────────────────────────────────
        private AudioClip _uiClickClip;
        private AudioClip _goldCoinClip;
        private AudioClip _chimeClip;

        // ══════════════════════════════════════════════════════════════════
        //  Volume Settings
        // ══════════════════════════════════════════════════════════════════

        private float _sfxVolume   = 1.0f;
        private float _musicVolume = 0.5f;

        /// <summary>Current SFX volume in the [0, 1] range.</summary>
        public float SFXVolume   { get; private set; } = 1.0f;

        /// <summary>Current music volume in the [0, 1] range.</summary>
        public float MusicVolume { get; private set; } = 0.5f;

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Awake()
        {
            // ── SFX channel (spatial 3D) ──────────────────────────────────
            _sfxSource               = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake   = false;
            _sfxSource.spatialBlend  = 1f;    // fully 3D
            _sfxSource.volume        = _sfxVolume;

            // ── Music channel (2D, looping) ───────────────────────────────
            _musicSource              = gameObject.AddComponent<AudioSource>();
            _musicSource.playOnAwake  = false;
            _musicSource.spatialBlend = 0f;   // fully 2D
            _musicSource.loop         = true;
            _musicSource.volume       = _musicVolume;

            // ── UI clips ──────────────────────────────────────────────────
            _uiClickClip  = CreateClickClip();
            _goldCoinClip = CreateGoldCoinClip();
            _chimeClip    = CreateChimeClip();
        }

        // ══════════════════════════════════════════════════════════════════
        //  Public API — SFX
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Play a one-shot sound effect at a world position using 3D spatial audio.
        /// Uses <see cref="AudioSource.PlayClipAtPoint"/> so the sound persists even
        /// if the caller is destroyed before the clip finishes.
        /// </summary>
        /// <param name="clip">The audio clip to play.</param>
        /// <param name="position">World-space position at which the sound originates.</param>
        /// <param name="volumeScale">Multiplier applied to <see cref="SFXVolume"/> (default 1).</param>
        public void PlaySFX(AudioClip clip, Vector3 position, float volumeScale = 1f)
        {
            if (clip == null)
            {
                Debug.LogWarning("[AudioManager] PlaySFX called with null clip.");
                return;
            }

            AudioSource.PlayClipAtPoint(clip, position, SFXVolume * volumeScale);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Public API — Music
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Play background music, replacing any currently playing track.
        /// </summary>
        /// <param name="clip">The music clip to play.</param>
        /// <param name="loop">Whether the track should loop (default <c>true</c>).</param>
        public void PlayMusic(AudioClip clip, bool loop = true)
        {
            if (clip == null)
            {
                Debug.LogWarning("[AudioManager] PlayMusic called with null clip.");
                return;
            }

            _musicSource.Stop();
            _musicSource.clip   = clip;
            _musicSource.volume = MusicVolume;
            _musicSource.loop   = loop;
            _musicSource.Play();
        }

        /// <summary>
        /// Fade out the currently playing music over <paramref name="duration"/> seconds,
        /// then stop the music source.
        /// </summary>
        /// <param name="duration">Time in seconds over which to fade to silence.</param>
        public void FadeMusic(float duration)
        {
            if (duration <= 0f)
            {
                _musicSource.Stop();
                return;
            }

            StartCoroutine(FadeMusicRoutine(duration));
        }

        // ══════════════════════════════════════════════════════════════════
        //  Public API — Volume
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Set the master SFX volume. The value is clamped to [0, 1].
        /// Affects all subsequent <see cref="PlaySFX"/> calls immediately.
        /// </summary>
        /// <param name="volume">Desired volume level in the [0, 1] range.</param>
        public void SetSFXVolume(float volume)
        {
            _sfxVolume = Mathf.Clamp01(volume);
            SFXVolume  = _sfxVolume;
            _sfxSource.volume = _sfxVolume;
        }

        /// <summary>
        /// Set the background music volume. The value is clamped to [0, 1].
        /// Takes effect immediately on the currently playing track.
        /// </summary>
        /// <param name="volume">Desired volume level in the [0, 1] range.</param>
        public void SetMusicVolume(float volume)
        {
            _musicVolume = Mathf.Clamp01(volume);
            MusicVolume  = _musicVolume;
            _musicSource.volume = _musicVolume;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Public API — Control
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Stop all audio immediately (both SFX and music channels).
        /// </summary>
        public void StopAll()
        {
            _sfxSource.Stop();
            _musicSource.Stop();
        }

        // ══════════════════════════════════════════════════════════════════
        //  Public API — UI Sound Effects
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Play a short UI click sound (button press).</summary>
        public void PlayClick()    => PlaySFX(_uiClickClip,  Vector3.zero, 0.7f);

        /// <summary>Play a gold coin pickup sound.</summary>
        public void PlayGoldCoin() => PlaySFX(_goldCoinClip, Vector3.zero, 0.8f);

        /// <summary>Play a chime sound (achievement / positive feedback).</summary>
        public void PlayChime()    => PlaySFX(_chimeClip,    Vector3.zero, 0.9f);

        // ══════════════════════════════════════════════════════════════════
        //  Internal — Coroutines
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Coroutine: linearly fades <see cref="_musicSource"/> volume from its
        /// current level to 0 over <paramref name="duration"/> seconds, then stops.
        /// </summary>
        private IEnumerator FadeMusicRoutine(float duration)
        {
            float startVolume = _musicSource.volume;
            float elapsed     = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
                yield return null;
            }

            _musicSource.volume = 0f;
            _musicSource.Stop();
        }

        // ══════════════════════════════════════════════════════════════════
        //  Internal — UI Clip Generators
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Short 1200 Hz sine click with sharp exponential decay (0.08s).
        /// </summary>
        private static AudioClip CreateClickClip()
        {
            int   sampleRate = 44100;
            float duration   = 0.08f;
            int   len        = (int)(sampleRate * duration);
            float[] samples  = new float[len];

            for (int i = 0; i < len; i++)
            {
                float t = (float)i / sampleRate;
                samples[i] = Mathf.Clamp(
                    Mathf.Sin(2f * Mathf.PI * 1200f * t) * Mathf.Exp(-t * 50f),
                    -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("UIClick", len, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Satisfying metallic coin strike: harmonic series (1200, 2400, 3600, 4800 Hz)
        /// with 8 ms sharp attack and 6-rate exponential ring-out over 0.5 s.
        /// Subtle per-overtone inharmonicity replicates the natural detuning of metal.
        /// </summary>
        private static AudioClip CreateGoldCoinClip()
        {
            int   sampleRate = 44100;
            float duration   = 0.5f;
            int   len        = (int)(sampleRate * duration);
            float[] samples  = new float[len];

            // Harmonic series: fundamental + overtones for a metallic bell-like coin strike
            float[] harmonics = { 1200f, 2400f, 3600f, 4800f };
            float[] weights   = { 0.55f,  0.28f,  0.12f,  0.05f };

            for (int i = 0; i < len; i++)
            {
                float t = (float)i / sampleRate;

                // Very sharp 8 ms attack then exponential ring-out
                float attack = Mathf.Min(t / 0.008f, 1f);
                float decay  = Mathf.Exp(-t * 6f);
                float env    = attack * decay;

                float sample = 0f;
                for (int h = 0; h < harmonics.Length; h++)
                {
                    // Slight natural inharmonicity per overtone (real metal is never perfectly tuned)
                    float detune = 1f + 0.003f * Mathf.Sin(2f * Mathf.PI * (8f + h * 2f) * t);
                    sample += Mathf.Sin(2f * Mathf.PI * harmonics[h] * detune * t) * weights[h];
                }

                samples[i] = Mathf.Clamp(sample * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("GoldCoin", len, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// C major triad chime: C5(523Hz) + E5(659Hz) + G5(784Hz), 0.8s with gentle attack.
        /// </summary>
        private static AudioClip CreateChimeClip()
        {
            int   sampleRate = 44100;
            float duration   = 0.8f;
            int   len        = (int)(sampleRate * duration);
            float[] samples  = new float[len];

            float[] freqs = { 523f, 659f, 784f }; // C5, E5, G5

            for (int i = 0; i < len; i++)
            {
                float t = (float)i / sampleRate;

                // Gentle attack (0–0.1s), sustain, fade (last 0.3s)
                float attack  = Mathf.Min(t / 0.1f, 1f);
                float release = Mathf.Min((duration - t) / 0.3f, 1f);
                float env     = Mathf.Clamp01(attack * release);

                float sample = 0f;
                foreach (float freq in freqs)
                    sample += Mathf.Sin(2f * Mathf.PI * freq * t) * 0.2f;

                samples[i] = Mathf.Clamp(sample * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Chime", len, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
