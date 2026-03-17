// ---------------------------------------------------------------------------
// GameplayBot.cs — Automated gameplay bot for PlayMode testing
// ---------------------------------------------------------------------------
// Drives the player ship through combat, port capture, and repair cycles.
// Used by PlayMode tests to exercise the gameplay loop without manual input.
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;
using Booty.Ships;
using Booty.Combat;
using Booty.Ports;

namespace Booty.Testing
{
    /// <summary>
    /// Automated bot that controls the player ship through gameplay loops.
    /// Attach to the player ship GameObject and enable to start automation.
    /// Disable to return control to the player.
    /// </summary>
    public class GameplayBot : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  State Machine
        // ══════════════════════════════════════════════════════════════════

        private enum BotState
        {
            FindEnemy,
            SailToEnemy,
            EngageCombat,
            FindHostilePort,
            SailToHostilePort,
            FindFriendlyPort,
            SailToFriendlyPort,
            Dock,
            Repair,
            Idle
        }

        private BotState _state = BotState.FindEnemy;

        // ══════════════════════════════════════════════════════════════════
        //  Public Metrics
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Number of enemies killed this session.</summary>
        public int KillCount { get; private set; }

        /// <summary>Number of ports captured this session.</summary>
        public int CapturedPortCount { get; private set; }

        // ══════════════════════════════════════════════════════════════════
        //  References
        // ══════════════════════════════════════════════════════════════════

        private ShipController _ship;
        private BroadsideSystem _broadside;
        private HPSystem _hp;
        private PortSystem _portSystem;

        // ══════════════════════════════════════════════════════════════════
        //  State Targets
        // ══════════════════════════════════════════════════════════════════

        private Transform _currentEnemy;
        private PortInteraction _currentPort;
        private HashSet<HPSystem> _subscribedEnemies = new HashSet<HPSystem>();
        private float _idleTimer;

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void OnEnable()
        {
            _ship = GetComponent<ShipController>();
            _broadside = GetComponent<BroadsideSystem>();
            _hp = GetComponent<HPSystem>();
            _portSystem = Object.FindObjectOfType<PortSystem>();

            if (_ship != null)
                _ship.SetPlayerControlled(false);

            _state = BotState.FindEnemy;
        }

