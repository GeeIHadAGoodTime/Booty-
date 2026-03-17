// ---------------------------------------------------------------------------
// ShipHPBar.cs — World-space HP bar displayed above each ship
// ---------------------------------------------------------------------------
// S3.1: Player feedback — HP bar on enemies (and player when damaged).
//
// Attach to any ship. Subscribes to HPSystem.OnDamaged and updates the bar
// fill width. Faces the camera every frame via LookAt.
//
// Layout (world-space canvas):
//   [dark background quad]
//   [coloured fill quad — width scales with HP ratio]
//
// The bar appears when the ship takes first damage, so pristine ships are
// not cluttered. Destroyed when the ship is destroyed.
// ---------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.UI;
using Booty.Combat;   // HPSystem

namespace Booty.UI
{
    /// <summary>
    /// World-space HP bar for a ship. Self-wires to HPSystem.OnDamaged.
    /// </summary>
    public class ShipHPBar : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Layout constants (world-space canvas units)
        // ══════════════════════════════════════════════════════════════════

        private const float BarWidth     = 3.5f;   // full bar width
        private const float BarHeight    = 0.35f;  // bar height
        private const float HeightOffset = 3.2f;   // metres above ship pivot

        // ══════════════════════════════════════════════════════════════════
        //  References
        // ══════════════════════════════════════════════════════════════════

        private HPSystem       _hp;
        private GameObject     _canvasGO;
        private RectTransform  _fillRT;
        private bool           _isPlayer;

        // ══════════════════════════════════════════════════════════════════
        //  Public API
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Wire HP system and set whether this bar belongs to the player
        /// (green) or an enemy (red). Call once from BootyBootstrap.
        /// </summary>
        public void Initialize(HPSystem hp, bool isPlayer)
        {
            _hp       = hp;
            _isPlayer = isPlayer;

            BuildBar();

            // Start hidden — show only after first hit
            _canvasGO.SetActive(false);

            if (_hp != null)
                _hp.OnDamaged += OnDamaged;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Start()
        {
            // Self-wire only when Initialize() was not called explicitly
            // (e.g., if AddComponent was used without following up with Initialize).
            if (_hp == null)
            {
                _hp       = GetComponent<HPSystem>();
                _isPlayer = CompareTag("Player");
                BuildBar();
                if (_canvasGO != null) _canvasGO.SetActive(false);
                if (_hp != null) _hp.OnDamaged += OnDamaged;
            }
        }

        private void OnDestroy()
        {
            if (_hp != null)
                _hp.OnDamaged -= OnDamaged;

            if (_canvasGO != null)
                Destroy(_canvasGO);
        }

        private void LateUpdate()
        {
            if (_canvasGO == null || !_canvasGO.activeSelf) return;

            // Keep bar above the ship
            _canvasGO.transform.position = transform.position + Vector3.up * HeightOffset;

            // Face the camera so the bar is always readable
            if (Camera.main != null)
            {
                _canvasGO.transform.LookAt(Camera.main.transform);
                _canvasGO.transform.rotation = Camera.main.transform.rotation;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Event Handler
        // ══════════════════════════════════════════════════════════════════

        private void OnDamaged(int currentHP, int maxHP)
        {
            if (_canvasGO != null)
                _canvasGO.SetActive(true);

            float ratio = maxHP > 0 ? (float)currentHP / maxHP : 0f;
            UpdateFill(ratio);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Bar Construction
        // ══════════════════════════════════════════════════════════════════

        private void BuildBar()
        {
            // World-space canvas — independent of ship hierarchy so it doesn't
            // scale with the ship's local scale or rotation.
            _canvasGO = new GameObject("HPBar");
            _canvasGO.transform.position = transform.position + Vector3.up * HeightOffset;

            var canvas = _canvasGO.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.WorldSpace;
            canvas.sortingOrder = 10;

            var rt = _canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(BarWidth, BarHeight);
            _canvasGO.transform.localScale = Vector3.one * 0.01f;  // scale to world units

            // ── Background ─────────────────────────────────────────────
            var bgGO  = new GameObject("Background");
            bgGO.transform.SetParent(_canvasGO.transform, false);

            var bgRect = bgGO.AddComponent<RectTransform>();
            bgRect.sizeDelta        = new Vector2(BarWidth * 100f, BarHeight * 100f);
            bgRect.anchoredPosition = Vector2.zero;

            var bgImage = bgGO.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.75f);

            // ── Fill ───────────────────────────────────────────────────
            var fillGO  = new GameObject("Fill");
            fillGO.transform.SetParent(_canvasGO.transform, false);

            _fillRT = fillGO.AddComponent<RectTransform>();
            _fillRT.sizeDelta        = new Vector2(BarWidth * 100f, BarHeight * 100f * 0.70f);
            _fillRT.anchoredPosition = Vector2.zero;
            _fillRT.pivot            = new Vector2(0f, 0.5f);   // pivot at left edge
            _fillRT.anchorMin        = new Vector2(0f, 0.5f);
            _fillRT.anchorMax        = new Vector2(0f, 0.5f);

            // Offset so fill starts at the left edge of the background
            _fillRT.anchoredPosition = new Vector2(-BarWidth * 50f, 0f);

            var fillImage = fillGO.AddComponent<Image>();
            fillImage.color = _isPlayer ? new Color(0.2f, 0.85f, 0.3f) : new Color(0.9f, 0.2f, 0.15f);
        }

        private void UpdateFill(float ratio)
        {
            if (_fillRT == null) return;

            // Scale the fill rect horizontally
            Vector2 size = _fillRT.sizeDelta;
            size.x = BarWidth * 100f * Mathf.Clamp01(ratio);
            _fillRT.sizeDelta = size;
        }
    }
}
