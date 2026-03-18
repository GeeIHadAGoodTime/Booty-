// ---------------------------------------------------------------------------
// BotController.cs - Programmatic command driver for PlayMode tests
// ---------------------------------------------------------------------------
// Issues direct API calls to game systems without keyboard input.
// NOT a MonoBehaviour state machine — tests call methods directly.
// Used by GameplayLoopTest to drive Sail > Fight > Kill > Capture > Repair.
// ---------------------------------------------------------------------------

using System;
using UnityEngine;
using Booty.Ships;
using Booty.Combat;
using Booty.Economy;
using Booty.Ports;

namespace Booty.Tests.PlayMode
{
    /// <summary>
    /// Programmatic bot that issues direct API calls to game systems.
    /// Used by GameplayLoopTest to drive Sail>Fight>Kill>Capture>Repair loops.
    /// Not a MonoBehaviour — no Update() loop. Tests call methods directly.
    /// </summary>
    public class BotController
    {
        // ══════════════════════════════════════════════════════════════════
        //  References
        // ══════════════════════════════════════════════════════════════════

        private readonly ShipController _ship;
        private readonly BroadsideSystem _broadside;
        private readonly HPSystem _playerHp;
        private readonly EconomySystem _economy;

        // ══════════════════════════════════════════════════════════════════
        //  Metrics
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Number of enemies killed this session.</summary>
        public int KillCount { get; private set; }

        // ══════════════════════════════════════════════════════════════════
        //  Constructor
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Create a BotController wired to the specified game systems.
        /// </summary>
        /// <param name="ship">The player ShipController to drive.</param>
        /// <param name="broadside">The player BroadsideSystem to fire with.</param>
        /// <param name="playerHp">The player HPSystem to monitor and repair.</param>
        /// <param name="economy">The EconomySystem for gold operations.</param>
        public BotController(ShipController ship, BroadsideSystem broadside,
                             HPSystem playerHp, EconomySystem economy)
        {
            _ship      = ship;
            _broadside = broadside;
            _playerHp  = playerHp;
            _economy   = economy;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Movement
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Set throttle and rudder on the ShipController to steer toward a target.
        /// </summary>
        /// <param name="target">World-space destination position.</param>
        public void MoveToward(Vector3 target)
        {
            if (_ship == null) return;

            Vector3 toTarget = target - _ship.transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude < 1f)
            {
                _ship.SetThrottle(0f);
                _ship.SetRudder(0f);
                return;
            }

            float signedAngle = Vector3.SignedAngle(
                _ship.transform.forward, toTarget, Vector3.up);
            float rudder = Mathf.Clamp(signedAngle / 45f, -1f, 1f);

            _ship.SetThrottle(1f);
            _ship.SetRudder(rudder);
        }

        /// <summary>
        /// Bring the ship to a stop by zeroing throttle and rudder.
        /// </summary>
        public void StopMovement()
        {
            if (_ship == null) return;
            _ship.SetThrottle(0f);
            _ship.SetRudder(0f);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Combat
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Fire the port broadside if the target is in the port arc and ready.
        /// </summary>
        /// <param name="targetPosition">World-space position of the target.</param>
        public void FirePortBroadsideAt(Vector3 targetPosition)
        {
            if (_broadside == null) return;
            if (_broadside.IsInPortArc(targetPosition) && _broadside.PortReady)
                _broadside.FirePort();
        }

        /// <summary>
        /// Fire the starboard broadside if the target is in the starboard arc and ready.
        /// </summary>
        /// <param name="targetPosition">World-space position of the target.</param>
        public void FireStarboardBroadsideAt(Vector3 targetPosition)
        {
            if (_broadside == null) return;
            if (_broadside.IsInStarboardArc(targetPosition) && _broadside.StarboardReady)
                _broadside.FireStarboard();
        }

        /// <summary>
        /// Try to fire at a target — checks port arc first, then starboard.
        /// </summary>
        /// <param name="targetPosition">World-space position of the target.</param>
        /// <returns>True if either broadside fired successfully.</returns>
        public bool TryFireAtTarget(Vector3 targetPosition)
        {
            if (_broadside == null) return false;

            bool fired = false;

            if (_broadside.IsInPortArc(targetPosition) && _broadside.PortReady)
                fired |= _broadside.FirePort();

            if (_broadside.IsInStarboardArc(targetPosition) && _broadside.StarboardReady)
                fired |= _broadside.FireStarboard();

            return fired;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Port Capture
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Directly call CapturePort on the PortSystem for the given portId.
        /// </summary>
        /// <param name="portSystem">The central PortSystem.</param>
        /// <param name="portId">The ID of the port to capture.</param>
        /// <returns>True if the port was captured successfully.</returns>
        public bool CapturePort(PortSystem portSystem, string portId)
        {
            if (portSystem == null) return false;
            return portSystem.CapturePort(portId);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Repair
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Directly call Heal on the player HPSystem to restore HP.
        /// </summary>
        /// <param name="amount">Amount of HP to restore.</param>
        public void RepairShip(int amount)
        {
            if (_playerHp == null) return;
            _playerHp.Heal(amount);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Kill Tracking
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Subscribe to an enemy HPSystem's OnDestroyed event to increment KillCount.
        /// Safe to call multiple times — subscription is idempotent per instance.
        /// </summary>
        /// <param name="enemyHp">The enemy HPSystem to track.</param>
        public void RegisterEnemyKill(HPSystem enemyHp)
        {
            if (enemyHp == null) return;
            enemyHp.OnDestroyed += () => KillCount++;
        }
    }
}
