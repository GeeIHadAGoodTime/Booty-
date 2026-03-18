// ---------------------------------------------------------------------------
// LootDrop.cs — Physical loot that spawns at ship death and can be collected
// ---------------------------------------------------------------------------
// S3.1: Loot drops from defeated ships.
//
// Attach to enemy ships (done by BootyBootstrap). On HPSystem.OnDestroyed:
//   - 3 collectible loot crates are scattered around the death position.
//   - Each crate is a bright-coloured primitive with a sphere trigger.
//   - When the player's ship collider enters the trigger, gold is awarded
//     and a "+N gold" popup appears.
//   - Uncollected crates auto-despawn after 30 seconds.
//
// Crate values scale with the attached ship's EnemyMetadata.tier.
// ---------------------------------------------------------------------------

using System.Collections;
using UnityEngine;
using UnityEngine.UI;   // Text, Image, Shadow
using Booty.World;      // EnemyMetadata

namespace Booty.Combat
{
    /// <summary>
    /// Spawns collectible loot crates when the owning ship is destroyed.
    /// Attach to enemy ships before they can die.
    /// </summary>
    public class LootDrop : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Configuration
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Number of crates to scatter on death.</summary>
        private const int CrateCount = 3;

        /// <summary>Scatter radius around death position.</summary>
        private const float ScatterRadius = 6f;

        /// <summary>Seconds before uncollected crates despawn.</summary>
        private const float DespawnTime = 30f;

        /// <summary>Base gold value per crate (multiplied by tier).</summary>
        private const int BaseGoldPerCrate = 20;

        // ══════════════════════════════════════════════════════════════════
        //  References
        // ══════════════════════════════════════════════════════════════════

        private HPSystem _hp;
        private int      _tier = 1;

        // ══════════════════════════════════════════════════════════════════
        //  Public API
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Configure before the ship can die. Called by BootyBootstrap.
        /// </summary>
        public void Initialize(HPSystem hp, int tier = 1)
        {
            _hp   = hp;
            _tier = Mathf.Max(1, tier);

            if (_hp != null)
                _hp.OnDestroyed += SpawnLoot;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Start()
        {
            // Self-wire only when Initialize() was not explicitly called.
            if (_hp == null)
            {
                _hp = GetComponent<HPSystem>();
                var meta = GetComponent<EnemyMetadata>();
                if (meta != null) _tier = Mathf.Max(1, meta.tier);
                if (_hp != null) _hp.OnDestroyed += SpawnLoot;
            }
        }

        private void OnDestroy()
        {
            if (_hp != null)
                _hp.OnDestroyed -= SpawnLoot;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Loot Spawning
        // ══════════════════════════════════════════════════════════════════

        private void SpawnLoot()
        {
            Vector3 origin = transform.position;
            int goldPerCrate = BaseGoldPerCrate * _tier;

            for (int i = 0; i < CrateCount; i++)
            {
                // Random scatter on XZ plane
                Vector2 rand = Random.insideUnitCircle * ScatterRadius;
                Vector3 pos  = origin + new Vector3(rand.x, 0.3f, rand.y);

                SpawnCrate(pos, goldPerCrate);
            }
        }

        private static void SpawnCrate(Vector3 position, int goldValue)
        {
            // Visual: bright yellow cube
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "LootCrate";
            go.transform.position   = position;
            go.transform.localScale = Vector3.one * 0.6f;

            // Gold-yellow material
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                var lootShader = Shader.Find("Universal Render Pipeline/Lit")
                                ?? Shader.Find("Standard");
                if (lootShader == null)
                {
                    Debug.LogWarning("[LootDrop] No shader found for loot crate — using fallback.");
                    lootShader = Shader.Find("Hidden/InternalErrorShader");
                }
                var mat = new Material(lootShader);
                mat.color = new Color(1.0f, 0.80f, 0.1f); // gold
                rend.material = mat;
            }

            // Remove the solid collider and add a trigger sphere for collection
            var defaultCol = go.GetComponent<Collider>();
            if (defaultCol != null) Destroy(defaultCol);

            var trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius    = 1.2f; // generous pickup radius

            // Attach collector behaviour
            var collector = go.AddComponent<LootCrateCollector>();
            collector.GoldValue = goldValue;

            // Gentle bob animation
            go.AddComponent<LootCrateBobber>();

            // Auto-despawn after DespawnTime seconds
            Destroy(go, DespawnTime);
        }
    }

