// ---------------------------------------------------------------------------
// IsometricCamera.cs — Fixed isometric follow camera
// ---------------------------------------------------------------------------
// Per PRD A3 (Visual & Camera Constraints):
//   - Fixed or gently-following isometric camera.
//   - No free third-person orbit, no behind-the-ship chase cam.
//   - Standard isometric: ~45 degree Y rotation, ~30 degree X pitch.
//
// The camera uses an orthographic projection and follows the player ship
// with smooth damping. Zoom is constrained to a small range.
// ---------------------------------------------------------------------------

using UnityEngine;

namespace Booty.World
{
    /// <summary>
    /// Isometric follow camera. Maintains a fixed rotation and smoothly
    /// tracks the target (player ship) on the XZ plane.
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

        [Header("Zoom")]
        [Tooltip("Minimum orthographic size.")]
        [SerializeField] private float zoomMin = 10f;

        [Tooltip("Maximum orthographic size.")]
        [SerializeField] private float zoomMax = 40f;

        [Tooltip("Zoom speed per scroll tick.")]
        [SerializeField] private float zoomSpeed = 2f;

        // ══════════════════════════════════════════════════════════════════
        //  Runtime State
        // ══════════════════════════════════════════════════════════════════

        private Transform _target;
        private Camera _cam;
        private Vector3 _velocity; // used by SmoothDamp

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
                transform.position = ComputeDesiredPosition();
            }

            // Add CameraShake for combat feedback
            if (GetComponent<CameraShake>() == null)
                gameObject.AddComponent<CameraShake>();
        }

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

            // ── Smooth follow ───────────────────────────────────────────
            Vector3 desired = ComputeDesiredPosition();
            transform.position = Vector3.SmoothDamp(
                transform.position,
                desired,
                ref _velocity,
                1f / smoothSpeed
            );

            // Maintain fixed rotation (never drift)
            transform.rotation = Quaternion.Euler(pitchAngle, yawAngle, 0f);

            // ── Zoom via scroll wheel ───────────────────────────────────
            if (_cam != null && _cam.orthographic)
            {
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(scroll) > 0.001f)
                {
                    _cam.orthographicSize -= scroll * zoomSpeed;
                    _cam.orthographicSize = Mathf.Clamp(_cam.orthographicSize, zoomMin, zoomMax);
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
        private Vector3 ComputeDesiredPosition()
        {
            // Build an offset vector that respects the isometric angles
            Quaternion rotation = Quaternion.Euler(pitchAngle, yawAngle, 0f);
            Vector3 offset = rotation * (Vector3.back * followDistance);
            offset.y = heightOffset;

            return _target.position + offset;
        }
    }
}