        private void OnDisable()
        {
            if (_ship != null)
                _ship.SetPlayerControlled(true);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Update
        // ══════════════════════════════════════════════════════════════════

        private void Update()
        {
            // HP emergency: seek repairs when critically low
            if (_hp != null && _hp.HPNormalized < 0.30f &&
                _state != BotState.FindFriendlyPort &&
                _state != BotState.SailToFriendlyPort &&
                _state != BotState.Dock &&
                _state != BotState.Repair)
            {
                _state = BotState.FindFriendlyPort;
            }

            switch (_state)
            {
                case BotState.FindEnemy:         ExecuteFindEnemy();         break;
                case BotState.SailToEnemy:       ExecuteSailToEnemy();       break;
                case BotState.EngageCombat:      ExecuteEngageCombat();      break;
                case BotState.FindHostilePort:   ExecuteFindHostilePort();   break;
                case BotState.SailToHostilePort: ExecuteSailToHostilePort(); break;
                case BotState.FindFriendlyPort:  ExecuteFindFriendlyPort();  break;
                case BotState.SailToFriendlyPort:ExecuteSailToFriendlyPort();break;
                case BotState.Dock:              ExecuteDock();              break;
                case BotState.Repair:            ExecuteRepair();            break;
                case BotState.Idle:              ExecuteIdle();              break;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  State: FindEnemy
        // ══════════════════════════════════════════════════════════════════

        private void ExecuteFindEnemy()
        {
            HPSystem nearest = null;
            float nearestDist = float.MaxValue;

            HPSystem[] allHP = Object.FindObjectsOfType<HPSystem>();
            foreach (HPSystem hp in allHP)
            {
                if (hp == _hp) continue;
                if (hp.IsDead) continue;
                if (hp.GetComponent<EnemyAI>() == null) continue;

                float dist = Vector3.Distance(transform.position, hp.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = hp;
                }
            }

            if (nearest == null)
            {
                _state = BotState.Idle;
                return;
            }

            // Subscribe to death event (only once per enemy)
            if (!_subscribedEnemies.Contains(nearest))
            {
                _subscribedEnemies.Add(nearest);
                nearest.OnDestroyed += () => KillCount++;
            }

            _currentEnemy = nearest.transform;
            _state = BotState.SailToEnemy;
        }

        // ══════════════════════════════════════════════════════════════════
        //  State: SailToEnemy
        // ══════════════════════════════════════════════════════════════════

        private void ExecuteSailToEnemy()
        {
            if (_currentEnemy == null || (_currentEnemy.GetComponent<HPSystem>()?.IsDead ?? true))
            {
                _currentEnemy = null;
                _state = BotState.FindEnemy;
                return;
            }

            float dist = Vector3.Distance(transform.position, _currentEnemy.position);

            if (_broadside != null && dist <= _broadside.FiringRange * 0.9f)
            {
                _state = BotState.EngageCombat;
                return;
            }

            SteerToward(_currentEnemy.position, 1f);
        }

        // ══════════════════════════════════════════════════════════════════
        //  State: EngageCombat
        // ══════════════════════════════════════════════════════════════════

        private void ExecuteEngageCombat()
        {
            if (_currentEnemy == null || (_currentEnemy.GetComponent<HPSystem>()?.IsDead ?? true))
            {
                _currentEnemy = null;
                _state = BotState.FindHostilePort;
                return;
            }

            if (_broadside == null)
            {
                _state = BotState.FindEnemy;
                return;
            }

            // Orbit the enemy: perpendicular + range correction (same pattern as EnemyAI)
            Vector3 toEnemy = _currentEnemy.position - transform.position;
            toEnemy.y = 0f;
            float dist = toEnemy.magnitude;

            Vector3 perpendicular = Vector3.Cross(Vector3.up, toEnemy.normalized);
            float rangeDelta = dist - _broadside.FiringRange * 0.7f;
            Vector3 desiredDir = perpendicular + toEnemy.normalized * (rangeDelta * 0.1f);
            desiredDir.y = 0f;
            if (desiredDir.sqrMagnitude > 0.001f)
                desiredDir.Normalize();

            Vector3 targetPoint = transform.position + desiredDir * 10f;
            SteerToward(targetPoint, 0.8f);

            // Fire broadsides when enemy is in arc
            if (_broadside.IsInPortArc(_currentEnemy.position) && _broadside.PortReady)
                _broadside.FirePort();

            if (_broadside.IsInStarboardArc(_currentEnemy.position) && _broadside.StarboardReady)
                _broadside.FireStarboard();
        }

        // ══════════════════════════════════════════════════════════════════
        //  State: FindHostilePort
        // ══════════════════════════════════════════════════════════════════

        private void ExecuteFindHostilePort()
        {
            if (_portSystem == null)
            {
                _state = BotState.FindEnemy;
                return;
            }

            PortInteraction nearest = null;
            float nearestDist = float.MaxValue;

            PortInteraction[] allPorts = Object.FindObjectsOfType<PortInteraction>();
            foreach (PortInteraction pi in allPorts)
            {
                if (!_portSystem.IsPortHostile(pi.PortId)) continue;

                float dist = Vector3.Distance(transform.position, pi.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = pi;
                }
            }

            if (nearest == null)
            {
                _state = BotState.FindFriendlyPort;
                return;
            }

            _currentPort = nearest;
            _state = BotState.SailToHostilePort;
        }

        // ══════════════════════════════════════════════════════════════════
        //  State: SailToHostilePort
        // ══════════════════════════════════════════════════════════════════

        private void ExecuteSailToHostilePort()
        {
            if (_currentPort == null || _portSystem == null)
            {
                _state = BotState.FindHostilePort;
                return;
            }

            float dist = Vector3.Distance(transform.position, _currentPort.transform.position);

            if (dist <= 15f)
            {
                bool captured = _portSystem.CapturePort(_currentPort.PortId);
                if (captured)
                    CapturedPortCount++;

                _state = BotState.FindFriendlyPort;
                return;
            }

            SteerToward(_currentPort.transform.position, 1f);
        }

        // ══════════════════════════════════════════════════════════════════
        //  State: FindFriendlyPort
        // ══════════════════════════════════════════════════════════════════

        private void ExecuteFindFriendlyPort()
        {
            if (_portSystem == null)
            {
                _state = BotState.FindEnemy;
                return;
            }

            PortInteraction nearest = null;
            float nearestDist = float.MaxValue;

            PortInteraction[] allPorts = Object.FindObjectsOfType<PortInteraction>();
            foreach (PortInteraction pi in allPorts)
            {
                if (!_portSystem.IsPortFriendly(pi.PortId)) continue;

                float dist = Vector3.Distance(transform.position, pi.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = pi;
                }
            }

            if (nearest == null)
            {
                _state = BotState.FindEnemy;
                return;
            }

            _currentPort = nearest;
            _state = BotState.SailToFriendlyPort;
        }

        // ══════════════════════════════════════════════════════════════════
        //  State: SailToFriendlyPort
        // ══════════════════════════════════════════════════════════════════

        private void ExecuteSailToFriendlyPort()
        {
            if (_currentPort == null)
            {
                _state = BotState.FindFriendlyPort;
                return;
            }

            float dist = Vector3.Distance(transform.position, _currentPort.transform.position);

            if (dist <= 20f)
            {
                bool docked = _currentPort.TryDock();
                if (docked)
                {
                    _state = BotState.Repair;
                    return;
                }
            }

            SteerToward(_currentPort.transform.position, 1f);
        }

        // ══════════════════════════════════════════════════════════════════
        //  State: Dock (transition state — TryDock already called)
        // ══════════════════════════════════════════════════════════════════

        private void ExecuteDock()
        {
            _state = BotState.Repair;
        }

        // ══════════════════════════════════════════════════════════════════
        //  State: Repair
        // ══════════════════════════════════════════════════════════════════

        private void ExecuteRepair()
        {
            if (_currentPort != null)
                _currentPort.RequestRepair();

            _state = BotState.FindEnemy;
        }

        // ══════════════════════════════════════════════════════════════════
        //  State: Idle
        // ══════════════════════════════════════════════════════════════════

        private void ExecuteIdle()
        {
            _idleTimer += Time.deltaTime;
            if (_idleTimer >= 3f)
            {
                _idleTimer = 0f;
                _state = BotState.FindEnemy;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Navigation Helper
        // ══════════════════════════════════════════════════════════════════

        private void SteerToward(Vector3 target, float throttle)
        {
            if (_ship == null)
            {
                Debug.LogWarning("[GameplayBot] SteerToward: _ship is null");
                return;
            }

            Vector3 toTarget = target - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude < 1f)
            {
                _ship.SetThrottle(0f);
                _ship.SetRudder(0f);
                return;
            }

            float signedAngle = Vector3.SignedAngle(transform.forward, toTarget, Vector3.up);
            float rudder = Mathf.Clamp(signedAngle / 45f, -1f, 1f);
            _ship.SetThrottle(throttle);
            _ship.SetRudder(rudder);
        }
    }
}
