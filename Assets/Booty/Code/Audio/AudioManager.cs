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
    }
}
