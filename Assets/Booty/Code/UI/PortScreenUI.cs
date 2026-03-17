// ---------------------------------------------------------------------------
// PortScreenUI.cs — Full-screen port menu (Repair / Trade / Recruit / Upgrade)
// ---------------------------------------------------------------------------
// ShowPortScreen(portId, justCaptured) opens the overlay.
// HidePortScreen() / IsOpen let BootyBootstrap and PauseMenuUI coordinate.
//
// Repair tab is functional: queries RepairShop for cost/HP, deducts gold.
// Trade / Recruit / Upgrade tabs show "Coming in Full Alpha" placeholder.
// If justCaptured == true, a victory banner is shown for 3 seconds.
// ---------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.UI;
using Booty.Ports;
using Booty.Economy;

namespace Booty.UI
{
    /// <summary>
    /// Full-screen port management overlay. Tabs: Repair (live), Trade / Recruit /
    /// Upgrade (placeholder). Triggered by BootyBootstrap on port capture or
    /// by PortInteraction on player proximity + Enter press.
    /// </summary>
    public class PortScreenUI : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  State
        // ══════════════════════════════════════════════════════════════════

        private string _currentPortId;
        private bool   _isOpen;

        /// <summary>True while the overlay is visible.</summary>
        public bool IsOpen => _isOpen;

        // ══════════════════════════════════════════════════════════════════
        //  UI References
        // ══════════════════════════════════════════════════════════════════

        private GameObject _root;

        // Header
        private Text _portNameText;
        private Text _victoryBannerText;
        private float _victoryTimer;

        // Tabs
        private Button _tabRepair;
        private Button _tabTrade;
        private Button _tabRecruit;
        private Button _tabUpgrade;
        private Text   _tabRepairLabel;
        private Text   _tabTradeLabel;
        private Text   _tabRecruitLabel;
        private Text   _tabUpgradeLabel;

        // Content panels
        private GameObject _repairPanel;
        private GameObject _comingSoonPanel;

        // Repair content
        private Text   _repairHpText;
        private Text   _repairCostText;
        private Button _repairButton;
        private Text   _repairButtonLabel;
        private Text   _repairFeedbackText;

        // ══════════════════════════════════════════════════════════════════
        //  Palette
        // ══════════════════════════════════════════════════════════════════

        private static readonly Color GoldColor   = new Color(1f,    0.85f, 0.2f);
        private static readonly Color GoldDim     = new Color(0.8f,  0.65f, 0.1f, 0.6f);
        private static readonly Color PanelBg     = new Color(0.04f, 0.07f, 0.14f, 0.97f);
        private static readonly Color BtnNormal   = new Color(0.10f, 0.15f, 0.25f, 0.92f);
        private static readonly Color BtnHover    = new Color(0.20f, 0.30f, 0.45f, 0.98f);
        private static readonly Color BtnPress    = new Color(0.06f, 0.10f, 0.18f, 1f);
        private static readonly Color TabActive   = new Color(0.18f, 0.28f, 0.45f, 1f);
        private static readonly Color TabInactive = new Color(0.08f, 0.12f, 0.22f, 0.92f);
        private static readonly Color VictoryGold = new Color(1f,    0.90f, 0.3f);

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Awake()
        {
            BuildUI();
            HidePortScreen();
        }

