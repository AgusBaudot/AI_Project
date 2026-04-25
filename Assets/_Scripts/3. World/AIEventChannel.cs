using System;
using UnityEngine;

namespace World
{
    /// <summary>
    /// ScriptableObject event bus for AI simulation hooks.
    ///
    /// Pattern — Observer (decoupled):
    ///   AI states fire events into this channel; particle systems, audio
    ///   managers, and UI subscribe to them. Neither side holds a direct
    ///   reference to the other. Adding a new VFX listener requires zero
    ///   changes to AI code.
    ///
    /// ScriptableObject as channel (not singleton):
    ///   The channel exists as a Project asset, assignable in the Inspector.
    ///   Multiple enemy groups can share one channel or have separate channels
    ///   (e.g., one per group for spatially scoped audio).
    ///   This avoids the Service Locator / Singleton pitfalls entirely.
    ///
    /// Optional usage:
    ///   Every AI state that fires events guards with null-conditional (?.)
    ///   so the channel can be left unassigned in the Inspector without errors.
    /// </summary>
    [CreateAssetMenu(menuName = "AI/Event Channel", fileName = "AIEventChannel")]
    public class AIEventChannel : ScriptableObject
    {
        // Fired whenever an AI agent changes state (string = state name)
        public event Action<string> OnStateChanged;

        // Fired when a Coward begins fleeing (Vector3 = flee origin)
        public event Action<Vector3> OnFleeStarted;

        // Fired when an Aggressor enters Attack state (Vector3 = attacker position)
        public event Action<Vector3> OnAttackStarted;

        // Fired when an attack lands (critical distance reached)
        public event Action<Vector3> OnAttackLanded;

        public void RaiseStateChanged(string stateName) => OnStateChanged?.Invoke(stateName);
        public void RaiseFleeStarted(Vector3 position) => OnFleeStarted?.Invoke(position);
        public void RaiseAttackStarted(Vector3 position) => OnAttackStarted?.Invoke(position);
        public void RaiseAttackLanded(Vector3 position) => OnAttackLanded?.Invoke(position);
    }
}