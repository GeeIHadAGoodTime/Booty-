// ---------------------------------------------------------------------------
// DebugConsole.cs — In-game debug overlay for development and QA use.
// ---------------------------------------------------------------------------
// Toggle with F1. Uses OnGUI for zero-dependency rendering.
// IMPORTANT: No production system may import or reference this class.
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;
using Booty.Bootstrap;
using Booty.Save;
using Booty.Ports;

namespace Booty.Infra.Debug
{
    /// <summary>
    /// Development debug console. Press F1 to toggle.
    /// Provides in-game commands for testing gameplay systems without editor access.
    /// PRODUCTION RULE: No other class may reference DebugConsole.
    /// </summary>
    public class DebugConsole : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  Constants
        // ══════════════════════════════════════════════════════════════════

        private const int WindowWidth  = 400;
        private const int WindowHeight = 300;
        private const int MaxLogLines  = 10;

        // Hardcoded port teleport positions as specified in requirements.
        // Format: portId -> world position (x, 0, z)
        private static readonly Dictionary<string, Vector3> PortPositions = new Dictionary<string, Vector3>
        {
            { "port_haven",      new Vector3(-40f, 0f,  30f) },
            { "fort_imperial",   new Vector3( 50f, 0f,  40f) },
            { "smugglers_cove",  new Vector3( 10f, 0f, -50f) },
            { "isla_del_oro",    new Vector3(-30f, 0f, -30f) },
        };

        // ══════════════════════════════════════════════════════════════════
        //  State
        // ══════════════════════════════════════════════════════════════════

        private bool   _visible       = false;
        private string _inputBuffer   = "";
        private Vector2 _scrollPos    = Vector2.zero;

        private readonly List<string> _log = new List<string>();

