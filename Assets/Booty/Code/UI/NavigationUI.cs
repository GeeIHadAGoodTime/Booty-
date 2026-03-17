// ---------------------------------------------------------------------------
// NavigationUI.cs — tactical navigation panel: nearby ports + wind indicator
// ---------------------------------------------------------------------------
// A screen-space overlay panel anchored bottom-left, positioned above the
// radar minimap. Shows:
//   • A "NEARBY PORTS" list — up to 5 discovered ports with compass bearing
//     and distance in world units.
//   • A "WIND" indicator — current wind direction, cardinal label, and a
//     filled strength bar.
//
// Wired by BootyBootstrap. Subscribes to WorldMapManager.OnPortDiscovered to
// refresh immediately when a new port is found.
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Booty.World;

namespace Booty.UI
{
    /// <summary>
    /// Screen-space navigation overlay. Bottom-left corner.
    /// Shows discovered nearby ports (bearing + distance) and wind state.
    /// Constructs its own Canvas hierarchy — no prefab dependency.
    /// </summary>
    public class NavigationUI : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Palette
        // ══════════════════════════════════════════════════════════════════

        private static readonly Color PanelBg      = new Color(0.04f, 0.07f, 0.14f, 0.90f);
        private static readonly Color AccentGold    = new Color(1f,    0.85f, 0.2f);
        private static readonly Color GoldDim       = new Color(0.8f,  0.65f, 0.1f, 0.6f);
        private static readonly Color TextWhite     = new Color(0.95f, 0.95f, 0.95f);
        private static readonly Color TextGrey      = new Color(0.65f, 0.65f, 0.65f);
        private static readonly Color WindBlue      = new Color(0.5f,  0.85f, 1f);
        private static readonly Color WindLow       = new Color(0.4f,  0.65f, 0.9f);
        private static readonly Color WindHigh      = new Color(0.9f,  0.95f, 1f);

        // ══════════════════════════════════════════════════════════════════
        //  Constants
        // ══════════════════════════════════════════════════════════════════

        private const float NearbyPortRadius = 250f;
        private const int   MaxPortsListed   = 5;
        private const float UpdateInterval   = 0.5f;

        // ══════════════════════════════════════════════════════════════════
        //  UI References
        // ══════════════════════════════════════════════════════════════════

        private readonly List<Text> _portNameTexts    = new List<Text>();
        private readonly List<Text> _portBearingTexts = new List<Text>();
        private Text  _windDirText;
        private Text  _windStrengthLabel;
        private Image _windStrengthBar;

        // ══════════════════════════════════════════════════════════════════
        //  Runtime
        // ══════════════════════════════════════════════════════════════════

        private WorldMapManager _worldMap;
        private Transform       _playerTransform;
        private float           _updateTimer;

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Awake()
        {
            BuildHierarchy();
        }

        private void Start()
        {
            _worldMap = WorldMapManager.Instance ?? FindObjectOfType<WorldMapManager>();

            if (_worldMap != null)
                _worldMap.OnPortDiscovered += OnPortDiscovered;
        }

        private void OnDestroy()
        {
            if (_worldMap != null)
                _worldMap.OnPortDiscovered -= OnPortDiscovered;
        }

        private void Update()
        {
            _updateTimer -= Time.deltaTime;
            if (_updateTimer > 0f) return;

            _updateTimer = UpdateInterval;
            RefreshPlayerRef();
            RefreshPortList();
            RefreshWindIndicator();
        }

        // ══════════════════════════════════════════════════════════════════
        //  Event Handlers
        // ══════════════════════════════════════════════════════════════════

        private void OnPortDiscovered(PortData pd)
        {
            // Force an immediate refresh so the newly-discovered port appears
            _updateTimer = 0f;
            Debug.Log($"[NavigationUI] Port discovered: {pd.portName}");
        }

        // ══════════════════════════════════════════════════════════════════
        //  Data Refresh
        // ══════════════════════════════════════════════════════════════════

