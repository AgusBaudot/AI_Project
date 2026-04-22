using UnityEngine;

namespace World
{
    [AddComponentMenu("AI/World/Patrol Route")]
    public class PatrolRoute : MonoBehaviour
    {
        [SerializeField] private Transform[] _waypoints;

        public int WaypointCount => _waypoints?.Length ?? 0;

        public Vector3 GetWaypoint(int index)
        {
            if (_waypoints == null || _waypoints.Length == 0)
                return transform.position;

            int clamped = Mathf.Clamp(index, 0, _waypoints.Length - 1);
            return _waypoints[clamped] != null
                ? _waypoints[clamped].position
                : transform.position;
        }

        // ── Scene Gizmos ─────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            if (_waypoints == null || _waypoints.Length < 2) return;

            Gizmos.color = new Color(0f, 1f, 0.3f, 0.8f);
            for (int i = 0; i < _waypoints.Length - 1; i++)
            {
                if (_waypoints[i] != null && _waypoints[i + 1] != null)
                    Gizmos.DrawLine(_waypoints[i].position, _waypoints[i + 1].position);
            }

            Gizmos.color = Color.green;
            foreach (var wp in _waypoints)
            {
                if (wp != null)
                    Gizmos.DrawSphere(wp.position, 0.2f);
            }
        }
    }
}