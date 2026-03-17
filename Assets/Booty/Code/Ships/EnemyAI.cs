// ---------------------------------------------------------------------------
// EnemyAI.cs — Patrol / aggro / chase state machine for enemy ships
// ---------------------------------------------------------------------------
// Per SubPRD 4.1 (Enemy AI):
//   States: Patrol -> Engage -> Chase & Circle (firing broadsides)
//   Target selection: always targets the player in P1.
//   No advanced maneuvers (tack against wind, etc.).
//   Fights to the death (no disengage in P1).
// ---------------------------------------------------------------------------

using UnityEngine;
using Booty.Faction;

namespace Booty.Ships
{
    /// <summary>
    /// Simple three-state enemy AI: Patrol, Chase, and Attack.
    /// Drives the ship's <see cref="ShipController"/> and fires broadsides
    /// via <see cref="Combat.BroadsideSystem"/> when the player is in arc.
    /// </summary>
    public class EnemyAI : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  State Machine
        // ══════════════════════════════════════════════════════════════════

        private enum AIState
        {
            Patrol,
            Chase,
            Attack,
            Evade
        }

        [Header("Faction")]
        [Tooltip("Faction this ship belongs to (e.g. 'spanish_crown'). " +
                 "Set by EnemySpawner or BootyBootstrap from EnemyMetadata.sourceFaction.\n" +
                 "Empty = no allegiance = always hostile.")]
        public string FactionId = "";

        [Header("Debug (read-only)")]
        [SerializeField] private AIState currentState = AIState.Patrol;

        // ══════════════════════════════════════════════════════════════════
        //  References (set via Initialize)
        // ══════════════════════════════════════════════════════════════════

        private Transform _playerTarget;
        private ShipController _ship;
        private Combat.BroadsideSystem _broadside;
        private Combat.HPSystem _hp;

        // ══════════════════════════════════════════════════════════════════
        //  Evade State
        // ══════════════════════════════════════════════════════════════════

        private float _evadeTimer;
        private bool _hasEvaded;

        // ══════════════════════════════════════════════════════════════════
        //  Patrol State
        // ══════════════════════════════════════════════════════════════════

        private Vector3 _spawnPosition;
        private Vector3 _patrolWaypoint;
        private float _waypointTimer;

