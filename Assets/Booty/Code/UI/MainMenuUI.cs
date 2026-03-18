// ---------------------------------------------------------------------------
// MainMenuUI.cs — Programmatic main menu for MainMenu.unity scene
// ---------------------------------------------------------------------------
// Builds its own Canvas hierarchy in Awake() — no prefab dependency.
// Pirate-themed dark ocean background, gold title, 4 buttons, settings panel.
// Scene transitions: Start Game → World_Main, Continue → World_Main (if save).
// ---------------------------------------------------------------------------

using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Booty.UI
{
    /// <summary>
    /// Main menu controller for the MainMenu.unity scene.
    /// Constructs the entire UI programmatically at runtime.
    /// Place on a single empty "MainMenuUI" GameObject in MainMenu.unity.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Private UI References
        // ══════════════════════════════════════════════════════════════════

        private GameObject _settingsPanel;
        private Slider     _volumeSlider;
        private Text       _feedbackText;
        private Button     _continueBtn;
        private float      _feedbackTimer;

        // ══════════════════════════════════════════════════════════════════
        //  Pirate palette
        // ══════════════════════════════════════════════════════════════════

        private static readonly Color BgColor       = new Color(0.05f, 0.08f, 0.15f, 1f);
        private static readonly Color GoldColor     = new Color(1f,   0.85f, 0.2f,  1f);
        private static readonly Color GoldDim       = new Color(0.8f, 0.65f, 0.1f,  0.6f);
        private static readonly Color BtnNormal     = new Color(0.10f, 0.15f, 0.25f, 0.92f);
        private static readonly Color BtnHover      = new Color(0.20f, 0.30f, 0.45f, 0.98f);
        private static readonly Color BtnPress      = new Color(0.06f, 0.10f, 0.18f, 1f);
        private static readonly Color BtnDisabled   = new Color(0.15f, 0.18f, 0.22f, 0.55f);
        private static readonly Color PanelBg       = new Color(0.04f, 0.07f, 0.14f, 0.97f);

        private const float BtnW  = 280f;
        private const float BtnH  = 52f;
        private const float BtnGap = 12f;

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
        //  Button actions
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
            var canvasGO = new GameObject("MainMenuUI_Canvas");
            canvasGO.transform.SetParent(transform, false);

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            canvasGO.AddComponent<GraphicRaycaster>();

            // ── Full-screen background (dark ocean) ──────────────────────
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(canvasGO.transform, false);
            var bgRect = bgGO.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            bgGO.AddComponent<Image>().color = BgColor;

            // ── Title: "BOOTY!" ──────────────────────────────────────────
            var titleText = MakeText(canvasGO, "Title", "BOOTY!",
                new Vector2(0.5f, 0.5f), new Vector2(0f, 140f), new Vector2(700f, 90f),
                64, FontStyle.Bold, GoldColor, TextAnchor.MiddleCenter);
            AddOutline(titleText.gameObject, new Color(0.5f, 0.3f, 0f, 0.8f));
            AddShadow(titleText.gameObject);

            // ── Subtitle ─────────────────────────────────────────────────
            MakeText(canvasGO, "Subtitle", "A Pirate's Rise",
                new Vector2(0.5f, 0.5f), new Vector2(0f, 88f), new Vector2(500f, 36f),
                22, FontStyle.Italic, new Color(0.85f, 0.85f, 0.85f),
                TextAnchor.MiddleCenter);

            // ── Gold divider ─────────────────────────────────────────────
            var divGO = new GameObject("Divider");
            divGO.transform.SetParent(canvasGO.transform, false);
            var divRT = divGO.AddComponent<RectTransform>();
            divRT.anchorMin = new Vector2(0.5f, 0.5f);
            divRT.anchorMax = new Vector2(0.5f, 0.5f);
            divRT.pivot = new Vector2(0.5f, 0.5f);
            divRT.anchoredPosition = new Vector2(0f, 58f);
            divRT.sizeDelta = new Vector2(340f, 2f);
            divGO.AddComponent<Image>().color = GoldDim;

            // ── Menu buttons (stacked from center down) ───────────────────
            float startY = 20f;
            MakeMenuButton(canvasGO, "Start Game",
                new Vector2(0f, startY - 0 * (BtnH + BtnGap)), StartGame);
            _continueBtn = MakeMenuButton(canvasGO, "Continue",
                new Vector2(0f, startY - 1 * (BtnH + BtnGap)), ContinueGame);
            MakeMenuButton(canvasGO, "Settings",
                new Vector2(0f, startY - 2 * (BtnH + BtnGap)), ToggleSettings);
            MakeMenuButton(canvasGO, "Quit",
                new Vector2(0f, startY - 3 * (BtnH + BtnGap)), QuitGame);

            // Dim Continue button if no save file
            if (!HasSaveFile() && _continueBtn != null)
            {
                var img = _continueBtn.GetComponent<Image>();
                if (img != null) img.color = BtnDisabled;
                var cb = _continueBtn.colors;
                cb.normalColor    = BtnDisabled;
                cb.highlightedColor = BtnDisabled;
                _continueBtn.colors = cb;
            }

            // ── Feedback text ─────────────────────────────────────────────
            _feedbackText = MakeText(canvasGO, "Feedback", "",
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, startY - 4 * (BtnH + BtnGap) - 10f),
                new Vector2(360f, 30f),
                16, FontStyle.Normal, new Color(1f, 0.35f, 0.35f),
                TextAnchor.MiddleCenter);

            // ── Version + copyright ───────────────────────────────────────
            MakeText(canvasGO, "Version", "v0.1.0 Pre-Alpha",
                new Vector2(0f, 0f), new Vector2(10f, 22f), new Vector2(200f, 22f),
                12, FontStyle.Normal, new Color(0.5f, 0.5f, 0.5f),
                TextAnchor.MiddleLeft);

            MakeText(canvasGO, "Copyright", "\u00A9 2026 Booty Studios",
                new Vector2(1f, 0f), new Vector2(-10f, 22f), new Vector2(200f, 22f),
                12, FontStyle.Normal, new Color(0.5f, 0.5f, 0.5f),
                TextAnchor.MiddleRight);

            // ── Settings panel (hidden by default) ────────────────────────
            _settingsPanel = BuildSettingsPanel(canvasGO);
            _settingsPanel.SetActive(false);
        }

        private GameObject BuildSettingsPanel(GameObject parent)
        {
            // Dark overlay
            var overlayGO = new GameObject("Settings_Overlay");
            overlayGO.transform.SetParent(parent.transform, false);
            var oRect = overlayGO.AddComponent<RectTransform>();
            oRect.anchorMin = Vector2.zero; oRect.anchorMax = Vector2.one;
            oRect.offsetMin = Vector2.zero; oRect.offsetMax = Vector2.zero;
            overlayGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            // Panel box
            var panelGO = new GameObject("Settings_Panel");
            panelGO.transform.SetParent(overlayGO.transform, false);
            var pRect = panelGO.AddComponent<RectTransform>();
            pRect.anchorMin = new Vector2(0.5f, 0.5f); pRect.anchorMax = new Vector2(0.5f, 0.5f);
            pRect.pivot = new Vector2(0.5f, 0.5f);
            pRect.anchoredPosition = Vector2.zero; pRect.sizeDelta = new Vector2(400f, 280f);
            panelGO.AddComponent<Image>().color = PanelBg;
            AddOutline(panelGO, GoldDim);

            // Title
            MakeText(panelGO, "Settings_Title", "SETTINGS",
                new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(360f, 36f),
                26, FontStyle.Bold, GoldColor, TextAnchor.MiddleCenter);

            // Volume label
            MakeText(panelGO, "Vol_Label", "Master Volume",
                new Vector2(0.5f, 0.5f), new Vector2(0f, 50f), new Vector2(300f, 30f),
                18, FontStyle.Normal, Color.white, TextAnchor.MiddleCenter);

            // Volume slider
            _volumeSlider = BuildSlider(panelGO, new Vector2(0f, 10f), new Vector2(300f, 30f));
            _volumeSlider.minValue = 0f;
            _volumeSlider.maxValue = 100f;
            _volumeSlider.value    = PlayerPrefs.GetFloat("MasterVolume", 80f);
            _volumeSlider.onValueChanged.AddListener(OnVolumeChanged);

            // Controls hint
            MakeText(panelGO, "Controls_Label", "WASD move  \u00b7  Space fire  \u00b7  Esc pause",
                new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(340f, 26f),
                14, FontStyle.Italic, new Color(0.7f, 0.7f, 0.7f),
                TextAnchor.MiddleCenter);

            // Back button
            var backBtn = MakeButton(panelGO, "Back", new Vector2(0f, -80f),
                new Vector2(160f, 44f));
            backBtn.GetComponentInChildren<Text>().text = "BACK";
            backBtn.onClick.AddListener(() => _settingsPanel.SetActive(false));

            return overlayGO;
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

        private Button MakeMenuButton(GameObject parent, string label, Vector2 pos,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(BtnW, BtnH);

            go.AddComponent<Image>().color = BtnNormal;

            var btn = go.AddComponent<Button>();
            var cb  = btn.colors;
            cb.normalColor      = BtnNormal;
            cb.highlightedColor = BtnHover;
            cb.pressedColor     = BtnPress;
            cb.disabledColor    = BtnDisabled;
            btn.colors = cb;
            btn.onClick.AddListener(onClick);

            AddOutline(go, GoldDim);

            var lGO = new GameObject("Label");
            lGO.transform.SetParent(go.transform, false);
            var lRT = lGO.AddComponent<RectTransform>();
            lRT.anchorMin = Vector2.zero; lRT.anchorMax = Vector2.one;
            lRT.offsetMin = Vector2.zero; lRT.offsetMax = Vector2.zero;
            var txt = lGO.AddComponent<Text>();
            txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.text      = label.ToUpper();
            txt.fontSize  = 20;
            txt.fontStyle = FontStyle.Bold;
            txt.color     = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;

            return btn;
        }

        private Button MakeButton(GameObject parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Btn_" + name);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;

            go.AddComponent<Image>().color = BtnNormal;

            var btn = go.AddComponent<Button>();
            var cb  = btn.colors;
            cb.normalColor      = BtnNormal;
            cb.highlightedColor = BtnHover;
            cb.pressedColor     = BtnPress;
            btn.colors = cb;

            AddOutline(go, GoldDim);

            var lGO = new GameObject("Label");
            lGO.transform.SetParent(go.transform, false);
            var lRT = lGO.AddComponent<RectTransform>();
            lRT.anchorMin = Vector2.zero; lRT.anchorMax = Vector2.one;
            lRT.offsetMin = Vector2.zero; lRT.offsetMax = Vector2.zero;
            var txt = lGO.AddComponent<Text>();
            txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize  = 17;
            txt.fontStyle = FontStyle.Bold;
            txt.color     = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;

            return btn;
        }

        private Slider BuildSlider(GameObject parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("VolumeSlider");
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var slider = go.AddComponent<Slider>();
            slider.minValue = 0f; slider.maxValue = 100f;

            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(go.transform, false);
            var bgRT = bgGO.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = new Vector2(0f, 8f); bgRT.offsetMax = new Vector2(0f, -8f);
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.15f, 0.15f, 0.25f, 0.9f);
            slider.targetGraphic = bgImg;

            var fillAreaGO = new GameObject("Fill Area");
            fillAreaGO.transform.SetParent(go.transform, false);
            var faRT = fillAreaGO.AddComponent<RectTransform>();
            faRT.anchorMin = Vector2.zero; faRT.anchorMax = Vector2.one;
            faRT.offsetMin = new Vector2(5f, 8f); faRT.offsetMax = new Vector2(-5f, -8f);

            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(fillAreaGO.transform, false);
            var fRT = fillGO.AddComponent<RectTransform>();
            fRT.anchorMin = Vector2.zero; fRT.anchorMax = Vector2.one;
            fRT.offsetMin = Vector2.zero; fRT.offsetMax = Vector2.zero;
            fillGO.AddComponent<Image>().color = GoldDim;
            slider.fillRect = fRT;

            var handleAreaGO = new GameObject("Handle Slide Area");
            handleAreaGO.transform.SetParent(go.transform, false);
            var haRT = handleAreaGO.AddComponent<RectTransform>();
            haRT.anchorMin = Vector2.zero; haRT.anchorMax = Vector2.one;
            haRT.offsetMin = new Vector2(10f, 0f); haRT.offsetMax = new Vector2(-10f, 0f);

            var handleGO = new GameObject("Handle");
            handleGO.transform.SetParent(handleAreaGO.transform, false);
            var hRT = handleGO.AddComponent<RectTransform>();
            hRT.sizeDelta = new Vector2(20f, 30f);
            handleGO.AddComponent<Image>().color = GoldColor;
            slider.handleRect = hRT;

            return slider;
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
            s.effectColor    = new Color(0f, 0f, 0f, 0.8f);
            s.effectDistance = new Vector2(2f, -2f);
        }
    }
}
