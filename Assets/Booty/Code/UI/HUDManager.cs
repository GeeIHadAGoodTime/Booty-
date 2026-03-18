// ---------------------------------------------------------------------------
// HUDManager.cs — Polished screen-space HUD (S3.3 upgrade)
// ---------------------------------------------------------------------------
// Three styled stat panels (Gold / HP / Renown), compass indicator (top-right),
// radar minimap (bottom-right), centred income notification with alpha fade-out.
//
// Public API preserved from S2.8: ShowIncomeNotification(float amount).
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Booty.Bootstrap;
using Booty.Economy;
using Booty.Save;
using Booty.Ports;
using Booty.Ships;

namespace Booty.UI
{
    /// <summary>
    /// Screen-space overlay HUD. Gold / HP / Renown stat panels anchored top-left;
    /// compass (top-right); radar minimap (bottom-right); income notification (bottom-centre).
    /// Constructs its own Canvas hierarchy — no prefab dependency.
    /// </summary>
    public class HUDManager : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Palette
        // ══════════════════════════════════════════════════════════════════

        private static readonly Color GoldColor      = new Color(1f,    0.85f, 0.2f);
        private static readonly Color GoldDim        = new Color(0.8f,  0.65f, 0.1f, 0.6f);
        private static readonly Color HpGreen        = new Color(0.25f, 0.90f, 0.35f);
        private static readonly Color HpLow          = new Color(0.95f, 0.25f, 0.20f);
        private static readonly Color RenownBlue     = new Color(0.45f, 0.75f, 1.0f);
        private static readonly Color PanelBg        = new Color(0.04f, 0.07f, 0.14f, 0.90f);
        private static readonly Color FactionPlayer  = new Color(1f,    0.85f, 0.2f);   // gold
        private static readonly Color FactionEnemy   = new Color(0.9f,  0.20f, 0.20f);  // red
        private static readonly Color FactionNeutral = new Color(0.55f, 0.55f, 0.55f);  // grey

        // ══════════════════════════════════════════════════════════════════
        //  UI References
        // ══════════════════════════════════════════════════════════════════

        // Stat panels
        private Text  _goldValueText;
        private Text  _hpValueText;
        private Text  _renownValueText;

        // Compass
        private Text  _compassDirText;
        private Text  _compassHeadingText;

        // Minimap
        private RectTransform _minimapRoot;
        private List<GameObject> _minimapDots = new List<GameObject>();

        // Kill counter
        private Text _killValueText;
        private int  _sessionKills = 0;

        // HP panel flash (below 25%)
        private Image _hpPanelBgImage;
        private float _hpFlashTimer    = 0f;
        private bool  _hpFlashVisible  = true;
        private const float HpFlashInterval = 0.4f;

        // Notification
        private Text  _notificationText;
        private float _notificationTimer;
        private const float NotificationDuration = 3f;

        // Minimap update tick
        private float _minimapTimer;
        private const float MinimapUpdateInterval = 0.5f;
        private const float MinimapRange          = 200f;   // world units shown by minimap

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Awake()
        {
            BuildHierarchy();
        }

        private void Start()
        {
            var eco = FindObjectOfType<EconomySystem>();
            if (eco != null) eco.OnIncomeCollected += OnIncomeCollected;
        }

        private void OnDestroy()
        {
            var eco = FindObjectOfType<EconomySystem>();
            if (eco != null) eco.OnIncomeCollected -= OnIncomeCollected;
        }

