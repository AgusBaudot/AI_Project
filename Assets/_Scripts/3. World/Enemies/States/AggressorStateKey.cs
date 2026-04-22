using System;

namespace World
{
    public readonly struct AggressorStateKey : IEquatable<AggressorStateKey>
    {
        private readonly int _value;
        private AggressorStateKey(int value) => _value = value;

        public static readonly AggressorStateKey Patrol = new AggressorStateKey(0);
        public static readonly AggressorStateKey Idle = new AggressorStateKey(1);
        public static readonly AggressorStateKey Attack = new AggressorStateKey(2);

        public bool Equals(AggressorStateKey other) => _value == other._value;
        public override bool Equals(object obj) => obj is AggressorStateKey other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public static bool operator ==(AggressorStateKey left, AggressorStateKey right) => left.Equals(right);
        public static bool operator !=(AggressorStateKey left, AggressorStateKey right) => !left.Equals(right);
    }
}