using System;
using Foundation;
using Core;

namespace World
{
    public sealed class EnemyIdleState<TKey> : IState<TKey> where TKey : struct, IEquatable<TKey>
    {
        public TKey StateKey { get; }

        private readonly SteeringAgent _agent;
        private readonly AIEventChannel _events;
        private readonly float _idleDuration;

        private float _timer;

        /// <summary>
        /// Fires when the idle timer expires. Subscribe in the enemy class.
        /// </summary>
        public event Action OnIdleComplete;

        public EnemyIdleState(TKey key, SteeringAgent agent,
            float idleDuration = 3f, AIEventChannel events = null)
        {
            StateKey = key;
            _agent = agent;
            _idleDuration = idleDuration;
            _events = events;
        }

        public void OnEnter()
        {
            _timer = _idleDuration;
            _agent.Stop();
            _events?.RaiseStateChanged("Idle");
        }

        public void OnTick(float deltaTime)
        {
            _timer -= deltaTime;
            if (_timer <= 0f)
                OnIdleComplete?.Invoke();
        }

        public void OnExit()
        {
            _events?.RaiseStateChanged("ResumePatrol");
        }
    }
}