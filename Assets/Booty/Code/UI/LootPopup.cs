// ---------------------------------------------------------------------------
// LootPopup.cs — Floating "+X gold" popup when enemy is destroyed
// ---------------------------------------------------------------------------
// Attach to enemy GameObjects. Self-subscribes to HPSystem.OnDestroyed.
// ---------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace Booty.UI
{
    /// <summary>
    /// Displays a "+X gold" floating popup when the attached ship is destroyed.
    /// Self-wires to HPSystem.OnDestroyed in Start().
    /// </summary>
    public class LootPopup : MonoBehaviour
    {
        [SerializeField] private int goldAmount = 50; // set by spawner

        private void Start()
        {
            var hp = GetComponent<Booty.Combat.HPSystem>();
            if (hp != null)
                hp.OnDestroyed += ShowPopup;
        }

        private void OnDestroy()
        {
            var hp = GetComponent<Booty.Combat.HPSystem>();
            if (hp != null)
                hp.OnDestroyed -= ShowPopup;
        }

        /// <summary>Set the gold reward before the ship can die.</summary>
        public void Configure(int gold) => goldAmount = gold;

        private void ShowPopup()
        {
            // Spawn a world-space "+X gold" canvas at current position
            var go = new GameObject("LootPopup");
            go.transform.position = transform.position + Vector3.up * 2f;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 25;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(3f, 1f);
            go.transform.localScale = Vector3.one * 0.025f;

            var labelGO = new GameObject("Text");
            labelGO.transform.SetParent(go.transform, false);

            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(120f, 60f);
            labelRect.anchoredPosition = Vector2.zero;

            var text = labelGO.AddComponent<Text>();
            text.text      = string.Format("+{0} gold", goldAmount);
            text.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize  = 22;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color     = new Color(1f, 0.85f, 0.1f); // gold yellow

            var shadow = labelGO.AddComponent<Shadow>();
            shadow.effectColor    = new Color(0f, 0f, 0f, 1f);
            shadow.effectDistance = new Vector2(2f, -2f);

            go.AddComponent<LootPopupMover>();
        }
    }

    /// <summary>Animates the loot popup — rise and fade.</summary>
    internal class LootPopupMover : MonoBehaviour
    {
        private float _elapsed;
        private const float Duration = 1.8f;
        private const float RiseSpeed = 3f;
        private Text _text;

        private void Start()
        {
            _text = GetComponentInChildren<Text>();
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = _elapsed / Duration;

            transform.position += Vector3.up * RiseSpeed * Time.deltaTime;
            if (Camera.main != null)
                transform.LookAt(Camera.main.transform);

            if (_text != null)
            {
                Color c = _text.color;
                c.a = 1f - t;
                _text.color = c;
            }

            if (_elapsed >= Duration)
                Destroy(gameObject);
        }
    }
}
