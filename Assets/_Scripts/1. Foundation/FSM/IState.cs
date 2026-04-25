using System;

namespace Foundation
{
    /// <summary>
    /// Generic state contract for the Finite State Machine.
    ///
    /// Generic Constraint — why "struct, IEquatable":
    ///   Constraining TStateKey to 'struct' ensures enums and value types are
    ///   accepted without boxing, keeping dictionary lookups allocation-free.
    ///   IEquatable<T> enables direct structural equality checks in the FSM
    ///   without falling back to the virtual object.Equals() path, which would
    ///   box the value type on every self-transition guard.
    ///
    /// Lifecycle contract:
    ///   OnEnter  → called exactly once when the FSM arrives at this state.
    ///   OnTick   → called every frame while this state is active (driven by FSM.Tick).
    ///   OnExit   → called exactly once when the FSM departs from this state.
    ///   States must be self-contained: they should never reach into the FSM
    ///   to trigger their own transitions; that belongs to the decision layer.
    /// </summary>
    public interface IState<TStateKey> where TStateKey : struct, IEquatable<TStateKey>
    {
        TStateKey StateKey { get; }
        void OnEnter();
        void OnTick(float deltaTime);
        void OnExit();
    }   
}