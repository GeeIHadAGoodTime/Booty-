// ---------------------------------------------------------------------------
// MainMenuManager.cs — Programmatic main menu scene UI
// ---------------------------------------------------------------------------
// Builds its own Canvas hierarchy in Awake() — no prefab dependency.
// Pirate-themed dark background with gold title, 4 menu buttons, settings panel.
// Scene transitions: Start Game → World_Main, Continue → World_Main (if save exists).
// ---------------------------------------------------------------------------

using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Booty.UI
{
    /// <summary>
    /// Main menu controller. Constructs the entire UI programmatically at
    /// runtime. Place on a single empty "MainMenu" GameObject in MainMenu.unity.
    /// </summary>
    public class MainMenuManager : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Private UI References
        // ══════════════════════════════════════════════════════════════════

        private GameObject _settingsPanel;
        private Slider     _volumeSlider;
        private Text       _feedbackText;
        private float      _feedbackTimer;

        // ══════════════════════════════════════════════════════════════════
        //  Constants — pirate palette
        // ══════════════════════════════════════════════════════════════════

        private static readonly Color BgColor        = new Color(0.05f, 0.08f, 0.15f, 1f);
        private static readonly Color GoldColor      = new Color(1f,   0.85f, 0.2f,  1f);
        private static readonly Color GoldDim        = new Color(0.8f, 0.65f, 0.1f,  1f);
        private static readonly Color ButtonNormal   = new Color(0.10f, 0.15f, 0.25f, 0.92f);
        private static readonly Color ButtonHover    = new Color(0.20f, 0.30f, 0.45f, 0.98f);
        private static readonly Color ButtonPressed  = new Color(0.06f, 0.10f, 0.18f, 1f);
        private static readonly Color PanelBg        = new Color(0.04f, 0.07f, 0.14f, 0.97f);

        private const float ButtonWidth  = 280f;
        private const float ButtonHeight = 52f;
        private const float ButtonGap    = 12f;

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Awake()
        {
            BuildUI();
            ApplySavedVolume();
        }

        private void Update()
        {
            TickFeedback();
        }

        // ══════════════════════════════════════════════════════════════════
        //  Button Actions
        // ══════════════════════════════════════════════════════════════════

        private void StartGame()
        {
            SceneManager.LoadScene("World_Main");
        }

        private void ContinueGame()
        {
            if (HasSaveFile())
            {
                SceneManager.LoadScene("World_Main");
            }
            else
            {
                ShowFeedback("No save found.", new Color(1f, 0.35f, 0.35f));
            }
        }

        private void ToggleSettings()
        {
            if (_settingsPanel != null)
                _settingsPanel.SetActive(!_settingsPanel.activeSelf);
        }

        private void QuitGame()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        // ══════════════════════════════════════════════════════════════════
        //  Helpers
        // ══════════════════════════════════════════════════════════════════

        private static bool HasSaveFile()
        {
            string path = Path.Combine(Application.persistentDataPath, "booty_save.json");
            return File.Exists(path);
        }

        private void ShowFeedback(string message, Color color)
        {
            if (_feedbackText == null) return;
            _feedbackText.text  = message;
            _feedbackText.color = color;
            _feedbackTimer      = 2.5f;
        }

        private void TickFeedback()
        {
            if (_feedbackTimer <= 0f || _feedbackText == null) return;
            _feedbackTimer -= Time.unscaledDeltaTime;
            float alpha = Mathf.Clamp01(_feedbackTimer / 0.6f);
            var c = _feedbackText.color;
            c.a = alpha;
            _feedbackText.color = c;
            if (_feedbackTimer <= 0f)
                _feedbackText.text = "";
        }

        private void ApplySavedVolume()
        {
            float vol = PlayerPrefs.GetFloat("MasterVolume", 80f);
            AudioListener.volume = vol / 100f;
            if (_volumeSlider != null)
                _volumeSlider.value = vol;
        }

        private void OnVolumeChanged(float value)
        {
            AudioListener.volume = value / 100f;
            PlayerPrefs.SetFloat("MasterVolume", value);
        }

        // ══════════════════════════════════════════════════════════════════
        //  UI Construction
        // ══════════════════════════════════════════════════════════════════

        private void BuildUI()
        {
            // ── Root Canvas ──────────────────────────────────────────────
            var canvasGO = new GameObject("MainMenu_Canvas");
            canvasGO.transform.SetParent(transform, false);

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            canvasGO.AddComponent<GraphicRaycaster>();

            // ── Full-screen background ───────────────────────────────────
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(canvasGO.transform, false);
            var bgRect = bgGO.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = BgColor;

            // ── Title: "BOOTY!" ──────────────────────────────────────────
            var titleText = CreateText(canvasGO, "Title",
                "BOOTY!",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 140f), new Vector2(700f, 90f),
                64, FontStyle.Bold, GoldColor, TextAnchor.MiddleCenter);
            AddOutline(titleText.gameObject, new Color(0.5f, 0.3f, 0f, 0.8f));
            AddShadow(titleText.gameObject);

            // ── Subtitle ─────────────────────────────────────────────────
            CreateText(canvasGO, "Subtitle",
                "A Pirates Rise",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 86f), new Vector2(500f, 36f),
                22, FontStyle.Italic, new Color(0.85f, 0.85f, 0.85f),
                TextAnchor.MiddleCenter);

            // ── Gold divider ─────────────────────────────────────────────
            var divGO = new GameObject("Divider");
            divGO.transform.SetParent(canvasGO.transform, false);
            var divRect = divGO.AddComponent<RectTransform>();
            divRect.anchorMin = new Vector2(0.5f, 0.5f);
            divRect.anchorMax = new Vector2(0.5f, 0.5f);
            divRect.pivot = new Vector2(0.5f, 0.5f);
            divRect.anchoredPosition = new Vector2(0f, 58f);
            divRect.sizeDelta = new Vector2(340f, 2f);
            var divImg = divGO.AddComponent<Image>();
            divImg.color = GoldDim;

            // ── Menu buttons (stacked from center-ish down) ───────────────
            float startY = 20f;
            CreateMenuButton(canvasGO, "Start Game", new Vector2(0f, startY - 0 * (ButtonHeight + ButtonGap)),
                StartGame);
            CreateMenuButton(canvasGO, "Continue",   new Vector2(0f, startY - 1 * (ButtonHeight + ButtonGap)),
                ContinueGame);
            CreateMenuButton(canvasGO, "Settings",   new Vector2(0f, startY - 2 * (ButtonHeight + ButtonGap)),
                ToggleSettings);
            CreateMenuButton(canvasGO, "Quit",       new Vector2(0f, startY - 3 * (ButtonHeight + ButtonGap)),
                QuitGame);

            // ── Feedback text (shown briefly below buttons) ───────────────
            _feedbackText = CreateText(canvasGO, "Feedback",
                "",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, startY - 4 * (ButtonHeight + ButtonGap) - 10f),
                new Vector2(360f, 30f),
                16, FontStyle.Normal, new Color(1f, 0.35f, 0.35f),
                TextAnchor.MiddleCenter);

            // ── Version + copyright ──────────────────────────────────────
            CreateText(canvasGO, "Version",
                "v0.1.0 Pre-Alpha",
                new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(10f, 22f), new Vector2(200f, 22f),
                12, FontStyle.Normal, new Color(0.5f, 0.5f, 0.5f),
                TextAnchor.MiddleLeft);

            CreateText(canvasGO, "Copyright",
                "\u00A9 2026 Booty Studios",
                new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-10f, 22f), new Vector2(200f, 22f),
                12, FontStyle.Normal, new Color(0.5f, 0.5f, 0.5f),
                TextAnchor.MiddleRight);

            // ── Settings panel (hidden by default) ───────────────────────
            _settingsPanel = BuildSettingsPanel(canvasGO);
            _settingsPanel.SetActive(false);
        }

        private GameObject BuildSettingsPanel(GameObject parent)
        {
            // Overlay
            var overlayGO = new GameObject("Settings_Overlay");
            overlayGO.transform.SetParent(parent.transform, false);
            var overlayRect = overlayGO.AddComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            var overlayImg = overlayGO.AddComponent<Image>();
            overlayImg.color = new Color(0f, 0f, 0f, 0.65f);

            // Panel
            var panelGO = new GameObject("Settings_Panel");
            panelGO.transform.SetParent(overlayGO.transform, false);
            var panelRect = panelGO.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(400f, 280f);
            var panelImg = panelGO.AddComponent<Image>();
            panelImg.color = PanelBg;
            AddOutline(panelGO, GoldDim);

            // Title
            CreateText(panelGO, "Settings_Title",
                "SETTINGS",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -24f), new Vector2(360f, 36f),
                26, FontStyle.Bold, GoldColor, TextAnchor.MiddleCenter);

            // Volume label
            CreateText(panelGO, "Vol_Label",
                "Master Volume",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 50f), new Vector2(300f, 30f),
                18, FontStyle.Normal, Color.white, TextAnchor.MiddleCenter);

            // Volume slider
            _volumeSlider = BuildSlider(panelGO, new Vector2(0f, 10f), new Vector2(300f, 30f));
            _volumeSlider.minValue = 0f;
            _volumeSlider.maxValue = 100f;
            _volumeSlider.value    = 80f;
            _volumeSlider.onValueChanged.AddListener(OnVolumeChanged);

            // Controls label
            CreateText(panelGO, "Controls_Label",
                "WASD move \u00b7 Space fire \u00b7 Esc pause",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -30f), new Vector2(340f, 26f),
                14, FontStyle.Italic, new Color(0.7f, 0.7f, 0.7f),
                TextAnchor.MiddleCenter);

            // Back button
            var backBtn = CreateButton(panelGO, "Back", new Vector2(0f, -80f),
                new Vector2(160f, 44f));
            backBtn.GetComponentInChildren<Text>().text = "BACK";
            backBtn.onClick.AddListener(() => _settingsPanel.SetActive(false));

            return overlayGO;
        }

        // ══════════════════════════════════════════════════════════════════
        //  UI Factories
        // ══════════════════════════════════════════════════════════════════

        private Text CreateText(
            GameObject parent, string name, string content,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pos, Vector2 size,
            int fontSize, FontStyle style, Color color, TextAnchor align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin        = anchorMin;
            rt.anchorMax        = anchorMax;
            rt.pivot            = new Vector2(anchorMin.x + (anchorMax.x - anchorMin.x) * 0.5f, 0.5f);
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

        private Button CreateMenuButton(GameObject parent, string label, Vector2 pos,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);

            var bg = go.AddComponent<Image>();
            bg.color = ButtonNormal;

            var btn = go.AddComponent<Button>();
            var cb  = btn.colors;
            cb.normalColor      = ButtonNormal;
            cb.highlightedColor = ButtonHover;
            cb.pressedColor     = ButtonPressed;
            cb.disabledColor    = new Color(0.25f, 0.25f, 0.25f, 0.5f);
            btn.colors = cb;
            btn.onClick.AddListener(onClick);

            // Gold outline
            AddOutline(go, GoldDim);

            // Label
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(go.transform, false);
            var labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;
            var txt = labelGO.AddComponent<Text>();
            txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.text      = label.ToUpper();
            txt.fontSize  = 20;
            txt.fontStyle = FontStyle.Bold;
            txt.color     = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;

            return btn;
        }

        private Button CreateButton(GameObject parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Btn_" + name);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var bg = go.AddComponent<Image>();
            bg.color = ButtonNormal;

            var btn = go.AddComponent<Button>();
            var cb  = btn.colors;
            cb.normalColor      = ButtonNormal;
            cb.highlightedColor = ButtonHover;
            cb.pressedColor     = ButtonPressed;
            btn.colors = cb;

            AddOutline(go, GoldDim);

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(go.transform, false);
            var lRT = labelGO.AddComponent<RectTransform>();
            lRT.anchorMin = Vector2.zero;
            lRT.anchorMax = Vector2.one;
            lRT.offsetMin = Vector2.zero;
            lRT.offsetMax = Vector2.zero;
            var txt = labelGO.AddComponent<Text>();
            txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize  = 17;
            txt.fontStyle = FontStyle.Bold;
            txt.color     = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;

            return btn;
        }

        private Slider BuildSlider(GameObject parent, Vector2 pos, Vector2 size)
        {
            var sliderGO = new GameObject("VolumeSlider");
            sliderGO.transform.SetParent(parent.transform, false);
            var rt = sliderGO.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var slider = sliderGO.AddComponent<Slider>();

            // Background
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(sliderGO.transform, false);
            var bgRT = bgGO.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = new Vector2(0f, 8f);
            bgRT.offsetMax = new Vector2(0f, -8f);
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.15f, 0.15f, 0.25f, 0.9f);
            slider.targetGraphic = bgImg;

            // Fill area
            var fillAreaGO = new GameObject("Fill Area");
            fillAreaGO.transform.SetParent(sliderGO.transform, false);
            var fillAreaRT = fillAreaGO.AddComponent<RectTransform>();
            fillAreaRT.anchorMin = Vector2.zero;
            fillAreaRT.anchorMax = Vector2.one;
            fillAreaRT.offsetMin = new Vector2(5f, 8f);
            fillAreaRT.offsetMax = new Vector2(-5f, -8f);

            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(fillAreaGO.transform, false);
            var fillRT = fillGO.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            var fillImg = fillGO.AddComponent<Image>();
            fillImg.color = GoldDim;
            slider.fillRect = fillRT;

            // Handle area
            var handleAreaGO = new GameObject("Handle Slide Area");
            handleAreaGO.transform.SetParent(sliderGO.transform, false);
            var handleAreaRT = handleAreaGO.AddComponent<RectTransform>();
            handleAreaRT.anchorMin = Vector2.zero;
            handleAreaRT.anchorMax = Vector2.one;
            handleAreaRT.offsetMin = new Vector2(10f, 0f);
            handleAreaRT.offsetMax = new Vector2(-10f, 0f);

            var handleGO = new GameObject("Handle");
            handleGO.transform.SetParent(handleAreaGO.transform, false);
            var handleRT = handleGO.AddComponent<RectTransform>();
            handleRT.sizeDelta = new Vector2(20f, 30f);
            var handleImg = handleGO.AddComponent<Image>();
            handleImg.color = GoldColor;
            slider.handleRect = handleRT;

            return slider;
        }

        private static void AddOutline(GameObject go, Color color)
        {
            var outline = go.AddComponent<Outline>();
            outline.effectColor    = color;
            outline.effectDistance = new Vector2(1f, -1f);
        }

        private static void AddShadow(GameObject go)
        {
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor    = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(2f, -2f);
        }
    }
}
