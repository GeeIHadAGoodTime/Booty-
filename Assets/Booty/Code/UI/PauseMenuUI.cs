// ---------------------------------------------------------------------------
// PauseMenuUI.cs — Esc-triggered pause overlay with Resume / Settings / Quit
// ---------------------------------------------------------------------------
// Self-contained: builds its own Canvas in Awake(), starts hidden.
// Esc toggles pause. If PortScreenUI is open, Esc closes that first.
// Quit to Menu saves state then loads MainMenu scene (falls back to Quit).
// ---------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Booty.Save;

namespace Booty.UI
{
    /// <summary>
    /// In-game pause menu. Toggles on Esc. Respects PortScreenUI priority.
    /// Constructs its own Canvas hierarchy — no prefab dependency.
    /// </summary>
    public class PauseMenuUI : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Private State
        // ══════════════════════════════════════════════════════════════════

        private GameObject _root;
        private GameObject _settingsPanel;
        private Slider     _volumeSlider;
        private bool       _isPaused;

        // ══════════════════════════════════════════════════════════════════
        //  Pirate palette
        // ══════════════════════════════════════════════════════════════════

        private static readonly Color GoldColor = new Color(1f, 0.85f, 0.2f);
        private static readonly Color GoldDim   = new Color(0.8f, 0.65f, 0.1f, 0.6f);
        private static readonly Color PanelBg   = new Color(0.04f, 0.07f, 0.14f, 0.97f);
        private static readonly Color BtnNormal = new Color(0.10f, 0.15f, 0.25f, 0.92f);
        private static readonly Color BtnHover  = new Color(0.20f, 0.30f, 0.45f, 0.98f);
        private static readonly Color BtnPress  = new Color(0.06f, 0.10f, 0.18f, 1f);

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Awake()
        {
            BuildUI();
            SetPaused(false);
        }

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;

            // Priority: close PortScreenUI first if open
            var portScreen = FindObjectOfType<PortScreenUI>();
            if (portScreen != null && portScreen.IsOpen)
            {
                portScreen.HidePortScreen();
                return;
            }

