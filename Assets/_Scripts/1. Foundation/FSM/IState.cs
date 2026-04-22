using System;

namespace Foundation
{
    public interface IState<TStateKey> where TStateKey : struct, IEquatable<TStateKey>
    {
        TStateKey StateKey { get; }
        void OnEnter();
        void OnTick(float deltaTime);
        void OnExit();
    }   
}