// ---------------------------------------------------------------------------
// DamagePopup.cs — Floating damage number popup using TextMesh (3D text)
// ---------------------------------------------------------------------------
// Spawned by HPSystem on hit. Rises and fades over 1.2 seconds.
// Red for player damage, yellow for enemy damage.
// ---------------------------------------------------------------------------

using System.Collections;
using UnityEngine;

namespace Booty.UI
{
    /// <summary>
    /// Floating damage number. Uses TextMesh (3D text, no TMP dependency).
    /// Rises 2.5 units/sec over 1.2 seconds, fades alpha, faces camera.
    /// </summary>
    public class DamagePopup : MonoBehaviour
    {
        private TextMesh _textMesh;
        private float    _elapsed;

        private const float Duration  = 1.2f;
        private const float RiseSpeed = 2.5f;

        // ══════════════════════════════════════════════════════════════════
        //  Public Factory
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Spawn a floating damage number at a world position.
        /// </summary>
        /// <param name="worldPosition">Where to spawn.</param>
        /// <param name="damage">Damage amount to display.</param>
        /// <param name="isPlayer">True = red (player took damage), false = yellow (enemy).</param>
        public static DamagePopup Spawn(Vector3 worldPosition, int damage, bool isPlayer)
        {
            var go = new GameObject("DamagePopup");
            go.transform.position = worldPosition + Vector3.up * 1.5f;

            var tm = go.AddComponent<TextMesh>();
            tm.text      = damage.ToString();
            tm.fontSize  = 48;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor    = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color     = isPlayer
                ? new Color(1f, 0.2f, 0.2f)   // red   — player is hurt
                : new Color(1f, 0.9f, 0.1f);  // yellow — enemy is hurt

            var popup = go.AddComponent<DamagePopup>();
            popup._textMesh = tm;
            return popup;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = _elapsed / Duration;

            // Rise
            transform.position += Vector3.up * RiseSpeed * Time.deltaTime;

            // Face camera
            if (Camera.main != null)
                transform.forward = Camera.main.transform.forward;

            // Fade alpha
            if (_textMesh != null)
            {
                Color c = _textMesh.color;
                c.a = 1f - t;
                _textMesh.color = c;
            }

            if (_elapsed >= Duration)
                Destroy(gameObject);
        }
    }
}
