using System;
using Foundation;
using Core;
using UnityEngine;

namespace World
{
    /// <summary>
    /// Attack State: closes on the player using Pursuit + Obstacle Avoidance,
    /// then triggers a game-ending interaction at critical distance.
    ///
    /// Pursuit over Seek:
    ///   A player moving laterally is a harder target than a stationary one.
    ///   Seek always heads for the player's current position — a laterally moving
    ///   player can outrun a Seek-based pursuer at equal speed simply by strafing.
    ///   Pursuit predicts and intercepts, making the Aggressor feel genuinely
    ///   threatening and "intelligent" without any extra code.
    ///
    /// Critical Distance Check:
    ///   sqrMagnitude comparison avoids a sqrt call every frame.
    ///   We compare: distSqr <= _criticalRangeSqr (precomputed in constructor).
    ///
    /// Game-End Hook:
    ///   _onAttackLanded is an injected Action delegate — no GameManager reference.
    ///   The concrete enemy (AggressorEnemy) provides the implementation,
    ///   keeping this state fully decoupled from game management.
    ///
    /// Velocity caching:
    ///   Player Rigidbody is cached on OnEnter for Pursuit's prediction math.
    /// </summary>
    public sealed class AttackState<TKey> : IState<TKey> where TKey : struct, IEquatable<TKey>
    {
        public TKey StateKey { get; }

        private readonly SteeringAgent _agent;
        private readonly Transform _playerTransform;
        private readonly float _criticalRangeSqr; // Precomputed to avoid sqrt per frame
        private readonly Action _onAttackLanded;
        private readonly AIEventChannel _events;

        private Rigidbody _playerRb; // Cached on enter
        private bool _attacked; // Guard: fire attack exactly once per entry

        public AttackState(TKey key, SteeringAgent agent, Transform playerTransform,
            float criticalRange, Action onAttackLanded, AIEventChannel events = null)
        {
            StateKey = key;
            _agent = agent;
            _playerTransform = playerTransform;
            _criticalRangeSqr = criticalRange * criticalRange;
            _onAttackLanded = onAttackLanded;
            _events = events;
        }

        public void OnEnter()
        {
            _attacked = false;

            // Cache player Rigidbody for velocity reads in Pursuit
            _playerRb = _playerTransform != null
                ? _playerTransform.GetComponent<Rigidbody>()
                : null;

            _events?.RaiseAttackStarted(_agent.transform.position);
            _events?.RaiseStateChanged("Attack");
        }

        public void OnTick(float deltaTime)
        {
            if (_playerTransform == null || _attacked) return;

            Vector3 myPos = _agent.transform.position;
            Vector3 playerPos = _playerTransform.position;

            // ── Critical Distance Check ───────────────────────────────────────
            // sqrMagnitude: avoids one sqrt per frame compared to Vector3.Distance
            float distSqr = (playerPos - myPos).sqrMagnitude;
            if (distSqr <= _criticalRangeSqr)
            {
                _attacked = true;
                _agent.Stop();
                _events?.RaiseAttackLanded(playerPos);
                _onAttackLanded?.Invoke(); // Trigger game-end logic
                return;
            }

            // ── Pursuit: intercept the player's predicted position ────────────
            Vector3 playerVelocity = _playerRb != null ? _playerRb.velocity : Vector3.zero;
            Vector3 desired = SteeringBehaviors.Pursuit(
                myPos, _agent.MaxSpeed, playerPos, playerVelocity);

            // SteeringAgent.Move() composites obstacle avoidance automatically
            _agent.Move(desired);
        }

        public void OnExit()
        {
            if (!_attacked) _agent.Stop();
        }
    }
}