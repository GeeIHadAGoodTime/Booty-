// ---------------------------------------------------------------------------
// EnemyShipAI.cs — Full state machine: Patrol → Chase → Attack → Flee
// ---------------------------------------------------------------------------
// Namespace: Booty.AI
//
// States:
//   Patrol  — follows random waypoints near spawn position
//   Chase   — closes distance when player is within AggroDistance
//   Attack  — orbits player and fires broadsides
//   Flee    — permanent retreat once HP drops below FleeThreshold (25%)
//
// The Flee state is permanent (unlike the legacy EnemyAI's temporary Evade).
// NavalEncounter can call TriggerFlee() externally when the player chooses
// "Surrender Cargo" and the enemy ship should retreat.
//
// Wire via Initialize() after instantiation; or let Start() self-wire via
// GetComponent if called standalone.
// ---------------------------------------------------------------------------

using UnityEngine;
using Booty.Ships;
using Booty.Combat;

namespace Booty.AI
{
    /// <summary>
    /// Four-state enemy ship AI: Patrol, Chase, Attack, Flee.
    /// </summary>
    public class EnemyShipAI : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  State
        // ══════════════════════════════════════════════════════════════════

        public enum AIState { Patrol, Chase, Attack, Flee }

        [Header("Debug — Read Only")]
        [SerializeField] private AIState _state = AIState.Patrol;

        // ══════════════════════════════════════════════════════════════════
        //  Inspector Tuning
        // ══════════════════════════════════════════════════════════════════

        [Header("Detection & Range")]
        [Tooltip("Distance at which this ship detects the player and switches to Chase.")]
        [SerializeField] private float aggroDistance = CombatConfig.AggroDistance;

        [Tooltip("Distance at which Chase transitions to Attack (orbiting broadsides).")]
        [SerializeField] private float attackRange = CombatConfig.PreferredCombatRange;

        [Tooltip("Random patrol waypoints are picked within this radius of the spawn point.")]
        [SerializeField] private float patrolRadius = CombatConfig.PatrolRadius;

        [Header("Flee")]
        [Tooltip("HP fraction (0–1) below which this ship permanently flees. Default 0.25 = 25%.")]
        [Range(0f, 1f)]
        [SerializeField] private float fleeHPThreshold = 0.25f;

        // ══════════════════════════════════════════════════════════════════
        //  References
        // ══════════════════════════════════════════════════════════════════

        private Transform      _playerTarget;
        private ShipController _ship;
        private BroadsideSystem _broadside;
        private HPSystem       _hp;

        // ══════════════════════════════════════════════════════════════════
        //  Patrol State
        // ══════════════════════════════════════════════════════════════════

        private Vector3 _spawnPos;
        private Vector3 _patrolWaypoint;
        private float   _waypointTimer;

        // ══════════════════════════════════════════════════════════════════
        //  Flee State
        // ══════════════════════════════════════════════════════════════════

        private bool _fleeTriggered;

        // ══════════════════════════════════════════════════════════════════
        //  Properties
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Current AI state (read-only for other systems).</summary>
        public AIState CurrentState => _state;

        /// <summary>True once the flee state has been permanently triggered.</summary>
        public bool IsFleeing => _fleeTriggered;

        // ══════════════════════════════════════════════════════════════════
        //  Public API
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Wire all references. Called by BootyBootstrap or EnemySpawner after instantiation.
        /// </summary>
        public void Initialize(Transform playerTarget, ShipController ship,
                               BroadsideSystem broadside, HPSystem hp,
                               float fleeThreshold = 0.25f)
        {
            _playerTarget   = playerTarget;
            _ship           = ship;
            _broadside      = broadside;
            _hp             = hp;
            fleeHPThreshold = Mathf.Clamp01(fleeThreshold);

            if (_ship != null)
                _ship.SetPlayerControlled(false);
        }

        /// <summary>
        /// Force this ship into the permanent Flee state immediately.
        /// Called by NavalEncounter when the player chooses "Surrender Cargo".
        /// </summary>
        public void TriggerFlee()
        {
            _fleeTriggered = true;
            _state         = AIState.Flee;
            Debug.Log($"[EnemyShipAI] {name} — flee triggered externally.");
        }

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Start()
        {
            _spawnPos = transform.position;
            PickPatrolWaypoint();

            // Self-wire if Initialize() was not called
            if (_ship      == null) _ship      = GetComponent<ShipController>();
            if (_broadside == null) _broadside = GetComponent<BroadsideSystem>();
            if (_hp        == null) _hp        = GetComponent<HPSystem>();

            if (_ship != null)
                _ship.SetPlayerControlled(false);
        }

