// ---------------------------------------------------------------------------
// IsometricCamera.cs — Fixed isometric follow camera  (S3.2 POLISH)
// ---------------------------------------------------------------------------
// Per PRD A3 (Visual & Camera Constraints):
//   - Fixed or gently-following isometric camera.
//   - No free third-person orbit, no behind-the-ship chase cam.
//   - Standard isometric: ~45 degree Y rotation, ~30 degree X pitch.
//
// S3.2 additions:
//   - Combat zoom: auto-zoom out when enemies are nearby.
//   - Screen boundaries: clamp camera to the ocean extent so it never
//     shows empty space outside the world.
//   - Velocity lead: camera anticipates movement direction.
//   - Public SetCombatMode() for external override (e.g., boarding).
// ---------------------------------------------------------------------------

using UnityEngine;
using Booty.Ships; // EnemyAI detection for combat zoom

namespace Booty.World
{
    /// <summary>
    /// Isometric follow camera with smooth follow, combat auto-zoom, and
    /// world-boundary clamping.
    /// </summary>
    public class IsometricCamera : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Tuning (Inspector)
        // ══════════════════════════════════════════════════════════════════

        [Header("Isometric Angles")]
        [Tooltip("Y-axis rotation in degrees (standard isometric = 45).")]
        [SerializeField] private float yawAngle   = 45f;

        [Tooltip("X-axis pitch in degrees (standard isometric = 30).")]
        [SerializeField] private float pitchAngle = 30f;

        [Header("Follow")]
        [Tooltip("Distance from the target along the camera's backward axis.")]
        [SerializeField] private float followDistance = 40f;

        [Tooltip("Height offset above the target.")]
        [SerializeField] private float heightOffset = 30f;

        [Tooltip("Smooth follow speed (higher = snappier).")]
        [SerializeField] private float smoothSpeed = 5f;

        [Tooltip("How far ahead of the ship the camera leads (world units). 0 = no lead.")]
        [SerializeField] [Range(0f, 10f)] private float velocityLeadAmount = 4f;

        [Tooltip("Smoothing time for the velocity lead offset.")]
        [SerializeField] [Range(0.05f, 1f)] private float leadSmoothTime = 0.35f;

        [Header("Zoom")]
        [Tooltip("Minimum orthographic size.")]
        [SerializeField] private float zoomMin = 10f;

        [Tooltip("Maximum orthographic size.")]
        [SerializeField] private float zoomMax = 40f;

        [Tooltip("Zoom speed per scroll tick.")]
        [SerializeField] private float zoomSpeed = 2f;

        [Header("Combat Zoom (S3.2)")]
        [Tooltip("Orthographic size to use during combat (enemy within radius).")]
        [SerializeField] private float combatZoomSize = 32f;

        [Tooltip("Smoothing speed for auto combat zoom transitions.")]
        [SerializeField] private float combatZoomSpeed = 2f;

        [Tooltip("Radius within which an enemy ship triggers combat mode.")]
        [SerializeField] private float combatDetectRadius = 35f;

        [Tooltip("Seconds with no enemies nearby before reverting to normal zoom.")]
        [SerializeField] private float combatExitDelay = 3f;

        [Header("World Boundaries (S3.2)")]
        [Tooltip("Clamp camera so it never leaves the world. Set to half the ocean size.")]
        [SerializeField] private float worldHalfExtent = 250f;

        [Tooltip("Enable world boundary clamping.")]
        [SerializeField] private bool  clampToBounds = true;

        // ══════════════════════════════════════════════════════════════════
        //  Runtime State
        // ══════════════════════════════════════════════════════════════════

        private Transform _target;
        private Camera    _cam;
        private Vector3   _followVelocity;     // used by SmoothDamp for position
        private Vector3   _leadOffset;         // current lead offset
        private Vector3   _leadOffsetVelocity; // SmoothDamp velocity for lead

        // Combat zoom state
        private bool  _combatModeActive;
        private bool  _externalCombatOverride; // set via SetCombatMode()
        private float _noEnemyTimer;           // how long since last enemy nearby
        private float _currentZoomSize;        // current ortho size target

        // Previous target position for velocity approximation
        private Vector3 _prevTargetPos;

        // ══════════════════════════════════════════════════════════════════
        //  Public API
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Set the follow target. Called by BootyBootstrap during wiring.
        /// </summary>
        public void Initialize(Transform target)
        {
            _target = target;

            // Apply the fixed isometric rotation immediately
            transform.rotation = Quaternion.Euler(pitchAngle, yawAngle, 0f);

            // Snap to target on first frame
            if (_target != null)
            {
                _prevTargetPos   = _target.position;
                transform.position = ComputeDesiredPosition(_target.position, Vector3.zero);
                _currentZoomSize   = _cam != null ? _cam.orthographicSize : zoomMax;
            }

            // Add CameraShake for combat feedback
            if (GetComponent<CameraShake>() == null)
                gameObject.AddComponent<CameraShake>();

            // Pull world extent from scene OceanPlane if available
#if UNITY_2023_1_OR_NEWER
            var ocean = FindFirstObjectByType<OceanPlane>();
#else
            var ocean = FindObjectOfType<OceanPlane>();
#endif
            if (ocean != null)
                worldHalfExtent = ocean.GetWorldExtent();
        }

