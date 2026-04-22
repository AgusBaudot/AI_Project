using System;
using Core;
using Foundation;
using UnityEngine;

namespace World
{
    public sealed class PatrolState<TKey> : IState<TKey> where TKey : struct, IEquatable<TKey>
    {
        public TKey StateKey { get; }

        private readonly SteeringAgent _agent;
        private readonly PatrolRoute _route;
        private readonly float _arriveThreshold;

        private int _waypointIndex = 0;
        private int _direction = 1; // +1 = forward, -1 = reverse
        private int _patrolCycleCount = 0;
        private float _patrolSpeed;

        // Read by the decision tree to know when to idle
        public int PatrolCycleCount => _patrolCycleCount;

        public PatrolState(TKey key, SteeringAgent agent, PatrolRoute route,
            float patrolSpeed, float arriveThreshold = 0.4f)
        {
            StateKey = key;
            _agent = agent;
            _route = route;
            _patrolSpeed = patrolSpeed;
            _arriveThreshold = arriveThreshold;
        }

        public void OnEnter()
        {
            /* Resume from last waypoint — no reset needed */
        }

        public void OnExit() => _agent.Stop();

        public void OnTick(float deltaTime)
        {
            if (_route == null || _route.WaypointCount == 0) return;

            Vector3 target = _route.GetWaypoint(_waypointIndex);
            Vector3 position = _agent.transform.position;

            // Arrival gives a smooth deceleration curve into the waypoint.
            // slowingRadius = 1.5f means deceleration starts 1.5 units out.
            Vector3 desired = SteeringBehaviors.Arrival(
                position, target, _patrolSpeed, slowingRadius: 1.5f);

            // SteeringAgent.Move() composites obstacle avoidance automatically
            _agent.Move(desired);

            // ── Waypoint Arrival Check ────────────────────────────────────────
            // sqrMagnitude avoids a sqrt — comparing squared values is equivalent
            // and cheaper when we only need a threshold comparison
            float distSqr = (position - target).sqrMagnitude;
            if (distSqr < _arriveThreshold * _arriveThreshold)
                AdvanceWaypoint();
        }

        private void AdvanceWaypoint()
        {
            int next = _waypointIndex + _direction;

            if (next >= _route.WaypointCount)
            {
                // Hit the forward end — reverse direction and count a half-cycle
                _direction = -1;
                _waypointIndex = Mathf.Max(0, _route.WaypointCount - 2);
                _patrolCycleCount++;
            }
            else if (next < 0)
            {
                // Hit the backward end — reverse again
                _direction = 1;
                _waypointIndex = Mathf.Min(1, _route.WaypointCount - 1);
                _patrolCycleCount++;
            }
            else
            {
                _waypointIndex = next;
            }
        }

        // Called by the concrete enemy when returning from Idle
        public void ResetCycleCount() => _patrolCycleCount = 0;
        public void SetPatrolSpeed(float speed) => _patrolSpeed = Mathf.Max(0.1f, speed);
    }
}