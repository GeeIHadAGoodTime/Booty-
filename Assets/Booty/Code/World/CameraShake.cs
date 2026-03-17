// ---------------------------------------------------------------------------
// CameraShake.cs — Screen shake on player hit
// ---------------------------------------------------------------------------
// Attached to the same GameObject as IsometricCamera (added by Initialize()).
// Self-wires to player HPSystem.OnDamaged via GameRoot.Instance.HPSystem in Start().
//
// EXECUTION ORDER NOTE:
//   [DefaultExecutionOrder(100)] ensures this LateUpdate() runs AFTER
//   IsometricCamera.LateUpdate() (default order 0). We add the shake offset
//   to the camera position AFTER IsometricCamera has computed and set the
//   follow position — if we used a coroutine or Update(), IsometricCamera's
//   LateUpdate would silently overwrite the offset every frame.
// ---------------------------------------------------------------------------

using UnityEngine;
using Booty.Bootstrap;

namespace Booty.World
{
    /// <summary>
    /// Applies a brief camera shake when the player ship takes damage.
    /// Attach to the IsometricCamera GameObject. Self-wires via GameRoot.
    /// Uses LateUpdate with [DefaultExecutionOrder(100)] so the shake offset
    /// is applied AFTER IsometricCamera sets the base follow position.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class CameraShake : MonoBehaviour
    {
        [Header("Shake Tuning")]
        [SerializeField] private float shakeDuration  = 0.25f;
        [SerializeField] private float shakeMagnitude = 0.4f;

        // Shake state — set on hit, consumed in LateUpdate
        private float _shakeElapsed    = float.MaxValue; // starts inactive
        private float _activeDuration  = 0f;
        private float _activeMagnitude = 0f;

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Start()
        {
            // Subscribe to player damage events
            var hp = GameRoot.Instance?.HPSystem;
            if (hp != null)
            {
                hp.OnDamaged += OnPlayerDamaged;
            }
        }

        private void OnDestroy()
        {
            var hp = GameRoot.Instance?.HPSystem;
            if (hp != null)
                hp.OnDamaged -= OnPlayerDamaged;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Event Handler
        // ══════════════════════════════════════════════════════════════════

        private void OnPlayerDamaged(int currentHP, int maxHP)
        {
            // Re-arm shake (restarts if hit while already shaking)
            _shakeElapsed    = 0f;
            _activeDuration  = shakeDuration;
            _activeMagnitude = shakeMagnitude;
        }

        // ══════════════════════════════════════════════════════════════════
        //  LateUpdate — runs AFTER IsometricCamera.LateUpdate (order 0)
        // ══════════════════════════════════════════════════════════════════

        private void LateUpdate()
        {
            if (_shakeElapsed >= _activeDuration) return;

            _shakeElapsed += Time.deltaTime;
            float t        = _shakeElapsed / _activeDuration;
            float strength = Mathf.Lerp(_activeMagnitude, 0f, t);

            // Add shake offset to whatever position IsometricCamera already set
            transform.position += (Vector3)Random.insideUnitCircle * strength;
        }
    }
}