        private void Update()
        {
            RefreshStatPanels();
            RefreshCompass();
            TickNotification();
            TickHpFlash();

            _minimapTimer -= Time.deltaTime;
            if (_minimapTimer <= 0f)
            {
                _minimapTimer = MinimapUpdateInterval;
                RefreshMinimap();
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Public API (preserved from S2.8)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Increments the session kill counter and updates the HUD panel.
        /// Called by BootyBootstrap when any enemy is destroyed.
        /// </summary>
        public void RegisterKill()
        {
            _sessionKills++;
            if (_killValueText != null)
                _killValueText.text = _sessionKills.ToString();
        }

        /// <summary>
        /// Show a transient income notification at the bottom of the screen.
        /// Resets the countdown so rapid ticks restart the fade.
        /// </summary>
        /// <param name="amount">Gold amount to display.</param>
        public void ShowIncomeNotification(float amount)
        {
            if (_notificationText == null) return;
            _notificationText.text  = string.Format("+{0:F0} gold", amount);
            _notificationTimer      = NotificationDuration;
            var c = _notificationText.color;
            c.a = 1f;
            _notificationText.color = c;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Data Refresh
        // ══════════════════════════════════════════════════════════════════

        private void RefreshStatPanels()
        {
            // HP
            var hpSys = GameRoot.Instance?.HPSystem;
            if (hpSys != null && _hpValueText != null)
            {
                _hpValueText.text  = string.Format("{0} / {1}", hpSys.CurrentHP, hpSys.MaxHP);
                float ratio = hpSys.MaxHP > 0 ? (float)hpSys.CurrentHP / hpSys.MaxHP : 1f;
                _hpValueText.color = Color.Lerp(HpLow, HpGreen, ratio);
            }

            // Gold + Renown
            var saveSys = FindObjectOfType<SaveSystem>();
            if (saveSys != null && saveSys.CurrentState != null)
            {
                var p = saveSys.CurrentState.player;
                if (_goldValueText   != null) _goldValueText.text   = string.Format("{0:F0}", p.gold);
                if (_renownValueText != null) _renownValueText.text = string.Format("{0:F0}", p.renown);
            }
        }

        private void RefreshCompass()
        {
            var ship = GameRoot.Instance?.PlayerShip;
            if (ship == null || _compassDirText == null) return;

            float heading = ship.transform.eulerAngles.y;
            _compassHeadingText.text = string.Format("{0:F0}°", Mathf.Round(heading));
            _compassDirText.text     = HeadingToCardinal(heading);
        }

        private static string HeadingToCardinal(float deg)
        {
            deg = (deg % 360f + 360f) % 360f;
            int sector = Mathf.RoundToInt(deg / 45f) % 8;
            string[] dirs = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            return dirs[sector];
        }

        private void TickHpFlash()
        {
            var hpSys = GameRoot.Instance?.HPSystem;
            if (hpSys == null || _hpPanelBgImage == null) return;

            float ratio = hpSys.MaxHP > 0 ? (float)hpSys.CurrentHP / hpSys.MaxHP : 1f;

            if (ratio < 0.25f)
            {
                _hpFlashTimer += Time.deltaTime;
                if (_hpFlashTimer >= HpFlashInterval)
                {
                    _hpFlashTimer   = 0f;
                    _hpFlashVisible = !_hpFlashVisible;
                }
                _hpPanelBgImage.color = _hpFlashVisible
                    ? new Color(0.6f, 0.05f, 0.05f, 0.95f)   // danger red flash
                    : PanelBg;
            }
            else
            {
                _hpPanelBgImage.color = PanelBg;
                _hpFlashTimer   = 0f;
                _hpFlashVisible = true;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Minimap
        // ══════════════════════════════════════════════════════════════════

        private void RefreshMinimap()
        {
            if (_minimapRoot == null) return;

            // Clear old dots
            foreach (var d in _minimapDots)
                if (d != null) Destroy(d);
            _minimapDots.Clear();

            var ship = GameRoot.Instance?.PlayerShip;
            Vector3 playerPos = ship != null ? ship.transform.position : Vector3.zero;

            float halfMap = _minimapRoot.sizeDelta.x * 0.5f; // pixels from centre to edge

            // ── Ports ─────────────────────────────────────────────────
            var portSystem = FindObjectOfType<PortSystem>();
            if (portSystem != null)
            {
                foreach (var kv in portSystem.GetAllPorts())
                {
                    var portData = kv.Value;
                    Vector3 delta = portData.worldPosition - playerPos;
                    float dx = delta.x; float dz = delta.z;
                    float dist = Mathf.Sqrt(dx * dx + dz * dz);
                    if (dist > MinimapRange) continue;

                    Vector2 dotPos = new Vector2(
                        (dx / MinimapRange) * halfMap,
                        (dz / MinimapRange) * halfMap);

                    Color dotColor = portData.factionOwner == "player_pirates"
                        ? FactionPlayer
                        : (portData.factionOwner == "neutral" ? FactionNeutral : FactionEnemy);

                    SpawnMinimapDot(dotPos, dotColor, 7f, portData.portName ?? portData.portId);
                }
            }

            // ── Enemy ships ───────────────────────────────────────────
            var enemies = FindObjectsOfType<EnemyAI>();
            foreach (var enemy in enemies)
            {
                if (enemy == null || enemy.gameObject == null) continue;
                Vector3 delta = enemy.transform.position - playerPos;
                float dx = delta.x; float dz = delta.z;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);
                if (dist > MinimapRange) continue;

                Vector2 dotPos = new Vector2(
                    (dx / MinimapRange) * halfMap,
                    (dz / MinimapRange) * halfMap);

                SpawnMinimapDot(dotPos, FactionEnemy, 5f, null);
            }
        }

        private void SpawnMinimapDot(Vector2 localPos, Color color, float size, string tooltip)
        {
            var go = new GameObject("Dot");
            go.transform.SetParent(_minimapRoot, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = localPos;
            rt.sizeDelta = new Vector2(size, size);
            go.AddComponent<Image>().color = color;
            _minimapDots.Add(go);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Notification tick
        // ══════════════════════════════════════════════════════════════════

        private void OnIncomeCollected(float amount, int portCount)
        {
            ShowIncomeNotification(amount);
        }

        private void TickNotification()
        {
            if (_notificationTimer <= 0f || _notificationText == null) return;

            _notificationTimer -= Time.deltaTime;
            float alpha = Mathf.Clamp01(_notificationTimer / NotificationDuration);
            var c = _notificationText.color;
            c.a = alpha;
            _notificationText.color = c;

            if (_notificationTimer <= 0f)
            {
                _notificationTimer     = 0f;
                _notificationText.text = "";
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  UI Construction
        // ══════════════════════════════════════════════════════════════════

        private void BuildHierarchy()
        {
            var canvasGO = new GameObject("HUD_Canvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            BuildStatPanels(canvasGO);
            BuildCompassWidget(canvasGO);
            BuildMinimapWidget(canvasGO);
            BuildNotification(canvasGO);
        }

        // ── Stat panels (top-left) ──────────────────────────────────────

        private void BuildStatPanels(GameObject canvas)
        {
            float startX = 10f;
            float panelW = 140f;
            float panelH = 58f;
            float gap    = 8f;

            _goldValueText   = MakeStatPanel(canvas, "Gold",   "⚓ GOLD",   GoldColor,                  0, startX, panelW, panelH, gap);
            _hpValueText     = MakeStatPanel(canvas, "HP",     "♥ HULL",   HpGreen,                    1, startX, panelW, panelH, gap);
            _renownValueText = MakeStatPanel(canvas, "Renown", "★ RENOWN", RenownBlue,                 2, startX, panelW, panelH, gap);
            _killValueText   = MakeStatPanel(canvas, "Kills",  "KILLS",    new Color(0.9f, 0.5f, 0.9f), 3, startX, panelW, panelH, gap);
            if (_killValueText != null) _killValueText.text = "0";

            // Cache HP panel background Image for low-HP flash effect
            var hpPanelT = canvas.transform.Find("HUD_HP");
            if (hpPanelT != null) _hpPanelBgImage = hpPanelT.GetComponent<Image>();
        }

        private Text MakeStatPanel(GameObject canvas, string id, string labelStr,
            Color accentColor, int index, float startX, float w, float h, float gap)
        {
            float xOffset = startX + index * (w + gap);

            var panelGO = new GameObject("HUD_" + id);
            panelGO.transform.SetParent(canvas.transform, false);
            var rt = panelGO.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(xOffset, -10f);
            rt.sizeDelta        = new Vector2(w, h);
            panelGO.AddComponent<Image>().color = PanelBg;
            AddOutline(panelGO, GoldDim);

            // Label row
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(panelGO.transform, false);
            var lRT = labelGO.AddComponent<RectTransform>();
            lRT.anchorMin = new Vector2(0f, 1f); lRT.anchorMax = new Vector2(1f, 1f);
            lRT.pivot = new Vector2(0.5f, 1f);
            lRT.anchoredPosition = new Vector2(0f, -4f); lRT.sizeDelta = new Vector2(0f, 22f);
            var lTxt = labelGO.AddComponent<Text>();
            lTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            lTxt.text = labelStr; lTxt.fontSize = 11; lTxt.fontStyle = FontStyle.Bold;
            lTxt.color = accentColor; lTxt.alignment = TextAnchor.MiddleCenter;

            // Value row
            var valueGO = new GameObject("Value");
            valueGO.transform.SetParent(panelGO.transform, false);
            var vRT = valueGO.AddComponent<RectTransform>();
            vRT.anchorMin = new Vector2(0f, 0f); vRT.anchorMax = new Vector2(1f, 0f);
            vRT.pivot = new Vector2(0.5f, 0f);
            vRT.anchoredPosition = new Vector2(0f, 6f); vRT.sizeDelta = new Vector2(0f, 28f);
            var vTxt = valueGO.AddComponent<Text>();
            vTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            vTxt.text = "..."; vTxt.fontSize = 18; vTxt.fontStyle = FontStyle.Bold;
            vTxt.color = Color.white; vTxt.alignment = TextAnchor.MiddleCenter;
            AddShadow(valueGO);

            return vTxt;
        }

        // ── Compass widget (top-right) ──────────────────────────────────

        private void BuildCompassWidget(GameObject canvas)
        {
            var widgetGO = new GameObject("Compass_Widget");
            widgetGO.transform.SetParent(canvas.transform, false);
            var rt = widgetGO.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-10f, -10f);
            rt.sizeDelta = new Vector2(80f, 80f);
            widgetGO.AddComponent<Image>().color = PanelBg;
            AddOutline(widgetGO, GoldDim);

            // "COMPASS" label
            var titleGO = MakeTextChild(widgetGO, "Title", "COMPASS",
                new Vector2(0.5f, 1f), new Vector2(0f, -5f), new Vector2(76f, 18f),
                10, FontStyle.Bold, GoldColor, TextAnchor.MiddleCenter);

            // Cardinal direction (large, centre)
            _compassDirText = MakeTextChild(widgetGO, "Dir", "N",
                new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(60f, 32f),
                26, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            AddShadow(_compassDirText.gameObject);

            // Numeric heading (smaller, below)
            _compassHeadingText = MakeTextChild(widgetGO, "Heading", "0°",
                new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(70f, 20f),
                12, FontStyle.Normal, new Color(0.75f, 0.75f, 0.75f), TextAnchor.MiddleCenter);

            // Decorative N/S/E/W labels
            MakeTextChild(widgetGO, "N", "N", new Vector2(0.5f, 1f), new Vector2(0f, -24f),
                new Vector2(14f, 16f), 9, FontStyle.Bold, GoldDim, TextAnchor.MiddleCenter);
            MakeTextChild(widgetGO, "S", "S", new Vector2(0.5f, 0f), new Vector2(0f, 10f),
                new Vector2(14f, 16f), 9, FontStyle.Bold, GoldDim, TextAnchor.MiddleCenter);
            MakeTextChild(widgetGO, "W", "W", new Vector2(0f, 0.5f), new Vector2(8f, 0f),
                new Vector2(14f, 16f), 9, FontStyle.Bold, GoldDim, TextAnchor.MiddleCenter);
            MakeTextChild(widgetGO, "E", "E", new Vector2(1f, 0.5f), new Vector2(-8f, 0f),
                new Vector2(14f, 16f), 9, FontStyle.Bold, GoldDim, TextAnchor.MiddleCenter);
        }

        // ── Minimap widget (bottom-right) ───────────────────────────────

        private void BuildMinimapWidget(GameObject canvas)
        {
            // Outer frame
            var frameGO = new GameObject("Minimap_Frame");
            frameGO.transform.SetParent(canvas.transform, false);
            var fRT = frameGO.AddComponent<RectTransform>();
            fRT.anchorMin = new Vector2(1f, 0f); fRT.anchorMax = new Vector2(1f, 0f);
            fRT.pivot = new Vector2(1f, 0f);
            fRT.anchoredPosition = new Vector2(-10f, 10f);
            fRT.sizeDelta = new Vector2(130f, 142f);
            frameGO.AddComponent<Image>().color = PanelBg;
            AddOutline(frameGO, GoldDim);

            // Title
            MakeTextChild(frameGO, "Title", "RADAR",
                new Vector2(0.5f, 1f), new Vector2(0f, -5f), new Vector2(120f, 18f),
                10, FontStyle.Bold, GoldColor, TextAnchor.MiddleCenter);

            // Radar area
            var radarGO = new GameObject("Radar_Area");
            radarGO.transform.SetParent(frameGO.transform, false);
            var rRT = radarGO.AddComponent<RectTransform>();
            rRT.anchorMin = new Vector2(0.5f, 0f); rRT.anchorMax = new Vector2(0.5f, 0f);
            rRT.pivot = new Vector2(0.5f, 0f);
            rRT.anchoredPosition = new Vector2(0f, 8f);
            rRT.sizeDelta = new Vector2(120f, 120f);
            radarGO.AddComponent<Image>().color = new Color(0.02f, 0.04f, 0.10f, 0.95f);
            AddOutline(radarGO, new Color(0.3f, 0.4f, 0.25f, 0.6f));

            _minimapRoot = rRT;

            // Player dot (always at centre)
            var playerDot = new GameObject("PlayerDot");
            playerDot.transform.SetParent(radarGO.transform, false);
            var pdRT = playerDot.AddComponent<RectTransform>();
            pdRT.anchorMin = new Vector2(0.5f, 0.5f); pdRT.anchorMax = new Vector2(0.5f, 0.5f);
            pdRT.pivot = new Vector2(0.5f, 0.5f);
            pdRT.anchoredPosition = Vector2.zero; pdRT.sizeDelta = new Vector2(8f, 8f);
            playerDot.AddComponent<Image>().color = new Color(0.3f, 0.9f, 1f);

            // Range rings (decorative)
            for (int ring = 1; ring <= 2; ring++)
            {
                var ringGO = new GameObject("Ring" + ring);
                ringGO.transform.SetParent(radarGO.transform, false);
                var ringRT = ringGO.AddComponent<RectTransform>();
                ringRT.anchorMin = new Vector2(0.5f, 0.5f); ringRT.anchorMax = new Vector2(0.5f, 0.5f);
                ringRT.pivot = new Vector2(0.5f, 0.5f);
                ringRT.anchoredPosition = Vector2.zero;
                float sz = 60f * ring;
                ringRT.sizeDelta = new Vector2(sz, sz);
                var ringImg = ringGO.AddComponent<Image>();
                ringImg.color = new Color(0.25f, 0.35f, 0.20f, 0.25f);
            }

            // Legend
            MakeTextChild(frameGO, "Legend_Player", "◆ You",
                new Vector2(0f, 0f), new Vector2(6f, 5f), new Vector2(60f, 14f),
                9, FontStyle.Normal, new Color(0.3f, 0.9f, 1f), TextAnchor.MiddleLeft);
            MakeTextChild(frameGO, "Legend_Port", "● Port",
                new Vector2(0.5f, 0f), new Vector2(2f, 5f), new Vector2(50f, 14f),
                9, FontStyle.Normal, FactionPlayer, TextAnchor.MiddleLeft);
        }

        // ── Notification (bottom-centre) ────────────────────────────────

        private void BuildNotification(GameObject canvas)
        {
            var go = new GameObject("HUD_Notification");
            go.transform.SetParent(canvas.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f); rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 80f);
            rt.sizeDelta = new Vector2(320f, 40f);

            _notificationText = go.AddComponent<Text>();
            _notificationText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _notificationText.fontSize  = 22;
            _notificationText.fontStyle = FontStyle.Bold;
            _notificationText.color     = new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0f);
            _notificationText.alignment = TextAnchor.MiddleCenter;
            _notificationText.text      = "";
            AddShadow(go);
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
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = content; t.fontSize = fontSize; t.fontStyle = style;
            t.color = color; t.alignment = align;
            return t;
        }

        private static void AddOutline(GameObject go, Color color)
        {
            var o = go.AddComponent<Outline>();
            o.effectColor = color; o.effectDistance = new Vector2(1f, -1f);
        }

        private static void AddShadow(GameObject go)
        {
            var s = go.AddComponent<Shadow>();
            s.effectColor    = new Color(0f, 0f, 0f, 0.85f);
            s.effectDistance = new Vector2(2f, -2f);
        }
    }
}
