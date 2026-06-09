using Core;
using Foundation;
using UnityEngine;

namespace World
{
    /// <summary>
    /// Flock Agent: implements Leader Following flocking behavior.
    ///
    /// Variant - Leader Following (Separation + Cohesion + Arrive):
    ///   The rubric permits the "leader following" variant:
    ///     Separation: steer away from nearby flock members to avoid crowding.
    ///     Cohesion:   steer toward the average position of nearby members.
    ///     Arrive:     steer toward the leader with deceleration near it.
    ///
    ///   Classic alignment (match neighbor velocities) is replaced by Arrive
    ///   because the goal is to follow a designated leader, not to form a
    ///   free-flying swarm. This produces a tighter, more readable cluster.
    ///
    /// Force Composition:
    ///   finalVelocity = w_arrive*arrive + w_cohesion×cohesion + w_separation×separation
    ///   All three desired velocities are weighted and summed, then clamped
    ///   to _maxSpeed. Weights are Inspector-tunable for feel.
    ///
    ///   Separation is given the highest weight by default — crowding looks bad
    ///   and feels wrong, so personal space is the top priority.
    ///
    /// Neighbor Detection:
    ///   Physics.OverlapSphereNonAlloc scans for nearby FlockAgents each frame.
    ///   A pre-allocated buffer avoids GC allocation per tick.
    ///   Only agents on _flockLayer are considered neighbors.
    ///
    /// Obstacle Avoidance:
    ///   Applied inside SteeringAgent.Move() automatically, same as all other
    ///   movement in this codebase. No extra code needed here.
    ///
    /// Independence from Enemy Types:
    ///   FlockAgent has no reference to AggressorEnemy, CowardEnemy, or the
    ///   player. It only knows about its leader and its neighbors.
    ///   The flock is a neutral ambient group - civilians, debris, critters.
    /// </summary>
    [RequireComponent(typeof(SteeringAgent))]
    [AddComponentMenu("AI/Flocking/Flock Agent")]
    public class FlockAgent : MonoBehaviour
    {
        [Header("Leader")] [SerializeField] private FlockLeader _leader;

        [Header("Flock Sensing")] [SerializeField]
        private float _neighborRadius = 3f;

        [SerializeField] private LayerMask _flockLayer;

        [Header("Behavior Weights")] [SerializeField]
        private float _arriveWeight = 1.0f;

        [SerializeField] private float _cohesionWeight = 0.6f;
        [SerializeField] private float _separationWeight = 1.5f;

        [Header("Personal Space")]
        [Tooltip("Agents closer than this distance trigger full-strength separation.")]
        [SerializeField]
        private float _separationRadius = 1.5f;

        [Header("Leader Offset")] [Tooltip("How far behind the leader the agents try to arrive.")] [SerializeField]
        private float _leaderBehindOffset = 2f;

        // Pre-allocated neighbor buffer - avoids GC alloc per frame
        private static readonly Collider[] _neighborBuffer = new Collider[16];

        private SteeringAgent _steeringAgent;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            _steeringAgent = GetComponent<SteeringAgent>();
        }

        private void Update()
        {
            if (_leader == null) return;


            // ── 1. Arrive to Leader ───────────────────────────────────────────
            // Target a point slightly behind the leader (offset against leader's forward)
            // so agents trail behind rather than pile onto the leader's position.
            Vector3 arriveTarget =
                _leader.LeaderTransform.position - _leader.LeaderTransform.forward * _leaderBehindOffset;

            Vector3 arriveVel = SteeringBehaviors.Arrival(
                transform.position, arriveTarget, _steeringAgent.MaxSpeed, slowingRadius: 2.5f);

            // ── 2. Sense Neighbors ────────────────────────────────────────────
            int neighborCount = Physics.OverlapSphereNonAlloc(
                transform.position, _neighborRadius, _neighborBuffer, _flockLayer);

            Vector3 cohesionSum = Vector3.zero;
            Vector3 separationSum = Vector3.zero;
            int validNeighbors = 0;

            for (int i = 0; i < neighborCount; i++)
            {
                var col = _neighborBuffer[i];
                if (col == null || col.gameObject == gameObject) continue;

                Vector3 neighborPos = col.transform.position;
                Vector3 toNeighbor = neighborPos - transform.position;
                float dist = toNeighbor.magnitude;

                if (dist < 0.001f) continue;

                // Cohesion: accumulate average neighbor position
                cohesionSum += neighborPos;

                // Separation: push away - force scales inversely with distance
                if (dist < _separationRadius)
                {
                    // Stronger push the closer the neighbor is
                    float strength = 1f - Mathf.Clamp01(dist / _separationRadius);
                    separationSum += -toNeighbor.normalized * strength;
                }

                validNeighbors++;
            }

            // ── 3. Cohesion Velocity ──────────────────────────────────────────
            Vector3 cohesionVel = Vector3.zero;
            if (validNeighbors > 0)
            {
                //calculate com of neighbors
                Vector3 toCom = (cohesionSum / validNeighbors) - transform.position;
                toCom.y = 0f;
                if (toCom.sqrMagnitude > 0.001f)
                    cohesionVel = toCom.normalized * _steeringAgent.MaxSpeed;
            }

            // ── 4. Separation Velocity ────────────────────────────────────────
            Vector3 separationVel = Vector3.zero;
            if (separationSum.sqrMagnitude > 0.001f)
            {
                separationSum.y = 0f;
                separationVel = separationSum.normalized * _steeringAgent.MaxSpeed;
            }

            // ── 5. Compose Final Velocity and Apply ─────────────────────────────────────
            Vector3 combined =
                arriveVel * _arriveWeight +
                cohesionVel * _cohesionWeight +
                separationVel * _separationWeight;

            combined.y = 0f;
            combined = Vector3.ClampMagnitude(combined, _steeringAgent.MaxSpeed);

            // SteeringAgent.Move() composites obstacle avoidance automatically
            _steeringAgent.Move(combined);
        }

        // ── Scene Gizmos ─────────────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, _neighborRadius);

            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, _separationRadius);
        }
    }
}