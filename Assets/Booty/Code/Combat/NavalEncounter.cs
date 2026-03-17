// ---------------------------------------------------------------------------
// NavalEncounter.cs — Pre-battle parley dialogue: Fight / Surrender / Flee
// ---------------------------------------------------------------------------
// Attach to enemy ships (done by BootyBootstrap or AI.EnemySpawner).
// Wire references with Initialize(); the component self-wires via Start()
// if Initialize() is not called.
//
// Trigger behaviour:
//   When the player enters detection range (default = AggroDistance) for the
//   FIRST TIME, the AI is frozen and a 3-button overlay appears:
//
//   [Fight!]            — resume AI; normal broadside combat begins
//   [Surrender Cargo]   — player loses cargoDemandFraction of each good;
//                         enemy triggers its Flee state and departs
//   [Attempt to Flee]   — success chance based on playerSpeed / enemySpeed;
//                         success: enemy stands down & despawns;
//                         failure: AI resumes and combat begins
//
// The component disables itself after one encounter so it cannot trigger again.
// ---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Booty.Ships;
using Booty.Economy;

namespace Booty.Combat
{
    /// <summary>
    /// Result of a naval encounter dialogue choice.
    /// </summary>
    public enum EncounterOutcome
    {
        /// <summary>Player chose to fight — AI resumes normally.</summary>
        Fight,
        /// <summary>Player surrendered cargo — enemy retreats.</summary>
        SurrenderCargo,
        /// <summary>Player successfully fled — no combat.</summary>
        FleeSuccess,
        /// <summary>Player tried to flee but failed — combat starts.</summary>
        FleeFailed,
    }

    /// <summary>
    /// Presents a pre-battle choice overlay when the player enters aggro range.
    /// Requires <see cref="HPSystem"/> on the same GameObject.
    /// </summary>
    [RequireComponent(typeof(HPSystem))]
    public class NavalEncounter : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Inspector
        // ══════════════════════════════════════════════════════════════════

        [Header("Detection")]
        [Tooltip("Distance from the player that triggers the encounter.")]
        [SerializeField] private float triggerDistance = CombatConfig.AggroDistance;

        [Header("Surrender Terms")]
        [Tooltip("Fraction (0–1) of each cargo type the enemy demands on surrender. " +
                 "Default 0.5 = half the hold.")]
        [Range(0f, 1f)]
        [SerializeField] private float cargoDemandFraction = 0.5f;

        [Header("Enemy Speed (for flee calculation)")]
        [Tooltip("Enemy ship max speed in world units per second. " +
                 "Used to calculate the player's flee success probability.")]
        [SerializeField] private float enemyMaxSpeed = 8f;

        [Header("Dialogue Text")]
        [SerializeField] private string hailText      = "Halt! Strike your colours or face our cannons!";
        [SerializeField] private string fightLabel     = "Fight!";
        [SerializeField] private string surrenderLabel = "Surrender Cargo";
        [SerializeField] private string fleeLabel      = "Attempt to Flee";

        // ══════════════════════════════════════════════════════════════════
        //  References
        // ══════════════════════════════════════════════════════════════════

        private Transform      _playerTransform;
        private ShipController _playerShip;
        private CargoInventory _cargoInventory;
        private HPSystem       _hp;

        // Enemy AI components — frozen during dialogue, then resumed or left as-is
        private Booty.AI.EnemyShipAI _advancedAI; // preferred
        private Booty.Ships.EnemyAI  _legacyAI;   // fallback for ships that use the old AI

        // ══════════════════════════════════════════════════════════════════
        //  State
        // ══════════════════════════════════════════════════════════════════

        private bool       _triggered;
        private bool       _dialogueOpen;
        private GameObject _dialogueCanvas;

        // ══════════════════════════════════════════════════════════════════
        //  Events
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Fired when the encounter resolves with the chosen outcome.
        /// Listen to this for quest tracking, analytics, etc.
        /// </summary>
        public event Action<EncounterOutcome> OnEncounterResolved;

        // ══════════════════════════════════════════════════════════════════
        //  Public API
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Wire player-side references. Call from BootyBootstrap or AI.EnemySpawner
        /// immediately after instantiating the enemy ship.
        /// </summary>
        public void Initialize(Transform playerTransform, ShipController playerShip,
                               CargoInventory cargoInventory)
        {
            _playerTransform = playerTransform;
            _playerShip      = playerShip;
            _cargoInventory  = cargoInventory;

            CacheComponents();
        }

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Start()
        {
            CacheComponents();

            // Self-wire player references when Initialize() was not called
            if (_playerTransform == null)
            {
                var playerGO = GameObject.FindWithTag("Player");
                if (playerGO != null)
                {
                    _playerTransform = playerGO.transform;
                    _playerShip      = playerGO.GetComponent<ShipController>();
                    _cargoInventory  = playerGO.GetComponent<CargoInventory>()
                                      ?? FindObjectOfType<CargoInventory>();
                }
            }
        }

