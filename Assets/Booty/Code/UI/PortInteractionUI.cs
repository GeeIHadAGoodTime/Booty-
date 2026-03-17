// ---------------------------------------------------------------------------
// PortInteractionUI.cs — Proximity-triggered port action panel.
// ---------------------------------------------------------------------------
// Shows "Capture" and "Repair" buttons when the player is near a port.
// Called by PortInteraction.cs (the per-port proximity detector) via
// FindObjectOfType<PortInteractionUI>().
//
// NOT a singleton — no static Instance. Callers use FindObjectOfType<>.
// ---------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.UI;
using Booty.Ports;
using Booty.Economy;

namespace Booty.UI
{
    /// <summary>
    /// Screen-space port interaction panel. Shows Capture / Repair buttons
    /// when the player ship is within proximity of a port.
    /// Call <see cref="ShowPortPanel"/> and <see cref="HidePortPanel"/> from
    /// the per-port PortInteraction component via FindObjectOfType.
    /// </summary>
    public class PortInteractionUI : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  UI References (built in Awake)
        // ══════════════════════════════════════════════════════════════════

        private GameObject _panel;
        private Text       _headerText;
        private Button     _captureButton;
        private Button     _repairButton;
        private Text       _captureLabel;
        private Text       _repairLabel;

        // ══════════════════════════════════════════════════════════════════
        //  State
        // ══════════════════════════════════════════════════════════════════

        private string _currentPortId = "";

        // ══════════════════════════════════════════════════════════════════
        //  Layout constants
        // ══════════════════════════════════════════════════════════════════

        private const float PanelWidth   = 300f;
        private const float PanelHeight  = 120f;
        private const float ButtonWidth  = 120f;
        private const float ButtonHeight = 36f;

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Awake()
        {
            BuildHierarchy();
            HidePortPanel();  // start hidden
        }

        // ══════════════════════════════════════════════════════════════════
        //  Public API
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Show the port interaction panel with the relevant buttons enabled
        /// according to the interaction options available at this port.
        /// </summary>
        /// <param name="portId">Identifier of the nearby port.</param>
        /// <param name="canCapture">Whether the Capture button should be active.</param>
        /// <param name="canRepair">Whether the Repair button should be active.</param>
        public void ShowPortPanel(string portId, bool canCapture, bool canRepair)
        {
            _currentPortId = portId;
            _headerText.text = string.IsNullOrEmpty(portId) ? "Port" : portId;

            // Enable / disable buttons based on available actions
            SetButtonInteractable(_captureButton, _captureLabel, canCapture);
            SetButtonInteractable(_repairButton,  _repairLabel,  canRepair);

            _panel.SetActive(true);
        }

