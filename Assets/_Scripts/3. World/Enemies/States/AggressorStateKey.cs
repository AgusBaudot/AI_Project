using System;

namespace World
{
    /// <summary>
    /// State keys for the Aggressor enemy archetype.
    ///
    /// PathfindingChase added: entered when the player is not visible and the
    /// Aggressor pursues using A* instead of Pursuit steering.
    /// </summary>
    public readonly struct AggressorStateKey : IEquatable<AggressorStateKey>
    {
        private readonly int _value;
        private AggressorStateKey(int value) => _value = value;

        public static readonly AggressorStateKey Patrol = new(0);
        public static readonly AggressorStateKey Idle = new(1);
        public static readonly AggressorStateKey Attack = new(2);
        public static readonly AggressorStateKey PathfindingChase = new(3);
        public static readonly AggressorStateKey Investigate = new(4);

        public bool Equals(AggressorStateKey other) => _value == other._value;
        public override bool Equals(object obj) => obj is AggressorStateKey other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public static bool operator ==(AggressorStateKey l, AggressorStateKey r) => l.Equals(r);
        public static bool operator !=(AggressorStateKey l, AggressorStateKey r) => !l.Equals(r);
    }
}