// ---------------------------------------------------------------------------
// CapturePopup.cs — Full-screen port-capture celebration overlay
// ---------------------------------------------------------------------------
// Shown when the player captures a port. Displays port name, faction change,
// and gold reward. Auto-dismisses after 3 seconds (or on click).
// Also checks victory condition: if all ports are player-owned, shows a
// persistent VICTORY banner (click to dismiss).
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Booty.Ports;

namespace Booty.UI
{
    /// <summary>
    /// Full-screen port-capture celebration popup. Wired by BootyBootstrap into
    /// portSystem.OnPortCaptured. Instantiated at runtime — no prefab needed.
    /// </summary>
    public class CapturePopup : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  State
        // ══════════════════════════════════════════════════════════════════

        private bool  _isOpen;
        private float _dismissTimer;
        private bool  _victoryMode;   // True → persistent until click

        /// <summary>True while the overlay is visible.</summary>
        public bool IsOpen => _isOpen;

        // ══════════════════════════════════════════════════════════════════
        //  UI References
        // ══════════════════════════════════════════════════════════════════

        private GameObject _root;
        private Text       _headerText;
        private Text       _portNameText;
        private Text       _factionText;
        private Text       _goldText;
        private Text       _victoryText;
        private Text       _dismissHintText;

        // ══════════════════════════════════════════════════════════════════
        //  Palette
        // ══════════════════════════════════════════════════════════════════

        private static readonly Color GoldColor    = new Color(1f,    0.85f, 0.2f);
        private static readonly Color GoldBright   = new Color(1f,    0.92f, 0.4f);
        private static readonly Color GoldDim      = new Color(0.8f,  0.65f, 0.1f, 0.6f);
        private static readonly Color VictoryColor = new Color(1f,    0.95f, 0.3f);
        private static readonly Color PanelBg      = new Color(0.03f, 0.06f, 0.12f, 0.97f);

        private const float AutoDismissDuration = 3f;

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Awake()
        {
            BuildUI();
            _root.SetActive(false);
        }

