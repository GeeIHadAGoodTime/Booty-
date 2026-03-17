// ---------------------------------------------------------------------------
// BoardingSystem.cs — Player-initiated ship boarding resolution
// ---------------------------------------------------------------------------
// S3.1: Boarding action mini-game / resolution system.
//
// Attach to the PLAYER ship only. Each frame:
//   1. Scans for the nearest living enemy ship within BoardingRange.
//   2. If one is found, displays an on-screen "B — Board!" prompt.
//   3. On B press, runs a dice-based resolution:
//        - Success (configurable base 60%): enemy ship sinks immediately +
//          a bonus gold reward is shown to the player.
//        - Failure: the crew repels the boarders, dealing a penalty to the
//          player's hull (30 HP by default).
//
// Resolution roll:
//   PlayerScore = Random(1, 10) + health_bonus + renown_tier_bonus
//   EnemyScore  = Random(1, 10) + tier_bonus
//   Success if PlayerScore > EnemyScore.
//
// Cooldown: 5 seconds between boarding attempts to prevent spam.
// ---------------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;   // Text, Shadow
using Booty.World;      // EnemyMetadata

namespace Booty.Combat
{
    /// <summary>
    /// Manages player boarding attempts against nearby enemy ships.
    /// Attach to the player ship GameObject.
    /// </summary>
    public class BoardingSystem : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Configuration
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Maximum range for initiating a boarding action (world units).</summary>
        public const float BoardingRange = 7.0f;

        /// <summary>Bonus gold multiplier for a successful boarding (vs. normal kill reward).</summary>
        private const float BoardingGoldBonus = 1.5f;

        /// <summary>HP damage dealt to player on a failed boarding attempt.</summary>
        private const int BoardingFailDamage = 30;

        /// <summary>Cooldown between boarding attempts (seconds).</summary>
        private const float BoardingCooldown = 5f;

        // ══════════════════════════════════════════════════════════════════
        //  Runtime State
        // ══════════════════════════════════════════════════════════════════

        private HPSystem       _playerHP;
        private Transform      _playerTransform;
        private float          _cooldownTimer;
        private bool           _inRange;
        private GameObject     _nearestEnemy;
        private float          _nearestDist;