        /// <summary>
        /// Hide the port interaction panel.
        /// </summary>
        public void HidePortPanel()
        {
            _currentPortId = "";
            _panel.SetActive(false);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Button callbacks
        // ══════════════════════════════════════════════════════════════════

        private void OnCaptureClicked()
        {
            if (string.IsNullOrEmpty(_currentPortId)) return;

            var portSystem = FindObjectOfType<PortSystem>();
            if (portSystem == null)
            {
                UnityEngine.Debug.LogWarning("[PortInteractionUI] PortSystem not found.");
                return;
            }

            bool success = portSystem.CapturePort(_currentPortId);
            UnityEngine.Debug.Log("[PortInteractionUI] CapturePort(" + _currentPortId + "): " + success);

            // Disable the capture button after a successful capture
            if (success)
            {
                SetButtonInteractable(_captureButton, _captureLabel, false);
            }
        }

        private void OnRepairClicked()
        {
            var repairShop = FindObjectOfType<RepairShop>();
            if (repairShop == null)
            {
                UnityEngine.Debug.LogWarning("[PortInteractionUI] RepairShop not found.");
                return;
            }

            bool success = repairShop.RepairShip();
            UnityEngine.Debug.Log("[PortInteractionUI] RepairShip(): " + success);
        }

        // ══════════════════════════════════════════════════════════════════
        //  UI Construction
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds the Canvas → Panel → Header + two Buttons hierarchy at runtime.
        /// Panel is anchored to the bottom-centre of the screen.
        /// </summary>
        private void BuildHierarchy()
        {
            // ── Canvas ──────────────────────────────────────────────────
            GameObject canvasGO = new GameObject("PortUI_Canvas");
            canvasGO.transform.SetParent(transform, false);

            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;  // above HUD

            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // ── Panel ────────────────────────────────────────────────────
            _panel = new GameObject("PortInteraction_Panel");
            _panel.transform.SetParent(canvasGO.transform, false);

            RectTransform panelRect = _panel.AddComponent<RectTransform>();
            // Anchor to bottom-centre
            panelRect.anchorMin        = new Vector2(0.5f, 0f);
            panelRect.anchorMax        = new Vector2(0.5f, 0f);
            panelRect.pivot            = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 24f);   // 24px above screen bottom
            panelRect.sizeDelta        = new Vector2(PanelWidth, PanelHeight);

            Image panelBg = _panel.AddComponent<Image>();
            panelBg.color = new Color(0.08f, 0.08f, 0.12f, 0.90f);

            // ── Header ───────────────────────────────────────────────────
            _headerText = CreateText(_panel, "Port_Header", "Port",
                new Vector2(0f, PanelHeight - 2f),
                new Vector2(PanelWidth, 28f),
                new Vector2(0f, 1f), 16, FontStyle.Bold);

            // ── Capture button ───────────────────────────────────────────
            float buttonY = 14f;                            // from panel bottom
            float leftX   = -(ButtonWidth / 2f) - 10f;
            float rightX  =  (ButtonWidth / 2f) + 10f;

            _captureButton = CreateButton(_panel, "Capture_Button", "Capture",
                new Vector2(leftX, buttonY), out _captureLabel);
            _captureButton.onClick.AddListener(OnCaptureClicked);

            // ── Repair button ────────────────────────────────────────────
            _repairButton = CreateButton(_panel, "Repair_Button", "Repair",
                new Vector2(rightX, buttonY), out _repairLabel);
            _repairButton.onClick.AddListener(OnRepairClicked);
        }

        /// <summary>Creates a Text element as a child of parent.</summary>
        private Text CreateText(
            GameObject parent,
            string name,
            string defaultText,
            Vector2 anchoredPos,
            Vector2 sizeDelta,
            Vector2 pivot,
            int fontSize,
            FontStyle style)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, 0f);
            rt.anchorMax        = new Vector2(1f, 0f);
            rt.pivot            = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta        = new Vector2(0f, sizeDelta.y);   // stretch width

            Text t = go.AddComponent<Text>();
            t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize  = fontSize;
            t.fontStyle = style;
            t.color     = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.text      = defaultText;

            Shadow shadow = go.AddComponent<Shadow>();
            shadow.effectColor    = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(1f, -1f);

            return t;
        }

        /// <summary>
        /// Creates a standard Button child with a label Text component.
        /// The button is anchored to the bottom-centre of the parent, offset by anchoredPos.
        /// </summary>
        private Button CreateButton(
            GameObject parent,
            string name,
            string label,
            Vector2 anchoredPos,
            out Text labelText)
        {
            // Button background GameObject
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0f);
            rt.anchorMax        = new Vector2(0.5f, 0f);
            rt.pivot            = new Vector2(0.5f, 0f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta        = new Vector2(ButtonWidth, ButtonHeight);

            Image bg = go.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.4f, 0.25f, 1f);   // muted green

            Button btn = go.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor      = new Color(0.20f, 0.40f, 0.25f);
            cb.highlightedColor = new Color(0.30f, 0.55f, 0.35f);
            cb.pressedColor     = new Color(0.12f, 0.28f, 0.18f);
            cb.disabledColor    = new Color(0.25f, 0.25f, 0.25f, 0.6f);
            btn.colors = cb;

            // Label text
            GameObject labelGO = new GameObject(name + "_Label");
            labelGO.transform.SetParent(go.transform, false);

            RectTransform labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;

            labelText = labelGO.AddComponent<Text>();
            labelText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize  = 15;
            labelText.fontStyle = FontStyle.Bold;
            labelText.color     = Color.white;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.text      = label;

            return btn;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Helpers
        // ══════════════════════════════════════════════════════════════════

        private static void SetButtonInteractable(Button btn, Text label, bool interactable)
        {
            btn.interactable = interactable;
            if (label != null)
            {
                label.color = interactable
                    ? Color.white
                    : new Color(0.55f, 0.55f, 0.55f);
            }
        }
    }
}