        private void CacheComponents()
        {
            if (_hp          == null) _hp          = GetComponent<HPSystem>();
            if (_advancedAI  == null) _advancedAI  = GetComponent<Booty.AI.EnemyShipAI>();
            if (_legacyAI    == null) _legacyAI    = GetComponent<Booty.Ships.EnemyAI>();
        }

        private void Update()
        {
            if (_triggered || _dialogueOpen)   return;
            if (_hp != null && _hp.IsDead)     return;
            if (_playerTransform == null)       return;

            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            if (dist <= triggerDistance)
            {
                _triggered = true;
                OpenDialogue();
            }
        }

        private void OnDestroy()
        {
            CloseDialogueUI();
        }

        // ══════════════════════════════════════════════════════════════════
        //  Dialogue Flow
        // ══════════════════════════════════════════════════════════════════

        private void OpenDialogue()
        {
            _dialogueOpen = true;

            // Freeze enemy AI so the ship holds position during dialogue
            SetAIEnabled(false);

            _dialogueCanvas = BuildDialogueCanvas();
            Debug.Log($"[NavalEncounter] Dialogue opened — {name} hails the player.");
        }

        private void ResolveEncounter(EncounterOutcome outcome)
        {
            CloseDialogueUI();
            Debug.Log($"[NavalEncounter] Outcome: {outcome}");
            OnEncounterResolved?.Invoke(outcome);

            switch (outcome)
            {
                case EncounterOutcome.Fight:
                    // Resume AI — enter normal broadside combat
                    SetAIEnabled(true);
                    break;

                case EncounterOutcome.SurrenderCargo:
                    TakeCargo();
                    BeginRetreat();
                    break;

                case EncounterOutcome.FleeSuccess:
                    // Player escaped — despawn this enemy quietly
                    gameObject.SetActive(false);
                    Destroy(gameObject, 0.1f);
                    break;

                case EncounterOutcome.FleeFailed:
                    // Failed to run — combat begins
                    SetAIEnabled(true);
                    Debug.Log("[NavalEncounter] Flee failed — combat begins.");
                    break;
            }

            enabled = false; // prevent any re-trigger
        }

        // ══════════════════════════════════════════════════════════════════
        //  Cargo Surrender
        // ══════════════════════════════════════════════════════════════════

        private void TakeCargo()
        {
            if (_cargoInventory == null) return;

            // Snapshot entries (can't modify during iteration)
            var entries = new List<CargoEntry>(_cargoInventory.Items);
            foreach (var entry in entries)
            {
                int qty = Mathf.CeilToInt(entry.quantity * cargoDemandFraction);
                if (qty > 0)
                    _cargoInventory.RemoveGoods(entry.goods, qty);
            }

            Debug.Log($"[NavalEncounter] Cargo seized ({cargoDemandFraction:P0} of hold).");
        }

        // ══════════════════════════════════════════════════════════════════
        //  Enemy Retreat
        // ══════════════════════════════════════════════════════════════════

