// ---------------------------------------------------------------------------
// FloatingDamageNumber.cs — Floating damage text that rises and fades
// ---------------------------------------------------------------------------
// Spawned by HPSystem.TakeDamage() at the hit position.
// Uses a World Space Canvas. Self-destructs after animation.
// ---------------------------------------------------------------------------

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Booty.UI
{
    /// <summary>
    /// Floating damage number that rises and fades above a hit position.
    /// Created procedurally — no prefab needed.
    /// </summary>
    public class FloatingDamageNumber : MonoBehaviour
    {
        private Text _label;
        private RectTransform _rect;

        private const float LifeTime    = 1.2f;
        private const float RiseSpeed   = 2.5f;
        private const float FontSz      = 20;

        /// <summary>
        /// Spawn a floating damage number at a world position.
        /// </summary>
        public static FloatingDamageNumber Spawn(Vector3 worldPosition, int damage, bool isPlayer)
        {
            // World-space canvas
            var canvasGO = new GameObject("DamageNumber");
            canvasGO.transform.position = worldPosition + Vector3.up * 1.5f;

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20;

            var rt = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(2f, 1f);
            canvasGO.transform.localScale = Vector3.one * 0.02f;

            // Text label
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(canvasGO.transform, false);

            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(100f, 50f);
            labelRect.anchoredPosition = Vector2.zero;

            var text = labelGO.AddComponent<Text>();
            text.text      = damage.ToString();
            text.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize  = (int)FontSz;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            // Red for player damage, yellow for enemy damage
            text.color = isPlayer ? new Color(1f, 0.2f, 0.2f) : new Color(1f, 0.9f, 0.1f);

            var shadow = labelGO.AddComponent<Shadow>();
            shadow.effectColor    = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(1f, -1f);

            var fn = canvasGO.AddComponent<FloatingDamageNumber>();
            fn._label = text;
            fn._rect  = rt;

            return fn;
        }

        private void Start()
        {
            StartCoroutine(FloatAndFade());
        }

        private IEnumerator FloatAndFade()
        {
            float elapsed = 0f;
            Color startColor = _label.color;

            while (elapsed < LifeTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / LifeTime;

                // Rise
                transform.position += Vector3.up * RiseSpeed * Time.deltaTime;

                // Fade out
                _label.color = new Color(startColor.r, startColor.g, startColor.b, 1f - t);

                // Face camera
                if (Camera.main != null)
                    transform.LookAt(Camera.main.transform);

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
