using System;

namespace World
{
    public readonly struct CowardStateKey : IEquatable<CowardStateKey>
    {
        private readonly int _value;
        private CowardStateKey(int value) => _value = value;

        public static readonly CowardStateKey Patrol = new CowardStateKey(0);
        public static readonly CowardStateKey Idle = new CowardStateKey(1);
        public static readonly CowardStateKey RunAway = new CowardStateKey(2);

        public bool Equals(CowardStateKey other) => _value == other._value;
        public override bool Equals(object obj) => obj is CowardStateKey other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public static bool operator ==(CowardStateKey left, CowardStateKey right) => left.Equals(right);
        public static bool operator !=(CowardStateKey left, CowardStateKey right) => !left.Equals(right);
    }
}