        private void BeginRetreat()
        {
            // Use EnemyShipAI's TriggerFlee() if available (preferred path)
            if (_advancedAI != null)
            {
                _advancedAI.enabled = true;
                _advancedAI.TriggerFlee();
                // Destroy the retreating ship after a generous window
                Destroy(gameObject, 30f);
                return;
            }

            // Legacy EnemyAI doesn't expose TriggerFlee — just despawn
            if (_legacyAI != null)
            {
                _legacyAI.enabled = false;
                Destroy(gameObject, 3f);
                return;
            }

            // No AI component — destroy directly
            Destroy(gameObject, 2f);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Flee Attempt
        // ══════════════════════════════════════════════════════════════════

        private EncounterOutcome AttemptFlee()
        {
            // Base chance on player speed vs enemy speed
            float playerSpeed = _playerShip != null ? _playerShip.CurrentSpeed : 8f;
            float maxSpeed    = Mathf.Max(playerSpeed, enemyMaxSpeed, 0.1f);
            float speedRatio  = playerSpeed / maxSpeed;

            // Mapped to 10 %–90 % success range with some randomness
            float fleeChance = Mathf.Clamp(speedRatio * 0.8f + 0.1f, 0.1f, 0.9f);
            bool  success    = Random.value < fleeChance;

            Debug.Log($"[NavalEncounter] Flee attempt: playerSpeed={playerSpeed:F1} " +
                      $"enemySpeed={enemyMaxSpeed:F1} chance={fleeChance:P0} " +
                      $"result={( success ? "SUCCESS" : "FAIL" )}");

            return success ? EncounterOutcome.FleeSuccess : EncounterOutcome.FleeFailed;
        }

        // ══════════════════════════════════════════════════════════════════
        //  AI Enable / Disable
        // ══════════════════════════════════════════════════════════════════

        private void SetAIEnabled(bool value)
        {
            if (_advancedAI != null) _advancedAI.enabled = value;
            if (_legacyAI   != null) _legacyAI.enabled   = value;
        }

        // ══════════════════════════════════════════════════════════════════
        //  UI Construction
        // ══════════════════════════════════════════════════════════════════

        private GameObject BuildDialogueCanvas()
        {
            // Root canvas — screen space so it always reads clearly
            var rootGO = new GameObject("NavalEncounterCanvas");
            var canvas = rootGO.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            rootGO.AddComponent<CanvasScaler>();
            rootGO.AddComponent<GraphicRaycaster>();

            // Semi-transparent dark panel
            var panelGO  = new GameObject("Panel");
            panelGO.transform.SetParent(rootGO.transform, false);

            var panelImg = panelGO.AddComponent<Image>();
            panelImg.color = new Color(0.04f, 0.04f, 0.16f, 0.90f);

            var panelRT = panelGO.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.25f, 0.28f);
            panelRT.anchorMax = new Vector2(0.75f, 0.72f);
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;

            // Hail message
            AddText(panelGO, hailText,
                    anchorMin: new Vector2(0.05f, 0.70f),
                    anchorMax: new Vector2(0.95f, 0.95f),
                    fontSize: 18, bold: false, color: new Color(1f, 0.9f, 0.7f));

            // Divider line (thin panel)
            AddLine(panelGO, new Vector2(0.05f, 0.66f), new Vector2(0.95f, 0.67f));

            // Buttons
            AddButton(panelGO, fightLabel,
                      new Vector2(0.1f, 0.44f), new Vector2(0.9f, 0.62f),
                      new Color(0.55f, 0.12f, 0.12f, 0.95f),
                      () => ResolveEncounter(EncounterOutcome.Fight));

            AddButton(panelGO, surrenderLabel,
                      new Vector2(0.1f, 0.24f), new Vector2(0.9f, 0.42f),
                      new Color(0.15f, 0.40f, 0.65f, 0.95f),
                      () => ResolveEncounter(EncounterOutcome.SurrenderCargo));

            AddButton(panelGO, fleeLabel,
                      new Vector2(0.1f, 0.05f), new Vector2(0.9f, 0.22f),
                      new Color(0.20f, 0.45f, 0.20f, 0.95f),
                      () => ResolveEncounter(AttemptFlee()));

            return rootGO;
        }

        private void CloseDialogueUI()
        {
            if (_dialogueCanvas != null)
            {
                Destroy(_dialogueCanvas);
                _dialogueCanvas = null;
            }
            _dialogueOpen = false;
        }

        // ── UI Helper Methods ──────────────────────────────────────────

        private static void AddText(GameObject parent, string text,
                                    Vector2 anchorMin, Vector2 anchorMax,
                                    int fontSize, bool bold, Color color)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent.transform, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var t = go.AddComponent<Text>();
            t.text      = text;
            t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize  = fontSize;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.alignment = TextAnchor.MiddleCenter;
            t.color     = color;
        }

        private static void AddLine(GameObject parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject("Divider");
            go.transform.SetParent(parent.transform, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.8f, 0.7f, 0.4f, 0.6f); // gold divider
        }

        private static void AddButton(GameObject parent, string label,
                                      Vector2 anchorMin, Vector2 anchorMax,
                                      Color bgColor, Action onClick)
        {
            var go = new GameObject($"Btn_{label}");
            go.transform.SetParent(parent.transform, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = bgColor;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            // Button label text
            var labelGO = new GameObject("Text");
            labelGO.transform.SetParent(go.transform, false);

            var labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;

            var t = labelGO.AddComponent<Text>();
            t.text      = label;
            t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize  = 17;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color     = Color.white;

            btn.onClick.AddListener(new UnityEngine.Events.UnityAction(onClick));
        }
    }
}
