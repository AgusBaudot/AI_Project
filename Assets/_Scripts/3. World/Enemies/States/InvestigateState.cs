using System;
using Core;
using Foundation;
using UnityEngine;

namespace World
{
    /// <summary>
    /// Investigate State: The agent stops moving and looks around (rotates left and right)
    /// for a set duration before giving up.
    /// 
    /// Perfect for stealth games: triggers when the agent reaches the last known 
    /// position of the player but can no longer see them.
    /// </summary>
    public sealed class InvestigateState<TKey> : IState<TKey> where TKey : struct, IEquatable<TKey>
    {
        public TKey StateKey { get; }

        private readonly SteeringAgent _agent;
        private readonly float _duration;
        private readonly float _lookSpeed;
        private readonly float _lookAngle;
        private readonly AIEventChannel _events;

        private float _timer;
        private float _timeInState;

        /// <summary>
        /// Fires when the investigation timer expires.
        /// </summary>
        public event Action OnInvestigateComplete;

        public InvestigateState(TKey key, SteeringAgent agent, 
            float duration = 4f, float lookSpeed = 3f, float lookAngle = 60f, AIEventChannel events = null)
        {
            StateKey = key;
            _agent = agent;
            _duration = duration;
            _lookSpeed = lookSpeed;
            _lookAngle = lookAngle;
            _events = events;
        }

        public void OnEnter()
        {
            _timer = _duration;
            _timeInState = 0f;
            _agent.Stop();
            _events?.RaiseStateChanged("Investigate");
        }

        public void OnTick(float deltaTime)
        {
            _timeInState += deltaTime;
            _timer -= deltaTime;

            
            float turnAmount = Mathf.Sin(_timeInState * _lookSpeed) * _lookAngle * deltaTime;
            _agent.transform.Rotate(0, turnAmount, 0);

            if (_timer <= 0f)
            {
                OnInvestigateComplete?.Invoke();
            }
        }

        public void OnExit()
        {
            _events?.RaiseStateChanged("GiveUp");
        }
    }
}