        private void Update()
        {
            if (_hp != null && _hp.IsDead) return;

            // ── Flee check: permanent once triggered ───────────────────────
            if (!_fleeTriggered && _hp != null && _hp.HPNormalized < fleeHPThreshold)
            {
                TriggerFlee();
            }

            if (_fleeTriggered)
            {
                ExecuteFlee();
                return;
            }

            float dist = PlayerDist();

            // ── State transitions ──────────────────────────────────────────
            switch (_state)
            {
                case AIState.Patrol:
                    if (dist <= aggroDistance)
                        _state = AIState.Chase;
                    break;

                case AIState.Chase:
                    if (dist <= attackRange)
                        _state = AIState.Attack;
                    else if (dist > aggroDistance * 1.25f)
                    {
                        _state = AIState.Patrol;
                        PickPatrolWaypoint();
                    }
                    break;

                case AIState.Attack:
                    if (dist > aggroDistance)
                    {
                        _state = AIState.Patrol;
                        PickPatrolWaypoint();
                    }
                    else if (dist > attackRange * 1.5f)
                        _state = AIState.Chase;
                    break;
            }

            // ── State behaviours ───────────────────────────────────────────
            switch (_state)
            {
                case AIState.Patrol:  ExecutePatrol();  break;
                case AIState.Chase:   ExecuteChase();   break;
                case AIState.Attack:  ExecuteAttack();  break;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  State: Patrol — random waypoints near spawn
        // ══════════════════════════════════════════════════════════════════

        private void ExecutePatrol()
        {
            _waypointTimer -= Time.deltaTime;
            if (_waypointTimer <= 0f)
                PickPatrolWaypoint();

            SteerTo(_patrolWaypoint, 0.55f);
        }

        private void PickPatrolWaypoint()
        {
            Vector2 c        = Random.insideUnitCircle * patrolRadius;
            _patrolWaypoint  = _spawnPos + new Vector3(c.x, 0f, c.y);
            _waypointTimer   = CombatConfig.PatrolWaypointInterval;
        }

        // ══════════════════════════════════════════════════════════════════
        //  State: Chase — close distance to player at full throttle
        // ══════════════════════════════════════════════════════════════════

        private void ExecuteChase()
        {
            if (_playerTarget == null) return;
            SteerTo(_playerTarget.position, 1f);
        }

        // ══════════════════════════════════════════════════════════════════
        //  State: Attack — orbit and fire broadsides
        // ══════════════════════════════════════════════════════════════════

        private void ExecuteAttack()
        {
            if (_playerTarget == null) return;

            Vector3 toPlayer = _playerTarget.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;

            // Orbit: steer perpendicular; lean in/out to hold preferred range
            Vector3 perp     = Vector3.Cross(Vector3.up, toPlayer.normalized);
            float   delta    = dist - attackRange;
            Vector3 desired  = perp + toPlayer.normalized * (delta * 0.1f);
            desired.y        = 0f;
            if (desired.sqrMagnitude > 0.001f) desired.Normalize();

            SteerTo(transform.position + desired * 10f, 0.85f);

            // Fire broadsides when player is within arc
            if (_broadside != null)
            {
                if (_broadside.IsInPortArc(_playerTarget.position) && _broadside.PortReady)
                    _broadside.FirePort();

                if (_broadside.IsInStarboardArc(_playerTarget.position) && _broadside.StarboardReady)
                    _broadside.FireStarboard();
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  State: Flee — permanent; move away from player at full throttle
        // ══════════════════════════════════════════════════════════════════

        private void ExecuteFlee()
        {
            if (_playerTarget == null) return;

            Vector3 away = transform.position - _playerTarget.position;
            away.y = 0f;

            if (away.sqrMagnitude > 0.01f)
                SteerTo(transform.position + away.normalized * 40f, 1f);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Navigation Helpers
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Steer the ship toward a world-space target at the given throttle (0–1).
        /// Uses proportional rudder control: small angle = gentle turn, large = full rudder.
        /// </summary>
        private void SteerTo(Vector3 target, float throttle)
        {
            if (_ship == null) return;

            Vector3 toTarget = target - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude < 1f)
            {
                _ship.SetThrottle(0f);
                _ship.SetRudder(0f);
                return;
            }

            float signedAngle = Vector3.SignedAngle(transform.forward, toTarget, Vector3.up);
            float rudder      = Mathf.Clamp(signedAngle / 45f, -1f, 1f);

            _ship.SetThrottle(throttle);
            _ship.SetRudder(rudder);
        }

        private float PlayerDist()
        {
            if (_playerTarget == null) return float.MaxValue;
            Vector3 diff = _playerTarget.position - transform.position;
            diff.y = 0f;
            return diff.magnitude;
        }
    }
}
