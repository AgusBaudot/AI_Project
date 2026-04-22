using System;
using Foundation;
using Core;
using UnityEngine;

namespace World
{
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