using System;
using Foundation;
using Core;
using UnityEngine;

namespace World
{
    /// <summary>
    /// RunAway State: Flee + Evasion blended steering with automatic obstacle avoidance.
    ///
    /// Blend Rationale — Why not pure Flee or pure Evasion?
    ///   Pure Flee reacts to where the threat is now. Against a fast pursuer heading
    ///   directly at you, Flee can accidentally run toward the pursuer's future position.
    ///   Pure Evasion predicts the pursuer's future position and flees that point,
    ///   which is optimal but visually "reads" as too mechanical.
    ///   A weighted blend (evasionBlend epsilon [0,1]) creates flight that is both reactive
    ///   and predictive, tunable per enemy type:
    ///     Cowards: evasionBlend approx 0.8 (very evasive, hard to corner)
    ///
    /// Math:
    ///   desired = Lerp(Flee(...), Evasion(...), evasionBlend)
    ///   Vector3.Lerp is appropriate here because both inputs are speed-clamped
    ///   desired velocities of the same magnitude — the blend is directional.
    ///
    /// Obstacle avoidance is applied by SteeringAgent.Move() automatically.
    ///
    /// Velocity caching:
    ///   pursuerRb is cached on OnEnter to avoid GetComponent allocations every frame.
    /// </summary>
    public sealed class RunAwayState<TKey> : IState<TKey> where TKey : struct, IEquatable<TKey>
    {
        public TKey StateKey { get; }

        private readonly SteeringAgent _agent;
        private readonly Transform _pursuerTransform;
        private readonly float _evasionBlend;
        private readonly AIEventChannel _events;

        private Rigidbody _pursuerRb; // Cached to avoid per-frame GetComponent

        public RunAwayState(TKey key, SteeringAgent agent, Transform pursuerTransform,
            float evasionBlend = 0.6f, AIEventChannel events = null)
        {
            StateKey = key;
            _agent = agent;
            _pursuerTransform = pursuerTransform;
            _evasionBlend = Mathf.Clamp01(evasionBlend);
            _events = events;
        }

        public void OnEnter()
        {
            // Cache the pursuer's Rigidbody once on enter instead of every tick
            _pursuerRb = _pursuerTransform != null
                ? _pursuerTransform.GetComponent<Rigidbody>()
                : null;

            _events?.RaiseFleeStarted(_agent.transform.position);
            _events?.RaiseStateChanged("RunAway");
        }

        public void OnTick(float deltaTime)
        {
            if (_pursuerTransform == null) return;

            Vector3 myPos = _agent.transform.position;
            Vector3 pursuerPos = _pursuerTransform.position;
            Vector3 pursuerVel = _pursuerRb != null ? _pursuerRb.velocity : Vector3.zero;
            float mySpeed = _agent.MaxSpeed;

            // Compute both behaviors independently
            Vector3 fleeResult = SteeringBehaviors.Flee(myPos, pursuerPos, mySpeed);
            Vector3 evadeResult = SteeringBehaviors.Evasion(myPos, mySpeed, pursuerPos, pursuerVel);

            // Blend: 0 = pure flee (reactive), 1 = pure evasion (predictive)
            Vector3 desired = Vector3.Lerp(fleeResult, evadeResult, _evasionBlend);

            // SteeringAgent.Move() composites obstacle avoidance automatically
            _agent.Move(desired);
        }

        public void OnExit() => _agent.Stop();
    }
}