        // ══════════════════════════════════════════════════════════════════
        //  Public API
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Wire references. Called by BootyBootstrap after instantiation.
        /// </summary>
        public void Initialize(Transform playerTarget, ShipController ship,
                               Combat.BroadsideSystem broadside, Combat.HPSystem hp)
        {
            _playerTarget = playerTarget;
            _ship = ship;
            _broadside = broadside;
            _hp = hp;

            // Mark as AI-controlled so ShipController reads SetThrottle/SetRudder
            // instead of WASD input.
            if (_ship != null)
            {
                _ship.SetPlayerControlled(false);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void Start()
        {
            _spawnPosition = transform.position;
            PickNewPatrolWaypoint();

            if (_ship == null) _ship = GetComponent<ShipController>();
            if (_broadside == null) _broadside = GetComponent<Combat.BroadsideSystem>();
            if (_hp == null) _hp = GetComponent<Combat.HPSystem>();
        }

        private void Update()
        {
            if (_hp != null && _hp.IsDead) return;

            float distToPlayer = PlayerDistance();

            // ── State transitions ───────────────────────────────────────
            switch (currentState)
            {
                case AIState.Patrol:
                    if (distToPlayer <= Combat.CombatConfig.AggroDistance && ShouldAggro())
                    {
                        currentState = AIState.Chase;
                    }
                    break;

                case AIState.Chase:
                    if (distToPlayer <= Combat.CombatConfig.PreferredCombatRange)
                    {
                        currentState = AIState.Attack;
                    }
                    break;

                case AIState.Attack:
                    if (!_hasEvaded && _hp != null && _hp.HPNormalized < 0.35f)
                    {
                        currentState = AIState.Evade;
                        _evadeTimer = 4f;
                        _hasEvaded = true;
                    }
                    else if (distToPlayer > Combat.CombatConfig.AggroDistance)
                    {
                        // Lost the player — return to patrol
                        currentState = AIState.Patrol;
                        PickNewPatrolWaypoint();
                    }
                    else if (distToPlayer > Combat.CombatConfig.PreferredCombatRange * 1.5f)
                    {
                        // Too far for attack pass — chase again
                        currentState = AIState.Chase;
                    }
                    break;

                case AIState.Evade:
                    if (_evadeTimer <= 0f)
                    {
                        currentState = AIState.Attack;
                    }
                    break;
            }

            // ── State behaviour ─────────────────────────────────────────
            switch (currentState)
            {
                case AIState.Patrol:
                    ExecutePatrol();
                    break;
                case AIState.Chase:
                    ExecuteChase();
                    break;
                case AIState.Attack:
                    ExecuteAttack();
                    break;
                case AIState.Evade:
                    ExecuteEvade();
                    break;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  State: Patrol
        // ══════════════════════════════════════════════════════════════════

        private void ExecutePatrol()
        {
            _waypointTimer -= Time.deltaTime;
            if (_waypointTimer <= 0f)
            {
                PickNewPatrolWaypoint();
            }

            SteerToward(_patrolWaypoint, 0.6f);
        }

        private void PickNewPatrolWaypoint()
        {
            Vector2 circle = Random.insideUnitCircle * Combat.CombatConfig.PatrolRadius;
            _patrolWaypoint = _spawnPosition + new Vector3(circle.x, 0f, circle.y);
            _waypointTimer = Combat.CombatConfig.PatrolWaypointInterval;
        }

        // ══════════════════════════════════════════════════════════════════
        //  State: Chase — close distance to the player
        // ══════════════════════════════════════════════════════════════════

        private void ExecuteChase()
        {
            if (_playerTarget == null) return;
            SteerToward(_playerTarget.position, 1f);
        }

        // ══════════════════════════════════════════════════════════════════
        //  State: Attack — circle the player while firing broadsides
        // ══════════════════════════════════════════════════════════════════

        private void ExecuteAttack()
        {
            if (_playerTarget == null) return;

            Vector3 toPlayer = _playerTarget.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;

            // Try to orbit: steer perpendicular to the line toward the player
            // This creates a circling behaviour, exposing broadside arcs
            Vector3 perpendicular = Vector3.Cross(Vector3.up, toPlayer.normalized);

            // Adjust: if too close, steer away; if too far, steer toward
            float rangeDelta = dist - Combat.CombatConfig.PreferredCombatRange;
            Vector3 desiredDir = perpendicular + toPlayer.normalized * (rangeDelta * 0.1f);
            desiredDir.y = 0f;
            desiredDir.Normalize();

            Vector3 targetPoint = transform.position + desiredDir * 10f;
            SteerToward(targetPoint, 0.8f);

            // ── Fire broadsides if player is in arc ─────────────────────
            if (_broadside != null)
            {
                if (_broadside.IsInPortArc(_playerTarget.position) && _broadside.PortReady)
                {
                    _broadside.FirePort();
                }

                if (_broadside.IsInStarboardArc(_playerTarget.position) && _broadside.StarboardReady)
                {
                    _broadside.FireStarboard();
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  State: Evade — turn away from player at full throttle for 4 s
        // ══════════════════════════════════════════════════════════════════

        private void ExecuteEvade()
        {
            _evadeTimer -= Time.deltaTime;
            if (_evadeTimer <= 0f)
            {
                currentState = AIState.Attack;
                return;
            }
            if (_playerTarget == null) return;
            // Steer away from player at full throttle
            Vector3 awayFromPlayer = transform.position - _playerTarget.position;
            awayFromPlayer.y = 0f;
            if (awayFromPlayer.sqrMagnitude > 0.01f)
            {
                Vector3 evadeTarget = transform.position + awayFromPlayer.normalized * 20f;
                SteerToward(evadeTarget, 1f);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Faction / Reputation Helpers
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns true when this ship should aggro the player based on faction
        /// reputation. Ships with Hostile standing attack on sight; Neutral and
        /// Allied ships patrol and ignore the player.
        /// <para>
        /// Falls back to <c>true</c> (always aggressive) when no
        /// <see cref="Faction.ReputationManager"/> is present in the scene.
        /// </para>
        /// </summary>
        private bool ShouldAggro()
        {
            var repMgr = ReputationManager.Instance;
            if (repMgr == null)
                return true; // No reputation system — use original always-aggressive behaviour

            return repMgr.IsHostile(FactionId);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Navigation Helpers
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Steer toward a world position by setting throttle and rudder.
        /// </summary>
        private void SteerToward(Vector3 target, float throttle)
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

            // Determine rudder: which side of our forward vector is the target?
            float signedAngle = Vector3.SignedAngle(transform.forward, toTarget, Vector3.up);

            // Proportional rudder control
            float rudder = Mathf.Clamp(signedAngle / 45f, -1f, 1f);

            _ship.SetThrottle(throttle);
            _ship.SetRudder(rudder);
        }

        private float PlayerDistance()
        {
            if (_playerTarget == null) return float.MaxValue;
            Vector3 diff = _playerTarget.position - transform.position;
            diff.y = 0f;
            return diff.magnitude;
        }
    }
}