    // =========================================================================
    //  LootCrateCollector — Handles player collection on trigger enter
    // =========================================================================

    /// <summary>
    /// Attached to each loot crate. Detects the player ship and awards gold.
    /// </summary>
    internal class LootCrateCollector : MonoBehaviour
    {
        internal int GoldValue = 20;

        private bool _collected = false;

        private void OnTriggerEnter(Collider other)
        {
            if (_collected) return;
            if (!other.CompareTag("Player") && !other.transform.root.CompareTag("Player")) return;

            _collected = true;

            // Show floating popup
            SpawnCollectionPopup(transform.position + Vector3.up * 1.5f, GoldValue);

            // Play a quick flash/scale-up then destroy
            StartCoroutine(CollectAnimation());
        }

        private IEnumerator CollectAnimation()
        {
            float elapsed = 0f;
            float duration = 0.2f;
            Vector3 startScale = transform.localScale;

            // Disable trigger so it can't be collected again while animating
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                transform.localScale = Vector3.Lerp(startScale, startScale * 2f, t);
                transform.Rotate(0f, 360f * Time.deltaTime, 0f);
                yield return null;
            }

            Destroy(gameObject);
        }

        private static void SpawnCollectionPopup(Vector3 worldPos, int gold)
        {
            var go = new GameObject("LootCollectionPopup");
            go.transform.position = worldPos;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 25;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(3f, 1f);
            go.transform.localScale = Vector3.one * 0.025f;

            var labelGO = new GameObject("Text");
            labelGO.transform.SetParent(go.transform, false);

            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.sizeDelta        = new Vector2(130f, 50f);
            labelRect.anchoredPosition = Vector2.zero;

            var text = labelGO.AddComponent<UnityEngine.UI.Text>();
            text.text      = $"+{gold} gold";
            text.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize  = 20;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color     = new Color(1f, 0.85f, 0.1f); // gold yellow

            var shadow = labelGO.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor    = new Color(0f, 0f, 0f, 1f);
            shadow.effectDistance = new Vector2(2f, -2f);

            go.AddComponent<LootPopupFloater>();
        }
    }

    // =========================================================================
    //  LootCrateBobber — Gentle up-down bob and slow Y-rotation
    // =========================================================================

    /// <summary>Makes the loot crate bob up and down to attract attention.</summary>
    internal class LootCrateBobber : MonoBehaviour
    {
        private Vector3 _basePosition;
        private float   _phase;

        private void Start()
        {
            _basePosition = transform.position;
            _phase        = Random.Range(0f, Mathf.PI * 2f); // random phase offset
        }

        private void Update()
        {
            float bobY = Mathf.Sin(Time.time * 2.0f + _phase) * 0.2f;
            transform.position = _basePosition + Vector3.up * bobY;
            transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
        }
    }

    // =========================================================================
    //  LootPopupFloater — Rises and fades the collection text
    // =========================================================================

    /// <summary>Animates a loot collection popup — rises and fades over 1.8s.</summary>
    internal class LootPopupFloater : MonoBehaviour
    {
        private const float Duration  = 1.8f;
        private const float RiseSpeed = 2.0f;

        private float               _elapsed;
        private UnityEngine.UI.Text _text;

        private void Start()
        {
            _text = GetComponentInChildren<UnityEngine.UI.Text>();
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
                c.a = Mathf.Lerp(1f, 0f, t);
                _text.color = c;
            }

            if (_elapsed >= Duration)
                Destroy(gameObject);
        }
    }
}
