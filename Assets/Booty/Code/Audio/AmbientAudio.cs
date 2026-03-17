using UnityEngine;

namespace Booty.Audio
{
    /// <summary>
    /// Manages procedural ambient audio and adaptive music for sailing/combat states.
    /// Attach to a persistent GameObject in the scene.
    /// </summary>
    public class AmbientAudio : MonoBehaviour
    {
        // ─── Music State ────────────────────────────────────────────────────────
        private enum MusicState { Sailing, Combat, Port }

        // ─── AudioManager reference ──────────────────────────────────────────
        private AudioManager _audio;

        // ─── Ambient AudioSources (managed directly, NOT via AudioManager) ───
        private AudioSource _wavesSource;
        private AudioSource _windSource;

        // ─── Procedurally generated clips ────────────────────────────────────
        private AudioClip _oceanWavesClip;
        private AudioClip _windClip;
        private AudioClip _seagullClip;
        private AudioClip _seaShantyClip;
        private AudioClip _combatMusicClip;
        private AudioClip _portMusicClip;

        // ─── State ────────────────────────────────────────────────────────────
        private MusicState _musicState = MusicState.Sailing;
        private float _combatTimer = 0f;
        private const float CombatCooldown = 10f; // seconds after last hit before returning to sailing music

        private float _seagullTimer = 0f;
        private float _nextSeagullTime = 8f; // seconds between seagull calls

        // ─── Awake ────────────────────────────────────────────────────────────
        private void Awake()
        {
            // Create two private AudioSources for ambient loops
            _wavesSource = gameObject.AddComponent<AudioSource>();
            _wavesSource.loop = true;
            _wavesSource.spatialBlend = 0f; // 2D
            _wavesSource.volume = 0.4f;

            _windSource = gameObject.AddComponent<AudioSource>();
            _windSource.loop = true;
            _windSource.spatialBlend = 0f;
            _windSource.volume = 0.2f;
        }

        // ─── Start ────────────────────────────────────────────────────────────
        private void Start()
        {
            // Find or create AudioManager
            _audio = FindObjectOfType<AudioManager>();
            if (_audio == null)
                _audio = new GameObject("AudioManager").AddComponent<AudioManager>();

            // Generate all clips procedurally
            _oceanWavesClip = CreateOceanWavesClip();
            _windClip = CreateWindClip();
            _seagullClip = CreateSeagullClip();
            _seaShantyClip = CreateSeaShantyClip();
            _combatMusicClip = CreateCombatMusicClip();
            _portMusicClip = CreatePortMusicClip();

            // Subscribe to combat events
            Booty.Combat.HPSystem[] hpSystems = FindObjectsOfType<Booty.Combat.HPSystem>();
            foreach (var hp in hpSystems)
                hp.OnDamaged += OnCombatEvent;

            Booty.Combat.BroadsideSystem.OnBroadsideFired += OnBroadsideFired;

            // Start ambient loops
            _wavesSource.clip = _oceanWavesClip;
            _wavesSource.Play();

            _windSource.clip = _windClip;
            _windSource.Play();

            // Start sailing music
            _audio.PlayMusic(_seaShantyClip);
        }

        // ─── OnDestroy ────────────────────────────────────────────────────────
        private void OnDestroy()
        {
            // Unsubscribe from HPSystem events
            Booty.Combat.HPSystem[] hpSystems = FindObjectsOfType<Booty.Combat.HPSystem>();
            foreach (var hp in hpSystems)
                hp.OnDamaged -= OnCombatEvent;

            // Unsubscribe from BroadsideSystem event
            Booty.Combat.BroadsideSystem.OnBroadsideFired -= OnBroadsideFired;
        }

        // ─── Update ───────────────────────────────────────────────────────────
        private void Update()
        {
            // Seagull chirps at random intervals
            _seagullTimer += Time.deltaTime;
            if (_seagullTimer >= _nextSeagullTime)
            {
                _seagullTimer = 0f;
                _nextSeagullTime = Random.Range(6f, 15f);
                Vector3 randomPos = Camera.main != null
                    ? Camera.main.transform.position + Random.insideUnitSphere * 20f
                    : Vector3.zero;
                _audio?.PlaySFX(_seagullClip, randomPos, 0.6f);
            }

            // Music state machine
            if (_musicState == MusicState.Combat)
            {
                _combatTimer -= Time.deltaTime;
                if (_combatTimer <= 0f)
                {
                    _musicState = MusicState.Sailing;
                    _audio?.FadeMusic(2f);
                    Invoke(nameof(StartSailingMusic), 2.5f); // start sailing music after fade completes
                }
            }
        }

        // ─── Event Handlers ───────────────────────────────────────────────────
        private void OnCombatEvent(int current, int max) => TriggerCombatMusic();
        private void OnBroadsideFired(Vector3 pos) => TriggerCombatMusic();