            TogglePause();
        }

        // ══════════════════════════════════════════════════════════════════
        //  Public API
        // ══════════════════════════════════════════════════════════════════

        public void TogglePause()
        {
            SetPaused(!_isPaused);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Internal
        // ══════════════════════════════════════════════════════════════════

        private void SetPaused(bool paused)
        {
            _isPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            _root.SetActive(paused);
            if (_settingsPanel != null && !paused)
                _settingsPanel.SetActive(false);
        }

        private void Resume()
        {
            SetPaused(false);
        }

        private void ShowSettingsPanel()
        {
            if (_settingsPanel != null)
                _settingsPanel.SetActive(true);
        }

        private void HideSettingsPanel()
        {
            if (_settingsPanel != null)
                _settingsPanel.SetActive(false);
        }

        private void QuitToMenu()
        {
            Time.timeScale = 1f;
            FindObjectOfType<SaveSystem>()?.SaveCurrent();
            if (HasMainMenuScene())
                SceneManager.LoadScene("MainMenu");
            else
                Application.Quit();
        }

        private static bool HasMainMenuScene()
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
                if (path.Contains("MainMenu")) return true;
            }
            return false;
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
            // Root canvas (above HUD)
            var canvasGO = new GameObject("PauseMenu_Canvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // Dim overlay
            var overlayGO = new GameObject("Overlay");
            overlayGO.transform.SetParent(canvasGO.transform, false);
            var oRect = overlayGO.AddComponent<RectTransform>();
            oRect.anchorMin = Vector2.zero; oRect.anchorMax = Vector2.one;
            oRect.offsetMin = Vector2.zero; oRect.offsetMax = Vector2.zero;
            overlayGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            // Panel
            var panelGO = new GameObject("PausePanel");
            panelGO.transform.SetParent(overlayGO.transform, false);
            var pRect = panelGO.AddComponent<RectTransform>();
            pRect.anchorMin = new Vector2(0.5f, 0.5f); pRect.anchorMax = new Vector2(0.5f, 0.5f);
            pRect.pivot = new Vector2(0.5f, 0.5f);
            pRect.anchoredPosition = Vector2.zero; pRect.sizeDelta = new Vector2(320f, 260f);
            panelGO.AddComponent<Image>().color = PanelBg;
            AddOutline(panelGO, GoldDim);

            // Title
            MakeText(panelGO, "Paused_Title", "PAUSED",
                new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(280f, 40f),
                28, FontStyle.Bold, GoldColor, TextAnchor.MiddleCenter);

            // Buttons
            MakeButton(panelGO, "Resume",      new Vector2(0f,  60f), Resume);
            MakeButton(panelGO, "Settings",    new Vector2(0f,   4f), ShowSettingsPanel);
            MakeButton(panelGO, "Quit To Menu",new Vector2(0f, -52f), QuitToMenu);

            _root = overlayGO;

            // Settings sub-panel (hidden)
            _settingsPanel = BuildSettingsSubPanel(overlayGO);
            _settingsPanel.SetActive(false);
        }

        private GameObject BuildSettingsSubPanel(GameObject parent)
        {
            var spGO = new GameObject("Settings_Subpanel");
            spGO.transform.SetParent(parent.transform, false);
            var spRect = spGO.AddComponent<RectTransform>();
            spRect.anchorMin = new Vector2(0.5f, 0.5f); spRect.anchorMax = new Vector2(0.5f, 0.5f);
            spRect.pivot = new Vector2(0.5f, 0.5f);
            spRect.anchoredPosition = Vector2.zero; spRect.sizeDelta = new Vector2(340f, 200f);
            spGO.AddComponent<Image>().color = PanelBg;
            AddOutline(spGO, GoldDim);

            MakeText(spGO, "SP_Title", "SETTINGS",
                new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(300f, 34f),
                22, FontStyle.Bold, GoldColor, TextAnchor.MiddleCenter);

            MakeText(spGO, "Vol_Label", "Master Volume",
                new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(280f, 28f),
                16, FontStyle.Normal, Color.white, TextAnchor.MiddleCenter);

            _volumeSlider = BuildSlider(spGO, new Vector2(0f, 4f), new Vector2(280f, 28f));
            _volumeSlider.value = PlayerPrefs.GetFloat("MasterVolume", 80f);
            _volumeSlider.onValueChanged.AddListener(OnVolumeChanged);

            MakeButton(spGO, "Back", new Vector2(0f, -52f), HideSettingsPanel);

            return spGO;
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
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(240f, 44f);
            go.AddComponent<Image>().color = BtnNormal;
            var btn = go.AddComponent<Button>();
            var cb = btn.colors;
            cb.normalColor = BtnNormal; cb.highlightedColor = BtnHover;
            cb.pressedColor = BtnPress;
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
            txt.text = label.ToUpper(); txt.fontSize = 16; txt.fontStyle = FontStyle.Bold;
            txt.color = Color.white; txt.alignment = TextAnchor.MiddleCenter;
            return btn;
        }

        private Slider BuildSlider(GameObject parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Slider");
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var slider = go.AddComponent<Slider>();
            slider.minValue = 0f; slider.maxValue = 100f;

            var bgGO = new GameObject("BG");
            bgGO.transform.SetParent(go.transform, false);
            var bgRT = bgGO.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = new Vector2(0f, 6f); bgRT.offsetMax = new Vector2(0f, -6f);
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.3f);
            slider.targetGraphic = bgImg;

            var fillAreaGO = new GameObject("FillArea");
            fillAreaGO.transform.SetParent(go.transform, false);
            var faRT = fillAreaGO.AddComponent<RectTransform>();
            faRT.anchorMin = Vector2.zero; faRT.anchorMax = Vector2.one;
            faRT.offsetMin = new Vector2(5f, 6f); faRT.offsetMax = new Vector2(-5f, -6f);
            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(fillAreaGO.transform, false);
            var fRT = fillGO.AddComponent<RectTransform>();
            fRT.anchorMin = Vector2.zero; fRT.anchorMax = Vector2.one;
            fRT.offsetMin = Vector2.zero; fRT.offsetMax = Vector2.zero;
            fillGO.AddComponent<Image>().color = new Color(0.8f, 0.65f, 0.1f);
            slider.fillRect = fRT;

            var handleAreaGO = new GameObject("HandleArea");
            handleAreaGO.transform.SetParent(go.transform, false);
            var haRT = handleAreaGO.AddComponent<RectTransform>();
            haRT.anchorMin = Vector2.zero; haRT.anchorMax = Vector2.one;
            haRT.offsetMin = new Vector2(10f, 0f); haRT.offsetMax = new Vector2(-10f, 0f);
            var handleGO = new GameObject("Handle");
            handleGO.transform.SetParent(handleAreaGO.transform, false);
            var hRT = handleGO.AddComponent<RectTransform>();
            hRT.sizeDelta = new Vector2(18f, 28f);
            handleGO.AddComponent<Image>().color = new Color(1f, 0.85f, 0.2f);
            slider.handleRect = hRT;

            return slider;
        }

        private static void AddOutline(GameObject go, Color color)
        {
            var o = go.AddComponent<Outline>();
            o.effectColor = color; o.effectDistance = new Vector2(1f, -1f);
        }
    }
}