        /// <summary>
        /// Manually enable or disable combat zoom mode.
        /// When overridden, auto-detection is suppressed.
        /// </summary>
        public void SetCombatMode(bool active)
        {
            _externalCombatOverride = true;
            _combatModeActive       = active;
        }

        /// <summary>
        /// Release the external combat-mode override so auto-detection resumes.
        /// </summary>
        public void ClearCombatModeOverride()
        {
            _externalCombatOverride = false;
        }

        /// <summary>Current normalised time of day (0=midnight, 0.5=noon).</summary>
        public bool IsInCombatMode => _combatModeActive;

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Awake()
        {
            _cam = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            float dt = Time.deltaTime;

            // ── 1. Velocity-lead calculation ────────────────────────────
            Vector3 targetVelocity = (_target.position - _prevTargetPos) / Mathf.Max(dt, 0.001f);
            _prevTargetPos = _target.position;

            // Flatten to XZ, scale by lead amount
            Vector3 desiredLead = new Vector3(targetVelocity.x, 0f, targetVelocity.z);
            desiredLead = Vector3.ClampMagnitude(desiredLead, 12f); // cap max lead
            desiredLead *= velocityLeadAmount * 0.1f;               // scale down

            _leadOffset = Vector3.SmoothDamp(
                _leadOffset, desiredLead,
                ref _leadOffsetVelocity, leadSmoothTime
            );

            // ── 2. Smooth camera follow ─────────────────────────────────
            Vector3 desired = ComputeDesiredPosition(_target.position + _leadOffset, _leadOffset);
            Vector3 clamped = clampToBounds ? ClampToWorldBounds(desired) : desired;

            transform.position = Vector3.SmoothDamp(
                transform.position, clamped,
                ref _followVelocity, 1f / smoothSpeed
            );

            // Maintain fixed rotation (never drift)
            transform.rotation = Quaternion.Euler(pitchAngle, yawAngle, 0f);

            // ── 3. Combat zoom auto-detection ───────────────────────────
            if (!_externalCombatOverride)
                UpdateCombatZoom(dt);

            // ── 4. Manual scroll zoom override ─────────────────────────
            if (_cam != null && _cam.orthographic)
            {
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(scroll) > 0.001f)
                {
                    // Manual scroll overrides auto zoom temporarily
                    _currentZoomSize -= scroll * zoomSpeed;
                    _currentZoomSize  = Mathf.Clamp(_currentZoomSize, zoomMin, zoomMax);
                }

                // Smoothly ease toward current target zoom size
                _cam.orthographicSize = Mathf.MoveTowards(
                    _cam.orthographicSize,
                    Mathf.Clamp(_currentZoomSize, zoomMin, zoomMax),
                    combatZoomSpeed * dt
                );
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Combat Zoom
        // ══════════════════════════════════════════════════════════════════

        private void UpdateCombatZoom(float dt)
        {
            if (_target == null) return;

            // Check for nearby enemies using Physics.OverlapSphere
            bool enemyNearby = false;
            var colliders = Physics.OverlapSphere(_target.position, combatDetectRadius);
            foreach (var col in colliders)
            {
                if (col.gameObject == _target.gameObject) continue;
                // Any tagged "Enemy" or has EnemyAI counts
                if (col.CompareTag("Enemy") || col.GetComponent<Booty.Ships.EnemyAI>() != null)
                {
                    enemyNearby = true;
                    break;
                }
            }

            if (enemyNearby)
            {
                _noEnemyTimer    = 0f;
                _combatModeActive = true;
                _currentZoomSize  = combatZoomSize;
            }
            else
            {
                _noEnemyTimer += dt;
                if (_noEnemyTimer >= combatExitDelay)
                {
                    _combatModeActive = false;
                    _currentZoomSize  = zoomMax;
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Helpers
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Compute where the camera should be based on target position,
        /// isometric angles, and offset distances.
        /// </summary>
        private Vector3 ComputeDesiredPosition(Vector3 targetPos, Vector3 /*leadOffset*/ _)
        {
            // Build an offset vector that respects the isometric angles
            Quaternion rotation = Quaternion.Euler(pitchAngle, yawAngle, 0f);
            Vector3    offset   = rotation * (Vector3.back * followDistance);
            offset.y = heightOffset;
            return targetPos + offset;
        }

        /// <summary>
        /// Clamp camera position so it stays within the ocean world extent.
        /// </summary>
        private Vector3 ClampToWorldBounds(Vector3 pos)
        {
            float margin = worldHalfExtent - 5f; // small inset
            pos.x = Mathf.Clamp(pos.x, -margin, margin);
            pos.z = Mathf.Clamp(pos.z, -margin, margin);
            return pos;
        }
    }
}
