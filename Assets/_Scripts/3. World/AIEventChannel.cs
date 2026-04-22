using System;
using UnityEngine;

namespace World
{
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