using Core;
using Foundation;
using UnityEngine;

namespace World
{
    /// <summary>
    /// Flock Leader: wanders the map by picking random waypoints and moving to them
    /// with Arrival steering. FlockAgent instances track this transform.
    ///
    /// Wandering Strategy - Bounded Random Waypoints:
    ///   On arrival at each waypoint, a new one is chosen randomly within
    ///   _wanderRadius of the leader's current position, projected onto the XZ
    ///   plane. A Physics.OverlapSphere check rejects candidates that would
    ///   land inside obstacles, giving the leader a natural drifting pattern
    ///   that avoids walls without needing full pathfinding.
    ///
    ///   This is intentionally lightweight: the leader is a passive reference
    ///   point for the flock, not an AI threat, so it doesn't need A* or FSM.
    ///
    /// Exposed References:
    ///   FlockAgent reads LeaderTransform to compute arrive-to-leader. 
    ///   Velocity is exposed so FlockAgent can optionally align to leader direction.
    ///
    /// Why not use SteeringAgent here?
    ///   The leader has no obstacle avoidance requirement (it picks waypoints
    ///   that avoid obstacles) and no FSM. A direct Rigidbody drive is simpler
    ///   and keeps the leader from interfering with the flock's own avoidance.
    /// </summary>
    [RequireComponent(typeof(SteeringAgent))]
    [AddComponentMenu("AI/Flocking/Flock Leader")]
    public class FlockLeader : MonoBehaviour
    {
        [Header("Wander Settings")] [SerializeField]
        private float _moveSpeed = 3f;

        [SerializeField] private float _wanderRadius = 8f;
        [SerializeField] private float _arriveThreshold = 0.5f;

        [Header("Obstacle Rejection")] [SerializeField]
        private LayerMask _obstacleLayer;

        [SerializeField] private float _obstacleCheckRadius = 0.6f;

        [Tooltip("How many attempts to find a clear waypoint before giving up and waiting.")] [SerializeField]
        private int _maxWaypointAttempts = 8;

        private SteeringAgent _steeringAgent;
        private Vector3 _currentWaypoint;

        // ── Public API ───────────────────────────────────────────────────────

        public Transform LeaderTransform => transform;

        public Vector3 Velocity => _steeringAgent != null
            ? _steeringAgent.CurrentVelocity
            : Vector3.zero;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            _steeringAgent = GetComponent<SteeringAgent>();
            _steeringAgent.SetMaxSpeed(_moveSpeed);
        }

        private void Start()
        {
            _currentWaypoint = PickWaypoint();
        }

        private void FixedUpdate()
        {
            MoveTowardsWaypoint();
        }

        // ── Movement ─────────────────────────────────────────────────────────

        private void MoveTowardsWaypoint()
        {
            Vector3 pos = transform.position;
            float distSqr = (pos - _currentWaypoint).sqrMagnitude;

            if (distSqr < _arriveThreshold * _arriveThreshold)
            {
                _currentWaypoint = PickWaypoint();
                return;
            }

            // Arrival: smooth deceleration.
            Vector3 desired = SteeringBehaviors.Arrival(
                pos, _currentWaypoint, _moveSpeed, slowingRadius: 1.5f);

            _steeringAgent.Move(desired);
        }

        // ── Waypoint Selection ────────────────────────────────────────────────

        /// <summary>
        /// Picks a random point within _wanderRadius that doesn't overlap an obstacle.
        /// Falls back to current position on all-fail (leader pauses briefly).
        /// </summary>
        private Vector3 PickWaypoint()
        {
            for (int attempt = 0; attempt < _maxWaypointAttempts; attempt++)
            {
                // Random point on XZ disk around current position
                Vector2 rand2D = Random.insideUnitCircle * _wanderRadius;
                Vector3 candidate = transform.position + new Vector3(rand2D.x, 0f, rand2D.y);

                // Reject if overlapping an obstacle
                if (!Physics.CheckSphere(candidate, _obstacleCheckRadius, _obstacleLayer))
                    return candidate;
            }

            // All attempts failed — stay put (will retry next arrival)
            return transform.position;
        }

        // ── Scene Gizmos ─────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            // Wander radius
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.15f);
            Gizmos.DrawSphere(transform.position, _wanderRadius);

            // Current waypoint
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(_currentWaypoint, 0.25f);
            Gizmos.DrawLine(transform.position, _currentWaypoint);
        }
    }
}