using System;
using System.Collections.Generic;
using UnityEngine;

namespace Foundation
{
    /// <summary>
    /// Highly decoupled, generic Finite State Machine.
    ///
    /// Design Rationale:
    ///   - States are keyed in a Dictionary<TStateKey, IState<TStateKey>> for
    ///     O(1) lookup. With enum keys this is essentially a flat array access.
    ///   - The FSM owns zero game logic. It is a pure lifecycle orchestrator:
    ///     it calls OnEnter/OnTick/OnExit at the correct moments and nothing else.
    ///   - Self-transition guard: TransitionTo() exits early if the requested key
    ///     equals the current state key. This is essential because the decision
    ///     tree fires every frame and would otherwise reset state timers, patrol
    ///     counters, etc. on every Update.
    ///   - AddState / Start are separated so all states can be registered before
    ///     any lifecycle callbacks fire, avoiding race conditions at startup.
    /// </summary>
    public class StateMachine<TStateKey> where TStateKey : struct, IEquatable<TStateKey>
    {
        private readonly Dictionary<TStateKey, IState<TStateKey>> _states = new();

        private IState<TStateKey> _currentState;
        private bool _isRunning;

        // ── Public Read API ──────────────────────────────────────────────────
        public TStateKey CurrentStateKey
            => _currentState != null ? _currentState.StateKey : default;

        public bool IsInState(TStateKey key)
            => _currentState != null && _currentState.StateKey.Equals(key);

        // ── Registration ──────────────────────────────────────────────────

        /// <summary>
        /// Registers a state. Must be called before Start().
        /// Throws on duplicate keys to force explicit FMS authorship.
        /// </summary>
        /// <param name="state"></param>
        public void AddState(IState<TStateKey> state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            if (_states.ContainsKey(state.StateKey))
                throw new InvalidOperationException(
                    $"[StateMachine] Duplicate state key: '{state.StateKey}'.");

            _states[state.StateKey] = state;
        }

        // ── Lifecycle ──────────────────────────────────────────────────
        
        /// <summary>
        /// Boots the FSM and calls OnEnter on the initial state.
        /// Call this after all states have been registered via AddState().
        /// </summary>
        /// <param name="initialKey"></param>
        public void Start(TStateKey initialKey)
        {
            if (!_states.TryGetValue(initialKey, out _currentState))
                throw new KeyNotFoundException($"[StateMachine] Initial state key '{initialKey}' not registered.");

            _isRunning = true;
            _currentState.OnEnter();
        }

        /// <summary>
        /// Advances the active state. Driven from MonoBehaviour.Update().
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!_isRunning || _currentState == null) return;
            _currentState.OnTick(deltaTime);
        }

        /// <summary>
        /// Requests a transition to a new state by key.
        ///
        /// Self-Transition Guard:
        ///   If the requested key == current key, we return immediately.
        ///   Without this guard, the decision tree (which fires every frame)
        ///   would call OnExit/OnEnter 60+ times per second, resetting timers
        ///   and patrol counters on every frame the condition holds true.
        /// </summary>
        public void TransitionTo(TStateKey newKey)
        {
            // Guard: ignore self-transitions silently
            if (_currentState != null && _currentState.StateKey.Equals(newKey))
                return;

            if (!_states.TryGetValue(newKey, out var nextState))
            {
                Debug.LogWarning($"[StateMachine] TransitionTo('{newKey}') failed: key not registered.");
                return;
            }

            _currentState?.OnExit();
            _currentState = nextState;
            _currentState.OnEnter();
        }

        /// <summary>
        /// Halts the FSM, calling OnExit on the active state.
        /// </summary>
        public void Stop()
        {
            _currentState?.OnExit();
            _currentState = null;
            _isRunning = false;
        }
    }
}