        private void TriggerCombatMusic()
        {
            _combatTimer = CombatCooldown;
            if (_musicState != MusicState.Combat)
            {
                _musicState = MusicState.Combat;
                _audio?.PlayMusic(_combatMusicClip);
            }
        }

        private void StartSailingMusic()
        {
            _audio?.PlayMusic(_seaShantyClip);
        }

        // ─── Port Music ───────────────────────────────────────────────────────

        /// <summary>
        /// Call when the player enters a port zone. Switches music to the calm port theme.
        /// </summary>
        public void EnterPort()
        {
            _musicState = MusicState.Port;
            _audio?.PlayMusic(_portMusicClip);
        }

        /// <summary>
        /// Call when the player exits a port zone. Fades out port music and returns to sailing.
        /// </summary>
        public void ExitPort()
        {
            _musicState = MusicState.Sailing;
            _audio?.FadeMusic(1.5f);
            Invoke(nameof(StartSailingMusic), 2f);
        }

        // ─── Procedural Clip Generators ───────────────────────────────────────

        /// <summary>
        /// Ocean waves: layered sine waves + noise with seamless crossfade loop (4.0s).
        /// </summary>
        private AudioClip CreateOceanWavesClip()
        {
            int sampleRate = 44100;
            float duration = 4.0f;
            int len = (int)(sampleRate * duration); // 176400 samples
            float[] samples = new float[len];

            for (int i = 0; i < len; i++)
            {
                float t = (float)i / sampleRate;

                float lowFreq = Mathf.Sin(2f * Mathf.PI * 0.3f * t);   // 0.3Hz wave rhythm
                float midFreq = Mathf.Sin(2f * Mathf.PI * 0.7f * t);   // 0.7Hz ripple
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.3f;
                float sample = (lowFreq * 0.4f + midFreq * 0.2f + noise) * 0.5f;

                // Crossfade at start/end for seamless loop (first and last 10% of samples)
                float fade = Mathf.Min(
                    Mathf.Min((float)i / (len * 0.1f), 1f),
                    Mathf.Min((float)(len - i) / (len * 0.1f), 1f));
                sample *= fade;

                samples[i] = Mathf.Clamp(sample, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("OceanWaves", len, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Wind: slow-modulated noise with seamless crossfade loop (3.0s).
        /// </summary>
        private AudioClip CreateWindClip()
        {
            int sampleRate = 44100;
            float duration = 3.0f;
            int len = (int)(sampleRate * duration); // 132300 samples
            float[] samples = new float[len];

            for (int i = 0; i < len; i++)
            {
                float t = (float)i / sampleRate;

                float modulation = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 0.2f * t); // slow modulation
                float noise = (UnityEngine.Random.value * 2f - 1f);
                float sample = noise * modulation * 0.3f;

                // Crossfade at start/end for seamless loop (first and last 10% of samples)
                float fade = Mathf.Min(
                    Mathf.Min((float)i / (len * 0.1f), 1f),
                    Mathf.Min((float)(len - i) / (len * 0.1f), 1f));
                sample *= fade;

                samples[i] = Mathf.Clamp(sample, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Wind", len, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Seagull chirp: 3 short high-frequency sine bursts (0.6s).
        /// </summary>
        private AudioClip CreateSeagullClip()
        {
            int sampleRate = 44100;
            float duration = 0.6f;
            int len = (int)(sampleRate * duration); // 26460 samples
            float[] samples = new float[len];

            for (int i = 0; i < len; i++)
            {
                float t = (float)i / sampleRate;

                float chirpFreq = 0f;
                float chirpEnv = 0f;

                if (t >= 0f && t < 0.15f)
                {
                    chirpFreq = 2400f;
                    chirpEnv = Mathf.Sin(Mathf.PI * t / 0.15f);
                }
                else if (t >= 0.2f && t < 0.35f)
                {
                    chirpFreq = 2800f;
                    chirpEnv = Mathf.Sin(Mathf.PI * (t - 0.2f) / 0.15f);
                }
                else if (t >= 0.4f && t < 0.55f)
                {
                    chirpFreq = 2200f;
                    chirpEnv = Mathf.Sin(Mathf.PI * (t - 0.4f) / 0.15f);
                }

                float sample = chirpEnv > 0f
                    ? Mathf.Sin(2f * Mathf.PI * chirpFreq * t) * chirpEnv * 0.5f
                    : 0f;

                samples[i] = Mathf.Clamp(sample, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Seagull", len, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Sea shanty: C major pentatonic melody with 5th harmony (8.0s loop).
        /// </summary>
        private AudioClip CreateSeaShantyClip()
        {
            int sampleRate = 44100;
            float duration = 8.0f;
            int len = (int)(sampleRate * duration); // 352800 samples
            float[] samples = new float[len];

            // C pentatonic major scale (Hz)
            float[] pentatonic = { 523f, 587f, 659f, 784f, 880f }; // C5, D5, E5, G5, A5
            // 16-note repeating melody
            int[] melody = { 0, 2, 4, 3, 2, 0, 1, 3, 4, 3, 2, 4, 0, 2, 3, 1 };
            float noteDuration = 0.5f;

            for (int i = 0; i < len; i++)
            {
                float t = (float)i / sampleRate;

                int noteIndex = (int)(t / noteDuration) % melody.Length;
                float noteFreq = pentatonic[melody[noteIndex]];
                float noteT = t % noteDuration;

                // Envelope: quick attack (0-0.05s), sustain, quick release (last 0.1s)
                float env = Mathf.Min(noteT / 0.05f, 1f) * Mathf.Min((noteDuration - noteT) / 0.1f, 1f);
                env = Mathf.Clamp01(env);

                float sample = Mathf.Sin(2f * Mathf.PI * noteFreq * t) * env * 0.4f;
                // Subtle harmony: a 5th above (frequency * 1.5)
                sample += Mathf.Sin(2f * Mathf.PI * noteFreq * 1.5f * t) * env * 0.1f;

                samples[i] = Mathf.Clamp(sample, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("SeaShanty", len, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Combat music: fast minor pentatonic melody + rhythmic bass pulse (8.0s loop).
        /// </summary>
        private AudioClip CreateCombatMusicClip()
        {
            int sampleRate = 44100;
            float duration = 8.0f;
            int len = (int)(sampleRate * duration); // 352800 samples
            float[] samples = new float[len];

            // A minor pentatonic scale (Hz)
            float[] minorPent = { 220f, 261f, 293f, 329f, 392f }; // A3, C4, D4, E4, G4
            // 16-note repeating melody
            int[] combatMelody = { 0, 2, 4, 3, 1, 4, 2, 0, 3, 4, 1, 2, 0, 3, 4, 2 };
            float noteDuration = 0.25f;

            for (int i = 0; i < len; i++)
            {
                float t = (float)i / sampleRate;

                int noteIndex = (int)(t / noteDuration) % combatMelody.Length;
                float noteFreq = minorPent[combatMelody[noteIndex]];
                float noteT = t % noteDuration;

                // Sharper attack/release for punchy feel
                float env = Mathf.Min(noteT / 0.02f, 1f) * Mathf.Min((noteDuration - noteT) / 0.05f, 1f);
                env = Mathf.Clamp01(env);

                float melody_s = Mathf.Sin(2f * Mathf.PI * noteFreq * t) * env * 0.35f;

                // Rhythmic bass pulse (drum-like low thud every 0.5s)
                float pulsePhase = (t % 0.5f) / 0.5f; // 0-1 within each beat
                float bass = Mathf.Sin(2f * Mathf.PI * 80f * t) * Mathf.Exp(-pulsePhase * 8f) * 0.3f;

                float sample = melody_s + bass;
                samples[i] = Mathf.Clamp(sample, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("CombatMusic", len, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Port music: calm D major pentatonic melody with gentle bass notes (9.0s loop).
        /// D4(293Hz), E4(329Hz), F#4(370Hz), A4(440Hz), B4(493Hz) — slower, relaxed feel.
        /// </summary>
        private AudioClip CreatePortMusicClip()
        {
            int sampleRate = 44100;
            float duration = 9.0f;
            int len = (int)(sampleRate * duration); // 396900 samples
            float[] samples = new float[len];

            // D major pentatonic scale (Hz)
            float[] dPentatonic = { 293f, 329f, 370f, 440f, 493f }; // D4, E4, F#4, A4, B4
            // 12-note melody, slower/relaxed feel
            int[] portMelody = { 0, 2, 4, 3, 1, 4, 2, 0, 3, 4, 1, 2 };
            float noteDuration = 0.75f;

            for (int i = 0; i < len; i++)
            {
                float t = (float)i / sampleRate;

                int noteIndex = (int)(t / noteDuration) % portMelody.Length;
                float noteFreq = dPentatonic[portMelody[noteIndex]];
                float noteT = t % noteDuration;

                // Gentle envelope: slow attack (0-0.1s), sustain, release (last 0.15s)
                float env = Mathf.Min(noteT / 0.1f, 1f) * Mathf.Min((noteDuration - noteT) / 0.15f, 1f);
                env = Mathf.Clamp01(env);

                float melody_s = Mathf.Sin(2f * Mathf.PI * noteFreq * t) * env * 0.3f;

                // Gentle bass note at 146Hz every 1.5s
                float bassPhase = (t % 1.5f) / 1.5f;
                float bass = Mathf.Sin(2f * Mathf.PI * 146f * t) * Mathf.Exp(-bassPhase * 6f) * 0.15f;

                float sample = melody_s + bass;
                samples[i] = Mathf.Clamp(sample, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("PortMusic", len, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
