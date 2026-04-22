using System;

namespace World
{
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