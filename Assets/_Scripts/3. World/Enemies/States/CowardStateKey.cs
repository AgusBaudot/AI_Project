using System;

namespace World
{
    /// <summary>
    /// State keys for the Coward enemy archetype.
    ///
    /// PathfindingFlee added: entered when the player is not visible but the
    /// Coward is still within safe escape distance - A* routes to the farthest
    /// reachable node away from the player.
    /// </summary>
    public readonly struct CowardStateKey : IEquatable<CowardStateKey>
    {
        private readonly int _value;
        private CowardStateKey(int value) => _value = value;

        public static readonly CowardStateKey Patrol = new(0);
        public static readonly CowardStateKey Idle = new(1);
        public static readonly CowardStateKey RunAway = new(2);
        public static readonly CowardStateKey PathfindingFlee = new(3);

        public bool Equals(CowardStateKey other) => _value == other._value;
        public override bool Equals(object obj) => obj is CowardStateKey other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public static bool operator ==(CowardStateKey l, CowardStateKey r) => l.Equals(r);
        public static bool operator !=(CowardStateKey l, CowardStateKey r) => !l.Equals(r);
    }
}