        // ══════════════════════════════════════════════════════════════════
        //  Public API
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Wire the player HP system. Called by BootyBootstrap after ship creation.
        /// </summary>
        public void Initialize(HPSystem playerHP)
        {
            _playerHP        = playerHP;
            _playerTransform = transform;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Awake()
        {
            if (_playerHP == null) _playerHP = GetComponent<HPSystem>();
            _playerTransform = transform;
        }

        private void Update()
        {
            if (_cooldownTimer > 0f)
                _cooldownTimer -= Time.deltaTime;

            ScanForBoardingTarget();

            // Player presses B to board
            if (_inRange && _nearestEnemy != null && Input.GetKeyDown(KeyCode.B))
            {
                if (_cooldownTimer <= 0f)
                    AttemptBoarding(_nearestEnemy);
                else
                    ShowCooldownFeedback();
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Target Scanning
        // ══════════════════════════════════════════════════════════════════

        private void ScanForBoardingTarget()
        {
            _inRange      = false;
            _nearestEnemy = null;
            _nearestDist  = float.MaxValue;

            // Find all living enemy ships by HPSystem components in scene
            var allHP = FindObjectsOfType<HPSystem>();
            foreach (var hp in allHP)
            {
                if (hp.gameObject == gameObject) continue; // skip self
                if (hp.IsDead)                  continue; // skip dead ships
                if (hp.CompareTag("Player"))    continue; // skip player

                float dist = Vector3.Distance(transform.position, hp.transform.position);
                if (dist < _nearestDist)
                {
                    _nearestDist  = dist;
                    _nearestEnemy = hp.gameObject;
                }
            }

            _inRange = _nearestEnemy != null && _nearestDist <= BoardingRange;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Boarding Resolution
        // ══════════════════════════════════════════════════════════════════

        private void AttemptBoarding(GameObject enemyShip)
        {
            _cooldownTimer = BoardingCooldown;

            // ── Dice Roll ──────────────────────────────────────────────
            int playerRoll = Random.Range(1, 11);   // 1-10
            int enemyRoll  = Random.Range(1, 11);

            // Player bonus: healthy crew fights better
            float playerHealthRatio = _playerHP != null ? _playerHP.HPNormalized : 1f;
            playerRoll += Mathf.RoundToInt(playerHealthRatio * 3f); // up to +3 for full HP

            // Enemy bonus from tier metadata
            var meta = enemyShip.GetComponent<EnemyMetadata>();
            int tier = meta != null ? Mathf.Max(1, meta.tier) : 1;
            enemyRoll += tier; // +1 per tier

            bool success = playerRoll > enemyRoll;

            if (success)
            {
                OnBoardingSuccess(enemyShip, tier);
            }
            else
            {
                OnBoardingFailure();
            }
        }

        private void OnBoardingSuccess(GameObject enemyShip, int enemyTier)
        {
            // Sink the enemy ship immediately
            var enemyHP = enemyShip.GetComponent<HPSystem>();
            if (enemyHP != null && !enemyHP.IsDead)
            {
                // Force kill by dealing remaining HP as damage
                enemyHP.TakeDamage(enemyHP.CurrentHP + 1);
            }

            // Award bonus gold popup
            int bonusGold = Mathf.RoundToInt(CombatConfig.GoldRewardPerKill * enemyTier * BoardingGoldBonus);
            SpawnBoardingResultPopup(transform.position + Vector3.up * 3f,
                                     $"+{bonusGold} gold (boarded!)",
                                     new Color(0.2f, 1.0f, 0.4f)); // green text

            Debug.Log($"[BoardingSystem] SUCCESS — boarded enemy tier {enemyTier}, bonus gold {bonusGold}");
        }

        private void OnBoardingFailure()
        {
            // Repelled — player takes damage
            if (_playerHP != null)
                _playerHP.TakeDamage(BoardingFailDamage);

            SpawnBoardingResultPopup(transform.position + Vector3.up * 3f,
                                     "Repelled! (-" + BoardingFailDamage + " HP)",
                                     new Color(1f, 0.3f, 0.2f)); // red text

            Debug.Log("[BoardingSystem] FAILED — crew repelled boarders.");
        }

        // ══════════════════════════════════════════════════════════════════
        //  UI Helpers
        // ══════════════════════════════════════════════════════════════════

        private void ShowCooldownFeedback()
        {
            float remaining = Mathf.CeilToInt(_cooldownTimer);
            Debug.Log($"[BoardingSystem] Crew recovering — {remaining}s cooldown.");
        }

        /// <summary>
        /// Spawn a floating world-space text popup at <paramref name="worldPos"/>.
        /// Rises and fades over 2.5 seconds.
        /// </summary>
        private static void SpawnBoardingResultPopup(Vector3 worldPos, string message, Color color)
        {
            var go = new GameObject("BoardingResultPopup");
            go.transform.position = worldPos;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 30;

            var rt = go.GetComponent<UnityEngine.RectTransform>();
            rt.sizeDelta = new Vector2(4f, 1f);
            go.transform.localScale = Vector3.one * 0.03f;

            var labelGO = new GameObject("Text");
            labelGO.transform.SetParent(go.transform, false);

            var labelRect = labelGO.AddComponent<UnityEngine.RectTransform>();
            labelRect.sizeDelta        = new Vector2(150f, 60f);
            labelRect.anchoredPosition = Vector2.zero;

            var text = labelGO.AddComponent<UnityEngine.UI.Text>();
            text.text      = message;
            text.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize  = 18;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color     = color;

            var shadow = labelGO.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor    = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = new Vector2(2f, -2f);

            go.AddComponent<BoardingPopupMover>();
        }

        // ══════════════════════════════════════════════════════════════════
        //  On-screen prompt (IMGUI)
        // ══════════════════════════════════════════════════════════════════

        private void OnGUI()
        {
            if (!_inRange || _nearestEnemy == null) return;

            bool onCooldown = _cooldownTimer > 0f;
            string label = onCooldown
                ? $"  (crew recovering {_cooldownTimer:F1}s)"
                : "  [B] — Board Enemy Ship!";

            Color bgColor = onCooldown ? new Color(0.4f, 0.3f, 0.1f, 0.75f) : new Color(0.1f, 0.5f, 0.2f, 0.80f);

            // Draw a centered prompt near the bottom-center of the screen
            float w = 280f, h = 36f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height - 90f;

            // Background box
            GUI.color = bgColor;
            GUI.DrawTexture(new Rect(x - 4, y - 4, w + 8, h + 8), Texture2D.whiteTexture);

            // Label
            GUI.color = Color.white;
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            style.normal.textColor = onCooldown ? new Color(0.9f, 0.7f, 0.3f) : new Color(0.3f, 1f, 0.5f);
            GUI.Label(new Rect(x, y, w, h), label, style);

            GUI.color = Color.white;
        }
    }

    // =========================================================================
    //  BoardingPopupMover — Animates the boarding result text
    // =========================================================================

    /// <summary>
    /// Rises and fades a world-space canvas over 2.5 seconds.
    /// </summary>
    internal class BoardingPopupMover : MonoBehaviour
    {
        private const float Duration  = 2.5f;
        private const float RiseSpeed = 2.5f;

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
