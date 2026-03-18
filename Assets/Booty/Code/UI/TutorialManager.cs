// ---------------------------------------------------------------------------
// TutorialManager.cs — Contextual onboarding hints (first play only)
// ---------------------------------------------------------------------------
// Shows 4 sequential hints triggered by in-game milestones:
//   1. "WASD to sail"            — on spawn, hides after 5s or first movement
//   2. "Q/E to fire broadsides"  — when first enemy appears in scene
//   3. "Sail to a port..."       — after first enemy kill (NotifyEnemyKilled)
//   4. "Press ESC to pause..."   — after 60 seconds of play time
// Uses PlayerPrefs("tutorialSeen") so hints only show on first play ever.
// ---------------------------------------------------------------------------

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Booty.Ships;

namespace Booty.UI
{
    /// <summary>
    /// Contextual onboarding tutorial. Builds its own Canvas in Awake().
    /// BootyBootstrap wires NotifyEnemyKilled() to the first enemy death.
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  UI References
        // ══════════════════════════════════════════════════════════════════

        private GameObject _hintPanel;
        private Text       _hintText;
        private Coroutine  _hideCoroutine;

        // ══════════════════════════════════════════════════════════════════
        //  Hint State
        // ══════════════════════════════════════════════════════════════════

        private bool  _hint1Shown          = false;  // WASD to sail
        private bool  _hint2Shown          = false;  // Q/E to fire broadsides
        private bool  _hint3Shown          = false;  // sail to port + Enter
        private bool  _hint4Shown          = false;  // ESC to pause
        private float _playTime            = 0f;
        private bool  _enemyKillReceived   = false;

        // ══════════════════════════════════════════════════════════════════
        //  Constants
        // ══════════════════════════════════════════════════════════════════

        private const string PrefKey          = "tutorialSeen";
        private const float  Hint1Duration    = 5f;
        private const float  Hint2Duration    = 6f;
        private const float  Hint3Duration    = 7f;
        private const float  Hint4Duration    = 6f;
        private const float  Hint4PlaySeconds = 60f;

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Awake()
        {
            // Already completed tutorial — disable immediately
            if (PlayerPrefs.GetInt(PrefKey, 0) == 1)
            {
                enabled = false;
                return;
            }
            BuildUI();
        }

        private void Start()
        {
            if (!enabled) return;

            // Hint 1: WASD to sail — show immediately on first frame
            if (!_hint1Shown)
            {
                _hint1Shown = true;
                ShowHint("WASD to sail", Hint1Duration);
            }
        }

        private void Update()
        {
            if (!enabled) return;
            _playTime += Time.deltaTime;

            // ── Hint 1 early dismiss on first movement ────────────────────
            if (_hint1Shown && !_hint2Shown)
            {
                if (Mathf.Abs(Input.GetAxis("Horizontal")) > 0.05f ||
                    Mathf.Abs(Input.GetAxis("Vertical"))   > 0.05f)
                {
                    HideHintNow();
                }
            }

            // ── Hint 2: Q/E to fire — when first enemy appears ────────────
            if (_hint1Shown && !_hint2Shown)
            {
                var enemy = FindObjectOfType<EnemyAI>();
                if (enemy != null)
                {
                    _hint2Shown = true;
                    ShowHint("Q/E to fire broadsides", Hint2Duration);
                }
            }

            // ── Hint 3: sail to port — after first kill ───────────────────
            if (_hint2Shown && !_hint3Shown && _enemyKillReceived)
            {
                _hint3Shown = true;
                ShowHint("Sail to a port and press Enter", Hint3Duration);
            }

            // ── Hint 4: ESC to pause — after 60 seconds ──────────────────
            if (_hint3Shown && !_hint4Shown && _playTime >= Hint4PlaySeconds)
            {
                _hint4Shown = true;
                ShowHint("Press ESC to pause and save", Hint4Duration);
            }

            // ── Tutorial complete — mark in PlayerPrefs and self-disable ──
            if (_hint1Shown && _hint2Shown && _hint3Shown && _hint4Shown)
            {
                PlayerPrefs.SetInt(PrefKey, 1);
                PlayerPrefs.Save();
                enabled = false;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Public API (called by BootyBootstrap)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Called by BootyBootstrap when any enemy is destroyed.
        /// Triggers the "Sail to a port" hint on first kill.
        /// </summary>
        public void NotifyEnemyKilled()
        {
            _enemyKillReceived = true;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Hint Display
        // ══════════════════════════════════════════════════════════════════

        private void ShowHint(string message, float duration)
        {
            if (_hintPanel == null) return;
            if (_hideCoroutine != null) StopCoroutine(_hideCoroutine);
            _hintText.text = message;
            _hintPanel.SetActive(true);
            _hideCoroutine = StartCoroutine(HideAfter(duration));
        }

        private void HideHintNow()
        {
            if (_hideCoroutine != null) StopCoroutine(_hideCoroutine);
            if (_hintPanel != null) _hintPanel.SetActive(false);
        }

        private System.Collections.IEnumerator HideAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (_hintPanel != null) _hintPanel.SetActive(false);
        }

        // ══════════════════════════════════════════════════════════════════
        //  UI Construction
        // ══════════════════════════════════════════════════════════════════

        private void BuildUI()
        {
            // Canvas (below HUD at 10, above nothing)
            var canvasGO = new GameObject("Tutorial_Canvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // Hint panel — centred horizontally, slightly below vertical centre
            _hintPanel = new GameObject("HintPanel");
            _hintPanel.transform.SetParent(canvasGO.transform, false);
            var rt = _hintPanel.AddComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -80f);
            rt.sizeDelta        = new Vector2(440f, 70f);
            _hintPanel.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.60f);

            // Hint text — white, bold, centred
            var textGO = new GameObject("HintText");
            textGO.transform.SetParent(_hintPanel.transform, false);
            var tRT = textGO.AddComponent<RectTransform>();
            tRT.anchorMin = Vector2.zero;
            tRT.anchorMax = Vector2.one;
            tRT.offsetMin = new Vector2(12f,  8f);
            tRT.offsetMax = new Vector2(-12f, -8f);
            _hintText = textGO.AddComponent<Text>();
            _hintText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _hintText.fontSize  = 18;
            _hintText.fontStyle = FontStyle.Bold;
            _hintText.color     = Color.white;
            _hintText.alignment = TextAnchor.MiddleCenter;
            _hintText.text      = "";

            // Start hidden
            _hintPanel.SetActive(false);
        }
    }
}