        private void RefreshPlayerRef()
        {
            if (_playerTransform != null) return;

            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null)
                _playerTransform = playerGO.transform;
        }

        private void RefreshPortList()
        {
            if (_worldMap == null || _playerTransform == null)
            {
                ClearPortRows();
                return;
            }

            Vector3 playerPos = _playerTransform.position;
            List<PortData> nearby = _worldMap.GetNearbyPorts(playerPos, NearbyPortRadius);

            int shown = Mathf.Min(nearby.Count, MaxPortsListed);

            for (int i = 0; i < MaxPortsListed; i++)
            {
                if (i < shown)
                {
                    var pd    = nearby[i];
                    Vector3 d = pd.worldPosition - playerPos;
                    d.y = 0f;

                    float dist    = d.magnitude;
                    float bearing = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                    if (bearing < 0f) bearing += 360f;

                    string cardinal = AngleToCardinal(bearing);

                    _portNameTexts[i].text    = pd.portName;
                    _portNameTexts[i].color   = TextWhite;
                    _portBearingTexts[i].text  = string.Format("{0} {1:F0}u", cardinal, dist);
                    _portBearingTexts[i].color = TextGrey;
                }
                else
                {
                    _portNameTexts[i].text    = "";
                    _portBearingTexts[i].text = "";
                }
            }
        }

        private void RefreshWindIndicator()
        {
            var wind = WindSystem.Current;

            if (wind == null)
            {
                if (_windDirText      != null) _windDirText.text      = "—";
                if (_windStrengthLabel != null) _windStrengthLabel.text = "Calm";
                return;
            }

            if (_windDirText != null)
                _windDirText.text = string.Format("{0}  {1:F0}°", wind.WindCardinal, wind.WindAngleDeg);

            float s = wind.WindStrength;

            if (_windStrengthLabel != null)
            {
                string label = s < 0.25f ? "Calm"
                             : s < 0.50f ? "Light"
                             : s < 0.75f ? "Fresh"
                             :             "Gale";
                _windStrengthLabel.text  = label;
                _windStrengthLabel.color = Color.Lerp(WindLow, WindHigh, s);
            }

            if (_windStrengthBar != null)
            {
                _windStrengthBar.fillAmount = s;
                _windStrengthBar.color      = Color.Lerp(WindLow, WindHigh, s);
            }
        }

        private void ClearPortRows()
        {
            for (int i = 0; i < _portNameTexts.Count; i++)
            {
                _portNameTexts[i].text    = "";
                _portBearingTexts[i].text = "";
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  UI Construction
        // ══════════════════════════════════════════════════════════════════

        private void BuildHierarchy()
        {
            var canvasGO = new GameObject("NavUI_Canvas");
            canvasGO.transform.SetParent(transform, false);

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9; // one below HUDManager (10)
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            BuildWindPanel(canvasGO);
            BuildPortPanel(canvasGO);
        }

        // ── Wind panel (bottom-left, lowest) ───────────────────────────

        private void BuildWindPanel(GameObject canvas)
        {
            const float panelW = 185f;
            const float panelH = 62f;

            var panelGO = new GameObject("Wind_Panel");
            panelGO.transform.SetParent(canvas.transform, false);
            var rt = panelGO.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot     = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(10f, 10f);
            rt.sizeDelta        = new Vector2(panelW, panelH);
            panelGO.AddComponent<Image>().color = PanelBg;
            AddOutline(panelGO, GoldDim);

            // Title
            MakeTextChild(panelGO, "Title", "WIND",
                new Vector2(0.5f, 1f), new Vector2(0f, -5f), new Vector2(175f, 16f),
                9, FontStyle.Bold, WindBlue, TextAnchor.MiddleCenter);

            // Direction label
            _windDirText = MakeTextChild(panelGO, "WindDir", "— °",
                new Vector2(0f, 0.5f), new Vector2(6f, 4f), new Vector2(120f, 22f),
                13, FontStyle.Bold, WindBlue, TextAnchor.MiddleLeft);
            AddShadow(_windDirText.gameObject);

            // Strength label (right-aligned)
            _windStrengthLabel = MakeTextChild(panelGO, "WindStrength", "Calm",
                new Vector2(1f, 0.5f), new Vector2(-6f, 4f), new Vector2(50f, 22f),
                10, FontStyle.Normal, WindLow, TextAnchor.MiddleRight);

            // Strength fill bar
            BuildWindBar(panelGO);
        }

        private void BuildWindBar(GameObject parent)
        {
            // Track (dark background)
            var trackGO = new GameObject("WindBar_Track");
            trackGO.transform.SetParent(parent.transform, false);
            var tRT = trackGO.AddComponent<RectTransform>();
            tRT.anchorMin = new Vector2(0f, 0f); tRT.anchorMax = new Vector2(1f, 0f);
            tRT.pivot     = new Vector2(0.5f, 0f);
            tRT.anchoredPosition = new Vector2(0f, 7f);
            tRT.sizeDelta        = new Vector2(-12f, 7f);
            trackGO.AddComponent<Image>().color = new Color(0.1f, 0.15f, 0.25f, 0.9f);

            // Fill (coloured bar)
            var fillGO = new GameObject("WindBar_Fill");
            fillGO.transform.SetParent(trackGO.transform, false);
            var fRT = fillGO.AddComponent<RectTransform>();
            fRT.anchorMin = new Vector2(0f, 0f); fRT.anchorMax = new Vector2(1f, 1f);
            fRT.pivot     = new Vector2(0f, 0.5f);
            fRT.anchoredPosition = Vector2.zero;
            fRT.sizeDelta        = Vector2.zero;

            var fillImg        = fillGO.AddComponent<Image>();
            fillImg.type       = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 0.5f;
            fillImg.color      = WindBlue;
            _windStrengthBar   = fillImg;
        }

        // ── Port list panel (bottom-left, above wind panel) ─────────────

        private void BuildPortPanel(GameObject canvas)
        {
            const float panelW    = 185f;
            const float rowHeight = 25f;
            float panelH = 22f + MaxPortsListed * rowHeight + 8f;

            // Position above the wind panel (height 62 + gap 6)
            float bottomY = 10f + 62f + 6f;

            var panelGO = new GameObject("Ports_Panel");
            panelGO.transform.SetParent(canvas.transform, false);
            var rt = panelGO.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot     = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(10f, bottomY);
            rt.sizeDelta        = new Vector2(panelW, panelH);
            panelGO.AddComponent<Image>().color = PanelBg;
            AddOutline(panelGO, GoldDim);

            // Title
            MakeTextChild(panelGO, "Title", "NEARBY PORTS",
                new Vector2(0.5f, 1f), new Vector2(0f, -5f), new Vector2(175f, 17f),
                9, FontStyle.Bold, AccentGold, TextAnchor.MiddleCenter);

            // Port rows
            for (int i = 0; i < MaxPortsListed; i++)
            {
                float rowY = -(22f + i * rowHeight + rowHeight * 0.5f);

                // Port name (left)
                var nameText = MakeTextChild(panelGO, "Port_Name_" + i, "",
                    new Vector2(0f, 1f), new Vector2(6f, rowY), new Vector2(115f, rowHeight - 2f),
                    11, FontStyle.Normal, TextWhite, TextAnchor.MiddleLeft);
                _portNameTexts.Add(nameText);

                // Bearing + distance (right)
                var bearingText = MakeTextChild(panelGO, "Port_Bearing_" + i, "",
                    new Vector2(1f, 1f), new Vector2(-6f, rowY), new Vector2(62f, rowHeight - 2f),
                    10, FontStyle.Normal, TextGrey, TextAnchor.MiddleRight);
                _portBearingTexts.Add(bearingText);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  UI Helpers
        // ══════════════════════════════════════════════════════════════════

        private static Text MakeTextChild(
            GameObject parent, string name, string content,
            Vector2 anchor, Vector2 pos, Vector2 size,
            int fontSize, FontStyle style, Color color, TextAnchor align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor; rt.anchorMax = anchor;
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta        = size;
            var t = go.AddComponent<Text>();
            t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text      = content;
            t.fontSize  = fontSize;
            t.fontStyle = style;
            t.color     = color;
            t.alignment = align;
            return t;
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
            s.effectColor    = new Color(0f, 0f, 0f, 0.85f);
            s.effectDistance = new Vector2(2f, -2f);
        }

        private static string AngleToCardinal(float deg)
        {
            deg = (deg % 360f + 360f) % 360f;
            int sector = Mathf.RoundToInt(deg / 45f) % 8;
            string[] dirs = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            return dirs[sector];
        }
    }
}
