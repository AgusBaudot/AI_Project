using System;
using Foundation;
using Core;

namespace World
{
    /// <summary>
    /// Idle State: enemy halts for a fixed duration, then signals completion.
    ///
    /// Timer Implementation:
    ///   A simple float countdown driven by deltaTime in OnTick.
    ///   We deliberately avoid Coroutines here to keep the state:
    ///     - Self-contained (no MonoBehaviour dependency injected)
    ///     - Testable (OnTick can be called with arbitrary deltaTime values)
    ///     - FSM-safe (Coroutines survive state exits unless manually stopped)
    ///
    /// Completion Signal:
    ///   OnIdleComplete is an event subscribed to by the concrete enemy class,
    ///   which resets patrol counters and transitions back to Patrol.
    ///   The state does NOT call FSM.TransitionTo() directly — it only signals.
    ///   This preserves state isolation: states don't know about other states.
    /// </summary>
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