// ---------------------------------------------------------------------------
// WindSystem.cs — wind direction + strength simulation
// ---------------------------------------------------------------------------
// Wind direction shifts gradually over time using smooth angle interpolation.
// Strength oscillates sinusoidally between calm and gale.
//
// The registered player ShipController receives a SetWindMultiplier() call
// every frame so sailing direction relative to the wind modifies speed:
//   • Tailwind  (+35% max bonus)  — sailing downwind is faster
//   • Headwind  (-25% max penalty) — sailing upwind is slower
//
// NavigationUI and HUDManager read WindSystem.Current for wind indicators.
// BootyBootstrap registers the player ship via RegisterPlayerShip().
// ---------------------------------------------------------------------------

using UnityEngine;
using Booty.Ships;

namespace Booty.World
{
    /// <summary>
    /// Simulates Caribbean trade winds. Direction shifts over time; strength
    /// oscillates. Applies a speed multiplier to the registered player ship.
    /// </summary>
    public class WindSystem : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Inspector
        // ══════════════════════════════════════════════════════════════════

        [Header("Direction Change")]
        [Tooltip("Average seconds between wind direction changes.")]
        [SerializeField] private float directionChangePeriod = 30f;

        [Tooltip("Degrees per second the wind direction lerps toward the new target.")]
        [SerializeField] private float directionLerpSpeed = 0.8f;

        [Header("Strength")]
        [Tooltip("Base wind strength (0 = calm, 1 = gale).")]
        [SerializeField, Range(0f, 1f)] private float baseStrength = 0.5f;

        [Tooltip("Amplitude of the sinusoidal strength oscillation (0–0.5).")]
        [SerializeField, Range(0f, 0.5f)] private float strengthVariance = 0.25f;

        [Tooltip("Period (seconds) of the strength oscillation cycle.")]
        [SerializeField] private float strengthPeriod = 20f;

        [Header("Ship Effect")]
        [Tooltip("Max speed bonus when sailing directly downwind (0 = no bonus).")]
        [SerializeField, Range(0f, 1f)] private float maxTailwindBonus = 0.35f;

        [Tooltip("Max speed penalty when sailing directly upwind (0 = no penalty).")]
        [SerializeField, Range(0f, 0.5f)] private float maxHeadwindPenalty = 0.25f;

        // ══════════════════════════════════════════════════════════════════
        //  Singleton
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Active WindSystem instance. Null if none exists.</summary>
        public static WindSystem Current { get; private set; }

        // ══════════════════════════════════════════════════════════════════
        //  Runtime State
        // ══════════════════════════════════════════════════════════════════

        private float _currentAngle;    // degrees, 0=North, clockwise on XZ plane
        private float _targetAngle;     // target we are lerping toward
        private float _changeTimer;     // seconds until next direction change
        private float _timeAccum;       // accumulator for strength sine wave

        private ShipController _playerShip;

        // ══════════════════════════════════════════════════════════════════
        //  Public Properties
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Current wind direction as a world-space unit vector on the XZ plane.
        /// Points in the direction the wind is blowing toward.
        /// </summary>
        public Vector3 WindDirection
        {
            get
            {
                float rad = _currentAngle * Mathf.Deg2Rad;
                return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            }
        }

        /// <summary>Current wind strength in the range [0, 1].</summary>
        public float WindStrength { get; private set; }

        /// <summary>Wind angle in degrees (0 = North, 90 = East, clockwise).</summary>
        public float WindAngleDeg => _currentAngle;

        /// <summary>
        /// Compass direction the wind is blowing toward ("N", "NE", "E" …).
        /// </summary>
        public string WindCardinal => AngleToCardinal(_currentAngle);

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Awake()
        {
            Current        = this;
            _currentAngle  = Random.Range(0f, 360f);
            _targetAngle   = _currentAngle;
            _changeTimer   = directionChangePeriod;
            WindStrength   = baseStrength;
        }

        private void Update()
        {
            float dt   = Time.deltaTime;
            _timeAccum += dt;

            // ── Direction ──────────────────────────────────────────────────
            _changeTimer -= dt;
            if (_changeTimer <= 0f)
            {
                _changeTimer  = directionChangePeriod + Random.Range(-5f, 5f);
                _targetAngle  = Random.Range(0f, 360f);
                Debug.Log($"[WindSystem] Wind shifting to " +
                           $"{AngleToCardinal(_targetAngle)} ({_targetAngle:F0}°)");
            }

            // Smooth angle interpolation (handles 360° wrap)
            float delta = Mathf.DeltaAngle(_currentAngle, _targetAngle);
            _currentAngle = Mathf.MoveTowardsAngle(
                _currentAngle,
                _currentAngle + delta,
                directionLerpSpeed * dt * 60f);
            _currentAngle = (_currentAngle + 360f) % 360f;

            // ── Strength ───────────────────────────────────────────────────
            float sine   = Mathf.Sin(_timeAccum * (Mathf.PI * 2f / strengthPeriod));
            WindStrength = Mathf.Clamp01(baseStrength + sine * strengthVariance);

            // ── Apply to player ship ───────────────────────────────────────
            if (_playerShip != null)
                _playerShip.SetWindMultiplier(GetSpeedMultiplier(_playerShip.transform.eulerAngles.y));
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Public API
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Register the player's ShipController so wind affects its speed.
        /// Called once by BootyBootstrap after the player ship is created.
        /// </summary>
        public void RegisterPlayerShip(ShipController shipController)
        {
            _playerShip = shipController;
            Debug.Log("[WindSystem] Player ship registered.");
        }

        /// <summary>
        /// Compute the speed multiplier for a ship sailing at
        /// <paramref name="shipHeadingDeg"/> degrees.
        /// Returns a value in the range [(1 - maxHeadwindPenalty), (1 + maxTailwindBonus)].
        /// </summary>
        /// <param name="shipHeadingDeg">
        /// The ship's Y-axis Euler angle (0 = North, 90 = East).
        /// </param>
        public float GetSpeedMultiplier(float shipHeadingDeg)
        {
            // Dot product: +1 = sailing with wind (tailwind), -1 = against it (headwind)
            float windRad = _currentAngle  * Mathf.Deg2Rad;
            float shipRad = shipHeadingDeg * Mathf.Deg2Rad;
            float dot     = Mathf.Cos(windRad - shipRad);

            float windEffect = dot >= 0f
                ? dot * maxTailwindBonus  * WindStrength   // tailwind bonus
                : dot * maxHeadwindPenalty * WindStrength;  // headwind penalty (dot is negative)

            return 1f + windEffect;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Private Helpers
        // ══════════════════════════════════════════════════════════════════

        private static string AngleToCardinal(float deg)
        {
            deg = (deg % 360f + 360f) % 360f;
            int sector = Mathf.RoundToInt(deg / 45f) % 8;
            string[] dirs = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            return dirs[sector];
        }
    }
}
