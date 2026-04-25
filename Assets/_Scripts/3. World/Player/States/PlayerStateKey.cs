using System;

namespace World
{
    /// <summary>
    /// A zero-allocation, struct-based alternative to a standard enum for the Player's FSM.
    /// 
    /// Standard enums cause hidden memory allocations (boxing) when passed into generic 
    /// interfaces like IEquatable<T>. By using a readonly struct, we satisfy the generic 
    /// constraints of the StateMachine without generating any GC overhead 
    /// during state comparisons or dictionary lookups.
    /// </summary>
    public readonly struct PlayerStateKey : IEquatable<PlayerStateKey>
    {
        private readonly int _value;
        private PlayerStateKey(int value) => _value = value;

        public static readonly PlayerStateKey Idle = new PlayerStateKey(0);
        public static readonly PlayerStateKey Walk = new PlayerStateKey(1);

        public bool Equals(PlayerStateKey other) => _value == other._value;
        public override bool Equals(object obj) => obj is PlayerStateKey other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public static bool operator ==(PlayerStateKey left, PlayerStateKey right) => left.Equals(right);
        public static bool operator !=(PlayerStateKey left, PlayerStateKey right) => !left.Equals(right);
    }
}