        // Cached GUI styles — created once to avoid per-frame allocations.
        private GUIStyle _boxStyle;
        private GUIStyle _logStyle;
        private bool     _stylesInitialized = false;

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                _visible = !_visible;
            }
        }

        private void OnGUI()
        {
            if (!_visible) return;

            EnsureStyles();

            // Semi-transparent background box
            GUI.Box(new Rect(4, 4, WindowWidth, WindowHeight), "Debug Console", _boxStyle);

            // Input field — positioned inside the box header area
            GUI.SetNextControlName("DebugInput");
            _inputBuffer = GUI.TextField(
                new Rect(8, 28, WindowWidth - 80, 22),
                _inputBuffer
            );

            // Execute button
            if (GUI.Button(new Rect(WindowWidth - 68, 28, 64, 22), "Execute")
                || (Event.current.isKey
                    && Event.current.keyCode == KeyCode.Return
                    && GUI.GetNameOfFocusedControl() == "DebugInput"))
            {
                ExecuteCommand(_inputBuffer.Trim());
                _inputBuffer = "";
                GUI.FocusControl("DebugInput");
            }

            // Scrollable log area
            float logY      = 56f;
            float logHeight = WindowHeight - logY - 4f;
            Rect  logView   = new Rect(8, logY, WindowWidth - 16, logHeight);
            Rect  logInner  = new Rect(0, 0, WindowWidth - 36, MaxLogLines * 18);

            _scrollPos = GUI.BeginScrollView(logView, _scrollPos, logInner);
            for (int i = 0; i < _log.Count; i++)
            {
                GUI.Label(new Rect(0, i * 18, logInner.width, 18), _log[i], _logStyle);
            }
            GUI.EndScrollView();
        }

        // ══════════════════════════════════════════════════════════════════
        //  Command dispatch
        // ══════════════════════════════════════════════════════════════════

        private void ExecuteCommand(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return;

            Log("> " + raw);

            string[] parts = raw.Split(' ');
            string   cmd   = parts[0].ToLowerInvariant();

            switch (cmd)
            {
                case "give_gold":
                    CmdGiveGold(parts);
                    break;

                case "set_hp":
                    CmdSetHP(parts);
                    break;

                case "teleport":
                    CmdTeleport(parts);
                    break;

                case "spawn_enemy":
                    CmdSpawnEnemy();
                    break;

                case "capture_port":
                    CmdCapturePort(parts);
                    break;

                case "set_port_owner":
                    CmdSetPortOwner(parts);
                    break;

                case "show_state":
                    CmdShowState();
                    break;

                default:
                    Log("Unknown command: " + cmd);
                    Log("Commands: give_gold <n>, set_hp <n>, teleport <port>,");
                    Log("          spawn_enemy, capture_port <id>, show_state,");
                    Log("          set_port_owner <id> <faction>");
                    break;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Commands
        // ══════════════════════════════════════════════════════════════════

        /// <summary>give_gold &lt;n&gt; — adds n gold via SaveSystem.CurrentState.</summary>
        private void CmdGiveGold(string[] parts)
        {
            if (parts.Length < 2 || !float.TryParse(parts[1], out float amount))
            {
                Log("Usage: give_gold <n>");
                return;
            }

            var saveSystem = FindObjectOfType<SaveSystem>();
            if (saveSystem == null || saveSystem.CurrentState == null)
            {
                Log("ERROR: SaveSystem not found.");
                return;
            }

            saveSystem.CurrentState.player.gold += amount;
            Log(string.Format("Gold +{0:F0}. Total: {1:F0}", amount, saveSystem.CurrentState.player.gold));
        }

        /// <summary>set_hp &lt;n&gt; — heals or damages player ship to target HP.</summary>
        private void CmdSetHP(string[] parts)
        {
            if (parts.Length < 2 || !int.TryParse(parts[1], out int targetHP))
            {
                Log("Usage: set_hp <n>");
                return;
            }

            var hpSystem = GameRoot.Instance?.HPSystem;
            if (hpSystem == null)
            {
                Log("ERROR: HPSystem not available.");
                return;
            }

            int current = hpSystem.CurrentHP;
            int delta   = targetHP - current;

            if (delta > 0)
            {
                hpSystem.Heal(delta);
            }
            else if (delta < 0)
            {
                hpSystem.TakeDamage(-delta);
            }

            Log(string.Format("HP set to {0}/{1}", hpSystem.CurrentHP, hpSystem.MaxHP));
        }

        /// <summary>teleport &lt;port&gt; — moves player ship to hardcoded port position.</summary>
        private void CmdTeleport(string[] parts)
        {
            if (parts.Length < 2)
            {
                Log("Usage: teleport <port_id>");
                Log("Ports: port_haven, fort_imperial, smugglers_cove, isla_del_oro");
                return;
            }

            string portId = parts[1].ToLowerInvariant();

            if (!PortPositions.TryGetValue(portId, out Vector3 pos))
            {
                Log("Unknown port: " + portId);
                Log("Valid: port_haven, fort_imperial, smugglers_cove, isla_del_oro");
                return;
            }

            var playerShip = GameRoot.Instance?.PlayerShip;
            if (playerShip == null)
            {
                Log("ERROR: PlayerShip not available.");
                return;
            }

            playerShip.transform.position = pos;
            Log(string.Format("Teleported to {0} at ({1}, {2}, {3})", portId, pos.x, pos.y, pos.z));
        }

        /// <summary>spawn_enemy — spawns a primitive cube enemy 30-50 units from the player.</summary>
        private void CmdSpawnEnemy()
        {
            var playerShip = GameRoot.Instance?.PlayerShip;
            Vector3 origin = playerShip != null
                ? playerShip.transform.position
                : Vector3.zero;

            // Random point on a ring 30-50 units from the player
            float angle    = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float distance = Random.Range(30f, 50f);
            Vector3 offset = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
            Vector3 spawnPos = origin + offset;

            // Create a visible primitive stand-in for a real enemy prefab
            GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            enemy.name = "DebugEnemy";
            enemy.transform.position = spawnPos;
            enemy.transform.localScale = new Vector3(2f, 1f, 3f);

            // Tint red so it's clearly a debug spawn
            var renderer = enemy.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = Color.red;
            }

            Log(string.Format("Enemy spawned at ({0:F1}, {1:F1}, {2:F1})",
                spawnPos.x, spawnPos.y, spawnPos.z));
        }

        /// <summary>capture_port &lt;id&gt; — captures a port via PortSystem.</summary>
        private void CmdCapturePort(string[] parts)
        {
            if (parts.Length < 2)
            {
                Log("Usage: capture_port <port_id>");
                return;
            }

            string portId = parts[1];
            var portSystem = FindObjectOfType<PortSystem>();
            if (portSystem == null)
            {
                Log("ERROR: PortSystem not found.");
                return;
            }

            bool success = portSystem.CapturePort(portId);
            Log(success
                ? "Port captured: " + portId
                : "Capture failed for: " + portId + " (not found or already owned)");
        }

        /// <summary>set_port_owner &lt;portId&gt; &lt;factionId&gt; — transfers port ownership to specified faction.</summary>
        private void CmdSetPortOwner(string[] parts)
        {
            if (parts.Length < 3)
            {
                Log("Usage: set_port_owner <port_id> <faction_id>");
                return;
            }

            string portId    = parts[1];
            string factionId = parts[2];

            var portSystem = FindObjectOfType<PortSystem>();
            if (portSystem == null)
            {
                Log("ERROR: PortSystem not found.");
                return;
            }

            portSystem.SetPortOwner(portId, factionId);
            Log(string.Format("Port '{0}' owner set to '{1}'.", portId, factionId));
        }

        /// <summary>show_state — prints gold, HP, and player position to the log area.</summary>
        private void CmdShowState()
        {
            // Gold / Renown from SaveSystem
            var saveSystem = FindObjectOfType<SaveSystem>();
            if (saveSystem != null && saveSystem.CurrentState != null)
            {
                var player = saveSystem.CurrentState.player;
                Log(string.Format("Gold: {0:F0}  Renown: {1:F0}", player.gold, player.renown));
            }
            else
            {
                Log("Gold: N/A  Renown: N/A");
            }

            // HP from HPSystem
            var hpSystem = GameRoot.Instance?.HPSystem;
            if (hpSystem != null)
            {
                Log(string.Format("HP: {0}/{1}", hpSystem.CurrentHP, hpSystem.MaxHP));
            }
            else
            {
                Log("HP: N/A");
            }

            // Position from PlayerShip
            var playerShip = GameRoot.Instance?.PlayerShip;
            if (playerShip != null)
            {
                Vector3 pos = playerShip.transform.position;
                Log(string.Format("Position: ({0:F1}, {1:F1}, {2:F1})", pos.x, pos.y, pos.z));
            }
            else
            {
                Log("Position: N/A");
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Helpers
        // ══════════════════════════════════════════════════════════════════

        private void Log(string message)
        {
            _log.Add(message);

            // Keep only the last MaxLogLines entries
            while (_log.Count > MaxLogLines)
            {
                _log.RemoveAt(0);
            }

            // Auto-scroll to bottom
            _scrollPos = new Vector2(0f, float.MaxValue);
        }

        /// <summary>
        /// Creates GUI styles once. Must be called from inside OnGUI where
        /// GUISkin is available.
        /// </summary>
        private void EnsureStyles()
        {
            if (_stylesInitialized) return;

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal =
                {
                    background = MakeTex(2, 2, new Color(0.05f, 0.05f, 0.05f, 0.88f))
                }
            };

            _logStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 12,
                wordWrap  = false,
                normal    = { textColor = new Color(0.85f, 0.95f, 0.85f) }
            };

            _stylesInitialized = true;
        }

        /// <summary>Creates a 1-colour Texture2D for GUI backgrounds.</summary>
        private static Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            Texture2D tex = new Texture2D(width, height);
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }
    }
}
