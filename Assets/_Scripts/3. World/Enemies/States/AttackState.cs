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
    /// Changes from original:
    ///   OnEnterCallback / OnExitCallback action hooks have been added so that
    ///   AggressorEnemy can detect "exited without landing" for dynamic roulette
    ///   weight tracking - without coupling this state to AggressorEnemy directly.
    ///   The state remains fully generic and reusable.
    /// </summary>
    public sealed class AttackState<TKey> : IState<TKey> where TKey : struct, IEquatable<TKey>
    {
        public TKey StateKey { get; }

        private readonly SteeringAgent _agent;
        private readonly Transform _playerTransform;
        private readonly float _criticalRangeSqr;
        private readonly Action _onAttackLanded;
        private readonly AIEventChannel _events;

        private Rigidbody _playerRb;
        private bool _attacked;

        // ── Lifecycle Hooks ───────────────────────────────────────────────────
        // Subscribed by AggressorEnemy to track dynamic roulette weight.
        // Optional - null-safe invocation.
        public event Action OnEnterCallback;
        public event Action OnExitCallback;

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
            _playerRb = _playerTransform != null
                ? _playerTransform.GetComponent<Rigidbody>()
                : null;

            _events?.RaiseAttackStarted(_agent.transform.position);
            _events?.RaiseStateChanged("Attack");
            OnEnterCallback?.Invoke();
        }

        public void OnTick(float deltaTime)
        {
            if (_playerTransform == null || _attacked) return;

            Vector3 myPos = _agent.transform.position;
            Vector3 playerPos = _playerTransform.position;

            float distSqr = (playerPos - myPos).sqrMagnitude;
            if (distSqr <= _criticalRangeSqr)
            {
                _attacked = true;
                _agent.Stop();
                _events?.RaiseAttackLanded(playerPos);
                _onAttackLanded?.Invoke();
                return;
            }

            Vector3 playerVelocity = _playerRb != null ? _playerRb.velocity : Vector3.zero;
            Vector3 desired = SteeringBehaviors.Pursuit(
                myPos, _agent.MaxSpeed, playerPos, playerVelocity);

            _agent.Move(desired);
        }

        public void OnExit()
        {
            if (!_attacked) _agent.Stop();
            OnExitCallback?.Invoke();
        }
    }
}