        private void Update()
        {
            if (!_isOpen) return;

            // Victory banner auto-dismiss
            if (_victoryTimer > 0f)
            {
                _victoryTimer -= Time.unscaledDeltaTime;
                if (_victoryTimer <= 0f && _victoryBannerText != null)
                    _victoryBannerText.gameObject.SetActive(false);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Public API
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Open the port screen for the given port. If justCaptured is true,
        /// a "PORT CAPTURED!" victory banner is shown for 3 seconds.
        /// </summary>
        public void ShowPortScreen(string portId, bool justCaptured = false)
        {
            _currentPortId = portId;

            // Header
            _portNameText.text = string.IsNullOrEmpty(portId)
                ? "PORT"
                : portId.Replace("_", " ").ToUpper();

            // Victory banner
            if (justCaptured && _victoryBannerText != null)
            {
                _victoryBannerText.gameObject.SetActive(true);
                _victoryTimer = 3f;
            }
            else if (_victoryBannerText != null)
            {
                _victoryBannerText.gameObject.SetActive(false);
            }

            _root.SetActive(true);
            _isOpen = true;

            // Default to Repair tab
            ShowTab("repair");
        }

        /// <summary>Close the port screen overlay.</summary>
        public void HidePortScreen()
        {
            _isOpen = false;
            if (_root != null) _root.SetActive(false);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Tab Switching
        // ══════════════════════════════════════════════════════════════════

        private void ShowTab(string tab)
        {
            bool isRepair = (tab == "repair");

            // Toggle panels
            if (_repairPanel    != null) _repairPanel.SetActive(isRepair);
            if (_comingSoonPanel != null) _comingSoonPanel.SetActive(!isRepair);

            // Tab highlight
            SetTabActive(_tabRepair,  _tabRepairLabel,  isRepair);
            SetTabActive(_tabTrade,   _tabTradeLabel,   tab == "trade");
            SetTabActive(_tabRecruit, _tabRecruitLabel, tab == "recruit");
            SetTabActive(_tabUpgrade, _tabUpgradeLabel, tab == "upgrade");

            if (isRepair) RefreshRepairPanel();
        }

        private static void SetTabActive(Button btn, Text label, bool active)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null) img.color = active ? TabActive : TabInactive;
            if (label != null) label.color = active ? GoldColor : new Color(0.7f, 0.7f, 0.7f);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Repair Tab Logic
        // ══════════════════════════════════════════════════════════════════

        private void RefreshRepairPanel()
        {
            var repairShop = FindObjectOfType<RepairShop>();
            var economy    = FindObjectOfType<EconomySystem>();

            if (repairShop == null || _repairHpText == null) return;

            repairShop.GetShipHullStatus(out int currentHp, out int maxHp);
            float cost = repairShop.GetRepairCost();
            float gold = economy != null ? economy.Gold : 0f;

            _repairHpText.text  = string.Format("Hull: {0} / {1}", currentHp, maxHp);
            _repairCostText.text = string.Format("Repair cost: {0:F0} gold  (You have: {1:F0})",
                cost, gold);

            bool canRepair = (currentHp < maxHp) && (gold >= cost);
            if (_repairButton != null) _repairButton.interactable = canRepair;
            if (_repairButtonLabel != null)
                _repairButtonLabel.color = canRepair ? Color.white : new Color(0.5f, 0.5f, 0.5f);

            if (_repairFeedbackText != null) _repairFeedbackText.text = "";
        }

        private void OnRepairClicked()
        {
            var repairShop = FindObjectOfType<RepairShop>();
            if (repairShop == null) return;

            bool success = repairShop.RepairShip();

            if (_repairFeedbackText != null)
            {
                _repairFeedbackText.text  = success ? "Hull repaired!" : "Cannot repair.";
                _repairFeedbackText.color = success
                    ? new Color(0.3f, 1f, 0.4f)
                    : new Color(1f, 0.35f, 0.35f);
            }

            RefreshRepairPanel();
        }

        // ══════════════════════════════════════════════════════════════════
        //  UI Construction
        // ══════════════════════════════════════════════════════════════════

        private void BuildUI()
        {
            // ── Canvas (above PortInteractionUI at 20, below PauseMenu at 50) ──
            var canvasGO = new GameObject("PortScreen_Canvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // ── Dark full-screen overlay ──────────────────────────────────
            var overlayGO = new GameObject("PortScreen_Overlay");
            overlayGO.transform.SetParent(canvasGO.transform, false);
            var oRect = overlayGO.AddComponent<RectTransform>();
            oRect.anchorMin = Vector2.zero; oRect.anchorMax = Vector2.one;
            oRect.offsetMin = Vector2.zero; oRect.offsetMax = Vector2.zero;
            overlayGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.82f);
            _root = overlayGO;

            // ── Main panel ────────────────────────────────────────────────
            var panelGO = new GameObject("PortScreen_Panel");
            panelGO.transform.SetParent(overlayGO.transform, false);
            var pRect = panelGO.AddComponent<RectTransform>();
            pRect.anchorMin        = new Vector2(0.5f, 0.5f);
            pRect.anchorMax        = new Vector2(0.5f, 0.5f);
            pRect.pivot            = new Vector2(0.5f, 0.5f);
            pRect.anchoredPosition = Vector2.zero;
            pRect.sizeDelta        = new Vector2(540f, 420f);
            panelGO.AddComponent<Image>().color = PanelBg;
            AddOutline(panelGO, GoldDim);

            // ── Close (X) button — top-right corner ───────────────────────
            var closeBtn = MakeButton(panelGO, "X", new Vector2(246f, 182f),
                HidePortScreen, new Vector2(36f, 36f));

            // ── Port name header ──────────────────────────────────────────
            _portNameText = MakeText(panelGO, "PortName", "PORT",
                new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(460f, 40f),
                28, FontStyle.Bold, GoldColor, TextAnchor.MiddleCenter);
            AddShadow(_portNameText.gameObject);

            // ── Victory banner (hidden by default) ────────────────────────
            _victoryBannerText = MakeText(panelGO, "VictoryBanner", "⚓ PORT CAPTURED! ⚓",
                new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(460f, 32f),
                18, FontStyle.Bold, VictoryGold, TextAnchor.MiddleCenter);
            AddShadow(_victoryBannerText.gameObject);
            _victoryBannerText.gameObject.SetActive(false);

            // ── Gold divider line ─────────────────────────────────────────
            var divGO = new GameObject("Divider");
            divGO.transform.SetParent(panelGO.transform, false);
            var dRT = divGO.AddComponent<RectTransform>();
            dRT.anchorMin = new Vector2(0.5f, 1f); dRT.anchorMax = new Vector2(0.5f, 1f);
            dRT.pivot = new Vector2(0.5f, 0.5f);
            dRT.anchoredPosition = new Vector2(0f, -110f);
            dRT.sizeDelta = new Vector2(480f, 2f);
            divGO.AddComponent<Image>().color = GoldDim;

            // ── Tab buttons row ───────────────────────────────────────────
            float tabY    = -135f;
            float tabGap  = 120f;
            float tabStartX = -tabGap * 1.5f;

            _tabRepair  = MakeTabButton(panelGO, "REPAIR",   new Vector2(tabStartX + 0 * tabGap, tabY), out _tabRepairLabel);
            _tabTrade   = MakeTabButton(panelGO, "TRADE",    new Vector2(tabStartX + 1 * tabGap, tabY), out _tabTradeLabel);
            _tabRecruit = MakeTabButton(panelGO, "RECRUIT",  new Vector2(tabStartX + 2 * tabGap, tabY), out _tabRecruitLabel);
            _tabUpgrade = MakeTabButton(panelGO, "UPGRADE",  new Vector2(tabStartX + 3 * tabGap, tabY), out _tabUpgradeLabel);

            _tabRepair.onClick.AddListener(() => ShowTab("repair"));
            _tabTrade.onClick.AddListener(() => ShowTab("trade"));
            _tabRecruit.onClick.AddListener(() => ShowTab("recruit"));
            _tabUpgrade.onClick.AddListener(() => ShowTab("upgrade"));

            // ── Content area (below tabs) ─────────────────────────────────
            var contentGO = new GameObject("ContentArea");
            contentGO.transform.SetParent(panelGO.transform, false);
            var cRT = contentGO.AddComponent<RectTransform>();
            cRT.anchorMin = new Vector2(0.5f, 0.5f); cRT.anchorMax = new Vector2(0.5f, 0.5f);
            cRT.pivot = new Vector2(0.5f, 0.5f);
            cRT.anchoredPosition = new Vector2(0f, -30f);
            cRT.sizeDelta = new Vector2(500f, 220f);

            // ── Repair sub-panel ──────────────────────────────────────────
            _repairPanel = BuildRepairPanel(contentGO);

            // ── Coming Soon sub-panel ─────────────────────────────────────
            _comingSoonPanel = BuildComingSoonPanel(contentGO);
        }

        private GameObject BuildRepairPanel(GameObject parent)
        {
            var go = new GameObject("Repair_Panel");
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            _repairHpText = MakeText(go, "Repair_HP", "Hull: ? / ?",
                new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(440f, 32f),
                18, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);

            _repairCostText = MakeText(go, "Repair_Cost", "Repair cost: ...",
                new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(440f, 28f),
                15, FontStyle.Normal, new Color(0.85f, 0.85f, 0.85f), TextAnchor.MiddleCenter);

            _repairButton = MakeButton(go, "REPAIR SHIP", new Vector2(0f, -100f),
                OnRepairClicked, new Vector2(200f, 44f));

            // Grab the label reference from the button
            var labelGO = _repairButton.transform.Find("Label");
            if (labelGO != null) _repairButtonLabel = labelGO.GetComponent<Text>();

            _repairFeedbackText = MakeText(go, "Repair_Feedback", "",
                new Vector2(0.5f, 0.5f), new Vector2(0f, -140f), new Vector2(440f, 28f),
                14, FontStyle.Italic, new Color(0.3f, 1f, 0.4f), TextAnchor.MiddleCenter);

            return go;
        }

        private GameObject BuildComingSoonPanel(GameObject parent)
        {
            var go = new GameObject("ComingSoon_Panel");
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            MakeText(go, "ComingSoon_Title", "Coming in Full Alpha",
                new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(440f, 40f),
                22, FontStyle.Bold, new Color(0.6f, 0.6f, 0.6f), TextAnchor.MiddleCenter);

            MakeText(go, "ComingSoon_Desc", "This port feature is under construction.\nCheck back in the next update!",
                new Vector2(0.5f, 0.5f), new Vector2(0f, -24f), new Vector2(400f, 56f),
                14, FontStyle.Italic, new Color(0.5f, 0.5f, 0.5f), TextAnchor.MiddleCenter);

            return go;
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
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = content; t.fontSize = fontSize; t.fontStyle = style;
            t.color = color; t.alignment = align;
            return t;
        }

        private Button MakeButton(GameObject parent, string label, Vector2 pos,
            UnityEngine.Events.UnityAction onClick, Vector2 size)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            go.AddComponent<Image>().color = BtnNormal;
            var btn = go.AddComponent<Button>();
            var cb = btn.colors;
            cb.normalColor = BtnNormal; cb.highlightedColor = BtnHover;
            cb.pressedColor = BtnPress; cb.disabledColor = new Color(0.15f, 0.15f, 0.2f, 0.6f);
            btn.colors = cb;
            btn.onClick.AddListener(onClick);
            AddOutline(go, GoldDim);

            var lGO = new GameObject("Label");
            lGO.transform.SetParent(go.transform, false);
            var lRT = lGO.AddComponent<RectTransform>();
            lRT.anchorMin = Vector2.zero; lRT.anchorMax = Vector2.one;
            lRT.offsetMin = Vector2.zero; lRT.offsetMax = Vector2.zero;
            var txt = lGO.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.text = label; txt.fontSize = 15; txt.fontStyle = FontStyle.Bold;
            txt.color = Color.white; txt.alignment = TextAnchor.MiddleCenter;
            return btn;
        }

        private Button MakeTabButton(GameObject parent, string label, Vector2 pos,
            out Text labelText)
        {
            var go = new GameObject("Tab_" + label);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(110f, 34f);
            go.AddComponent<Image>().color = TabInactive;
            var btn = go.AddComponent<Button>();
            var cb = btn.colors;
            cb.normalColor = TabInactive; cb.highlightedColor = BtnHover;
            cb.pressedColor = BtnPress;
            btn.colors = cb;

            var lGO = new GameObject("Label");
            lGO.transform.SetParent(go.transform, false);
            var lRT = lGO.AddComponent<RectTransform>();
            lRT.anchorMin = Vector2.zero; lRT.anchorMax = Vector2.one;
            lRT.offsetMin = Vector2.zero; lRT.offsetMax = Vector2.zero;
            labelText = lGO.AddComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.text = label; labelText.fontSize = 13; labelText.fontStyle = FontStyle.Bold;
            labelText.color = new Color(0.7f, 0.7f, 0.7f); labelText.alignment = TextAnchor.MiddleCenter;
            return btn;
        }

        private static void AddOutline(GameObject go, Color color)
        {
            var o = go.AddComponent<Outline>();
            o.effectColor = color; o.effectDistance = new Vector2(1f, -1f);
        }

        private static void AddShadow(GameObject go)
        {
            var s = go.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, 0.8f);
            s.effectDistance = new Vector2(2f, -2f);
        }
    }
}
