// ---------------------------------------------------------------------------
// GameOverUI.cs — Game-over overlay shown when the player ship is destroyed
// ---------------------------------------------------------------------------
// Call Show() to display the game-over screen (pauses time).
// Respawn button: reloads the active scene (safest approach since HPSystem
// destroys the player GameObject after the death animation completes).
// Quit button: loads MainMenu scene or quits the application.
// ---------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Booty.Save;

namespace Booty.UI
{
    /// <summary>
    /// Full-screen game-over overlay. Wired from BootyBootstrap via
    /// HPSystem.OnDestroyed. Does NOT subscribe to that event itself —
    /// BootyBootstrap calls <see cref="Show"/> directly.
    /// </summary>
    public class GameOverUI : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  UI References
        // ══════════════════════════════════════════════════════════════════

        private GameObject _root;
        private Text       _goldText;
        private Text       _renownText;

        // ══════════════════════════════════════════════════════════════════
        //  Palette
        // ══════════════════════════════════════════════════════════════════

        private static readonly Color GoldColor = new Color(1f,   0.85f, 0.2f);
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
            Hide();
        }

        // ══════════════════════════════════════════════════════════════════
        //  Public API
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Show the game-over screen and pause time.
        /// Call from BootyBootstrap when HPSystem.OnDestroyed fires.
        /// </summary>
        public void Show()
        {
            // Populate stats from SaveSystem
            var saveSystem = FindObjectOfType<SaveSystem>();
            if (saveSystem != null && saveSystem.CurrentState != null)
            {
                var player = saveSystem.CurrentState.player;
                if (_goldText   != null) _goldText.text   = string.Format("Gold earned: {0:F0}", player.gold);
                if (_renownText != null) _renownText.text = string.Format("Renown: {0:F0}", player.renown);
            }

            _root.SetActive(true);
            Time.timeScale = 0f;
        }

        /// <summary>
        /// Hide the game-over screen and restore time scale.
        /// </summary>
        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
            Time.timeScale = 1f;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Button Actions
        // ══════════════════════════════════════════════════════════════════

        private void Respawn()
        {
            Time.timeScale = 1f;
            // Reload the current scene — safest approach since the player
            // GameObject is destroyed by the death animation.
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void QuitToMenu()
        {
            Time.timeScale = 1f;
            if (HasScene("MainMenu"))
                SceneManager.LoadScene("MainMenu");
            else
                Application.Quit();
        }

        private static bool HasScene(string sceneName)
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                if (path.Contains(sceneName)) return true;
            }
            return false;
        }

        // ══════════════════════════════════════════════════════════════════
        //  UI Construction
        // ══════════════════════════════════════════════════════════════════

        private void BuildUI()
        {
            // Canvas (highest sort order — must appear above everything)
            var canvasGO = new GameObject("GameOver_Canvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // Dark full-screen overlay
            var overlayGO = new GameObject("GameOver_Overlay");
            overlayGO.transform.SetParent(canvasGO.transform, false);
            var oRect = overlayGO.AddComponent<RectTransform>();
            oRect.anchorMin = Vector2.zero; oRect.anchorMax = Vector2.one;
            oRect.offsetMin = Vector2.zero; oRect.offsetMax = Vector2.zero;
            overlayGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.88f);

            _root = overlayGO;

            // Inner panel
            var panelGO = new GameObject("GameOver_Panel");
            panelGO.transform.SetParent(overlayGO.transform, false);
            var pRect = panelGO.AddComponent<RectTransform>();
            pRect.anchorMin = new Vector2(0.5f, 0.5f); pRect.anchorMax = new Vector2(0.5f, 0.5f);
            pRect.pivot = new Vector2(0.5f, 0.5f);
            pRect.anchoredPosition = Vector2.zero; pRect.sizeDelta = new Vector2(420f, 280f);
            panelGO.AddComponent<Image>().color = PanelBg;
            AddOutline(panelGO, GoldDim);

            // "SHIP LOST!" in red
            var titleT = MakeText(panelGO, "GameOver_Title", "SHIP LOST!",
                new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(380f, 50f),
                36, FontStyle.Bold, new Color(0.9f, 0.2f, 0.2f), TextAnchor.MiddleCenter);
            AddShadow(titleT.gameObject);

            // Subtitle
            MakeText(panelGO, "Subtitle", "The seas claim another captain\u2026",
                new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(360f, 30f),
                15, FontStyle.Italic, new Color(0.65f, 0.65f, 0.65f), TextAnchor.MiddleCenter);

            // Stats
            _goldText   = MakeText(panelGO, "Gold_Stat",   "Gold earned: 0",
                new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(340f, 28f),
                16, FontStyle.Normal, GoldColor, TextAnchor.MiddleCenter);

            _renownText = MakeText(panelGO, "Renown_Stat", "Renown: 0",
                new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(340f, 28f),
                16, FontStyle.Normal, new Color(0.7f, 0.9f, 1f), TextAnchor.MiddleCenter);

            // Buttons
            MakeButton(panelGO, "Respawn",      new Vector2(-80f, -80f), Respawn);
            MakeButton(panelGO, "Quit",         new Vector2( 80f, -80f), QuitToMenu);
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
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(160f, 46f);
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
