// ---------------------------------------------------------------------------
// ShipController.cs — WASD ship movement on the XZ nav plane
// ---------------------------------------------------------------------------
// Arcade-style movement per PRD A3 (Visual & Camera Constraints):
//   - Speed / turn_rate curves on the 2D plane.
//   - No wind, no buoyancy, no sail rig simulation.
//   - Simple linear acceleration capped at max speed.
//
// PREFAB SETUP (Ship.prefab):
//   Root GameObject:
//     - ShipController (this script)
//     - HPSystem
//     - BroadsideSystem
//     - Rigidbody  (useGravity=false, freeze Y position, freeze X/Z rotation)
//     - CapsuleCollider or BoxCollider (sized to hull shape)
//   Child "Model":
//     - MeshFilter + MeshRenderer (ship mesh / placeholder capsule)
//     - Oriented so the bow faces local +Z
//
//   For enemy ships, also add EnemyAI to the root.
// ---------------------------------------------------------------------------

using UnityEngine;

namespace Booty.Ships
{
    /// <summary>
    /// Arcade ship controller. Reads classic Input API (WASD) when
    /// <see cref="isPlayerControlled"/> is true; otherwise accepts
    /// programmatic input via <see cref="SetThrottle"/> / <see cref="SetRudder"/>.
    /// </summary>
    public class ShipController : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Tuning (Inspector)
        // ══════════════════════════════════════════════════════════════════

        [Header("Movement")]
        [SerializeField] private float maxSpeed     = 12f;
        [SerializeField] private float acceleration = 6f;
        [SerializeField] private float deceleration = 4f;
        [SerializeField] private float turnRate     = 90f;   // degrees / sec

        [Header("Control")]
        [SerializeField] private bool isPlayerControlled = true;

        // ══════════════════════════════════════════════════════════════════
        //  Runtime State (read-only properties for other systems)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Current forward speed (world units / sec).</summary>
        public float CurrentSpeed { get; private set; }

        /// <summary>Normalised speed (0 = stopped, 1 = max).</summary>
        public float SpeedNormalized => Mathf.Clamp01(CurrentSpeed / maxSpeed);

        /// <summary>World-space forward direction on the XZ plane.</summary>
        public Vector3 Forward => transform.forward;

        /// <summary>Unit right vector (starboard) on the XZ plane.</summary>
        public Vector3 Starboard => transform.right;

        /// <summary>Unit left vector (port side) on the XZ plane.</summary>
        public Vector3 Port => -transform.right;

        /// <summary>Whether this ship accepts WASD input.</summary>
        public bool IsPlayerControlled => isPlayerControlled;

        // ── Programmatic input (used by EnemyAI) ───────────────────────
        private float _aiThrottle;  // -1..1
        private float _aiRudder;    // -1..1

        // ── Damage multipliers (set by ShipDamageState) ─────────────────
        private float _speedMultiplier = 1f;   // hull damage → speed penalty
        private float _turnMultiplier  = 1f;   // sail damage → turn penalty

        // ══════════════════════════════════════════════════════════════════
        //  Public API
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Set throttle for AI-controlled ships. Clamped -1 (reverse) to 1 (full ahead).
        /// </summary>
        public void SetThrottle(float value)
        {
            _aiThrottle = Mathf.Clamp(value, -1f, 1f);
        }

        /// <summary>
        /// Set rudder for AI-controlled ships. Clamped -1 (port) to 1 (starboard).
        /// </summary>
        public void SetRudder(float value)
        {
            _aiRudder = Mathf.Clamp(value, -1f, 1f);
        }

        /// <summary>
        /// Configure movement parameters from data (e.g., ShipData).
        /// </summary>
        public void Configure(float speed, float turn, float accel)
        {
            maxSpeed     = speed;
            turnRate     = turn;
            acceleration = accel;
        }

        /// <summary>
        /// Switch this ship between player-controlled (WASD) and AI-controlled
        /// (SetThrottle/SetRudder) modes.
        /// </summary>
        public void SetPlayerControlled(bool value)
        {
            isPlayerControlled = value;
        }

        /// <summary>
        /// Apply damage-state multipliers (0-1) to speed and turn rate.
        /// Called by ShipDamageState when hull or sail takes damage.
        /// </summary>
        /// <param name="speed">Speed multiplier: 1.0 = full, 0.1 = minimum.</param>
        /// <param name="turn">Turn-rate multiplier: 1.0 = full, 0.1 = minimum.</param>
        public void SetDamageMultipliers(float speed, float turn)
        {
            _speedMultiplier = Mathf.Clamp(speed, 0.1f, 1f);
            _turnMultiplier  = Mathf.Clamp(turn,  0.1f, 1f);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Update Loop
        // ══════════════════════════════════════════════════════════════════

        private void Update()
        {
            float throttle;
            float rudder;

            if (isPlayerControlled)
            {
                // Classic Input API — W/S for thrust, A/D for yaw
                throttle = Input.GetAxis("Vertical");   // W = +1, S = -1
                rudder   = Input.GetAxis("Horizontal"); // D = +1, A = -1
            }
            else
            {
                throttle = _aiThrottle;
                rudder   = _aiRudder;
            }

            ApplyMovement(throttle, rudder);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Movement Math
        // ══════════════════════════════════════════════════════════════════

        private void ApplyMovement(float throttle, float rudder)
        {
            float dt = Time.deltaTime;

            // Apply damage multipliers to base stats
            float effectiveMaxSpeed = maxSpeed * _speedMultiplier;
            float effectiveTurnRate = turnRate * _turnMultiplier;

            // ── Acceleration / deceleration ─────────────────────────────
            float targetSpeed = throttle * effectiveMaxSpeed;

            if (Mathf.Abs(targetSpeed) > Mathf.Abs(CurrentSpeed))
            {
                // Accelerating
                CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, targetSpeed, acceleration * dt);
            }
            else
            {
                // Decelerating / braking
                CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, targetSpeed, deceleration * dt);
            }

            // ── Turning (yaw on Y axis) ─────────────────────────────────
            // Turn rate scales with speed — can't turn at full rate when stopped
            float speedFactor = Mathf.Clamp01(Mathf.Abs(CurrentSpeed) / (effectiveMaxSpeed * 0.2f));
            float yaw = rudder * effectiveTurnRate * speedFactor * dt;
            transform.Rotate(0f, yaw, 0f, Space.World);

            // ── Translation on XZ plane ─────────────────────────────────
            Vector3 movement = transform.forward * (CurrentSpeed * dt);
            movement.y = 0f; // enforce nav plane
            transform.position += movement;

            // Clamp Y to zero (safety — physics should also handle this)
            Vector3 pos = transform.position;
            pos.y = 0f;
            transform.position = pos;
        }
    }
}