        private void Update()
        {
            if (!_isOpen) return;

            // Click to dismiss (any mouse button)
            if (Input.GetMouseButtonDown(0))
            {
                Hide();
                return;
            }

            // Auto-dismiss after 3 seconds (non-victory only)
            if (!_victoryMode)
            {
                _dismissTimer -= Time.unscaledDeltaTime;
                if (_dismissTimer <= 0f)
                    Hide();
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Public API
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Show the capture celebration for the given port.
        /// Checks if all ports are now player-owned — if yes, enters victory mode.
        /// </summary>
        /// <param name="portId">Port that was captured.</param>
        /// <param name="previousFaction">Faction that previously owned the port.</param>
        /// <param name="goldReward">Gold rewarded on capture.</param>
        public void ShowCapture(string portId, string previousFaction, float goldReward)
        {
            // Populate header and details
            _headerText.text   = "PORT CAPTURED!";
            _portNameText.text = string.IsNullOrEmpty(portId)
                ? "PORT"
                : portId.Replace("_", " ").ToUpper();
            _factionText.text  = string.Format("{0}  \u2192  Player Pirates", previousFaction);
            _goldText.text     = string.Format("+ {0:F0} gold", goldReward);

            // Victory check: does player now own ALL ports?
            _victoryMode = CheckVictory();
            if (_victoryText != null)
                _victoryText.gameObject.SetActive(_victoryMode);
            if (_dismissHintText != null)
                _dismissHintText.text = _victoryMode
                    ? "Click to continue"
                    : "Click to dismiss (auto-close in 3s)";

            _dismissTimer = AutoDismissDuration;
            _root.SetActive(true);
            _isOpen = true;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Internal
        // ══════════════════════════════════════════════════════════════════

        private void Hide()
        {
            _isOpen = false;
            _root.SetActive(false);
        }

        /// <summary>
        /// Returns true if every registered port is owned by player_pirates.
        /// Gracefully returns false if PortSystem is not found.
        /// </summary>
        private static bool CheckVictory()
        {
            var portSystem = Object.FindObjectOfType<PortSystem>();
            if (portSystem == null) return false;

            IReadOnlyDictionary<string, PortRuntimeData> all = portSystem.GetAllPorts();
            if (all == null || all.Count == 0) return false;

            foreach (var kvp in all)
            {
                if (kvp.Value.factionOwner != "player_pirates")
                    return false;
            }
            return true;
        }

        // ══════════════════════════════════════════════════════════════════
        //  UI Construction
        // ══════════════════════════════════════════════════════════════════

        private void BuildUI()
        {
            // ── Canvas (sortingOrder=40, above PortScreenUI at 30) ────────
            var canvasGO = new GameObject("CapturePopup_Canvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // ── Full-screen dark overlay ──────────────────────────────────
            var overlayGO = new GameObject("CapturePopup_Overlay");
            overlayGO.transform.SetParent(canvasGO.transform, false);
            var oRect = overlayGO.AddComponent<RectTransform>();
            oRect.anchorMin = Vector2.zero; oRect.anchorMax = Vector2.one;
            oRect.offsetMin = Vector2.zero; oRect.offsetMax = Vector2.zero;
            overlayGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.88f);
            _root = overlayGO;

            // ── Centre panel ──────────────────────────────────────────────
            var panelGO = new GameObject("CapturePopup_Panel");
            panelGO.transform.SetParent(overlayGO.transform, false);
            var pRect = panelGO.AddComponent<RectTransform>();
            pRect.anchorMin = new Vector2(0.5f, 0.5f); pRect.anchorMax = new Vector2(0.5f, 0.5f);
            pRect.pivot = new Vector2(0.5f, 0.5f);
            pRect.anchoredPosition = Vector2.zero; pRect.sizeDelta = new Vector2(560f, 380f);
            panelGO.AddComponent<Image>().color = PanelBg;
            AddOutline(panelGO, GoldDim);

            // ── Gold divider (top accent) ─────────────────────────────────
            AddDivider(panelGO, new Vector2(0f, 152f), new Vector2(500f, 3f), GoldDim);

            // ── "PORT CAPTURED!" header ───────────────────────────────────
            _headerText = MakeText(panelGO, "Header", "PORT CAPTURED!",
                new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(520f, 60f),
                38, FontStyle.Bold, GoldBright, TextAnchor.MiddleCenter);
            AddShadow(_headerText.gameObject);

            // ── Port name ─────────────────────────────────────────────────
            _portNameText = MakeText(panelGO, "PortName", "PORT",
                new Vector2(0.5f, 0.5f), new Vector2(0f, 50f), new Vector2(500f, 52f),
                32, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);

            // ── Faction change ────────────────────────────────────────────
            _factionText = MakeText(panelGO, "Faction", "enemy fleet  \u2192  Player Pirates",
                new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(500f, 34f),
                18, FontStyle.Italic, new Color(0.85f, 0.85f, 0.85f),
                TextAnchor.MiddleCenter);

            // ── Gold reward ───────────────────────────────────────────────
            _goldText = MakeText(panelGO, "Gold", "+ 200 gold",
                new Vector2(0.5f, 0.5f), new Vector2(0f, -46f), new Vector2(500f, 38f),
                26, FontStyle.Bold, GoldColor, TextAnchor.MiddleCenter);

            // ── Gold divider (separator before victory) ───────────────────
            AddDivider(panelGO, new Vector2(0f, -88f), new Vector2(460f, 2f), GoldDim);

            // ── Victory banner (hidden until all ports captured) ──────────
            _victoryText = MakeText(panelGO, "Victory", "\u2693 VICTORY!  All ports captured! \u2693",
                new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(520f, 48f),
                28, FontStyle.Bold, VictoryColor, TextAnchor.MiddleCenter);
            AddShadow(_victoryText.gameObject);
            _victoryText.gameObject.SetActive(false);

            // ── Dismiss hint ──────────────────────────────────────────────
            _dismissHintText = MakeText(panelGO, "DismissHint", "Click to dismiss (auto-close in 3s)",
                new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(500f, 28f),
                13, FontStyle.Italic, new Color(0.55f, 0.55f, 0.55f),
                TextAnchor.MiddleCenter);
        }

        // ══════════════════════════════════════════════════════════════════
        //  UI Factories
        // ══════════════════════════════════════════════════════════════════

        private static Text MakeText(
            GameObject parent, string name, string content,
            Vector2 anchor, Vector2 pos, Vector2 size,
            int fontSize, FontStyle style, Color color, TextAnchor align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor; rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var t = go.AddComponent<Text>();
            t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text      = content;
            t.fontSize  = fontSize;
            t.fontStyle = style;
            t.color     = color;
            t.alignment = align;
            return t;
        }

        private static void AddDivider(GameObject parent, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject("Divider");
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            go.AddComponent<Image>().color = color;
        }

        private static void AddOutline(GameObject go, Color color)
        {
            var o = go.AddComponent<Outline>();
            o.effectColor    = color;
            o.effectDistance = new Vector2(1f, -1f);
        }

        private static void AddShadow(GameObject go)
        {
            var s = go.AddComponent<Shadow>();
            s.effectColor    = new Color(0f, 0f, 0f, 0.9f);
            s.effectDistance = new Vector2(2f, -2f);
        }
    }
}
