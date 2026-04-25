using Foundation;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// MonoBehaviour that applies steering forces to a Rigidbody.
    ///
    /// Responsibility Boundary:
    ///   SteeringBehaviors.cs -> pure math, returns desired velocity vectors
    ///   SteeringAgent.c s-> physics application layer (Unity glue)
    ///
    /// Movement Model — Direct Velocity Assignment:
    ///   We set Rigidbody.velocity each frame rather than using AddForce/MovePosition.
    ///   Rationale:
    ///     - AddForce accumulates over time; speed is hard to predict frame-to-frame.
    ///     - MovePosition bypasses physics and produces tunneling on thin colliders.
    ///     - Direct velocity gives crisp, predictable, per-frame-deterministic movement
    ///       while still allowing the Rigidbody to participate in collision response.
    ///   The Rigidbody is not kinematic for this exact reason — wall collisions slide
    ///   the agent naturally without requiring manual code.
    ///
    /// Obstacle Avoidance — Architectural Guarantee:
    ///   Every call to Move() computes and composites obstacle avoidance automatically.
    ///   There is no way to move an agent without avoidance being applied.
    ///   This satisfies the "every movement must factor in obstacle avoidance" constraint
    ///   at the architecture level, not through coding convention.
    ///
    ///   finalVelocity = ClampMagnitude(primaryVelocity + avoidance, maxSpeed)
    ///
    ///   Vector3.ClampMagnitude is used (not Mathf.Clamp per component) because it
    ///   preserves the direction of the combined vector while limiting total speed.
    ///   Per-component clamping would deform diagonal movement directions.
    ///
    /// Rotation:
    ///   The agent rotates to face its movement direction via Quaternion.Slerp.
    ///   We rotate toward the finalVelocity direction (after avoidance), not the
    ///   raw desired velocity. This ensures ConeLOS.forward stays aligned with
    ///   actual movement, so detection cones point where the agent is actually going.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("AI/Core/Steering Agent")]
    public class SteeringAgent : MonoBehaviour
    {
        [Header("Movement")] 
        [SerializeField] private float _maxSpeed = 5f;
        [SerializeField] private float _rotationSpeed = 10f;

        [Header("Obstacle Avoidance")] [SerializeField]
        private float _avoidDistance = 2.5f;

        [SerializeField] private float _avoidForce = 7f;
        [SerializeField] private float _whiskerAngle = 30f;
        [SerializeField] private LayerMask _obstacleLayer;

        private Rigidbody _rb;

        // ── Public Read Accessors ────────────────────────────────────────────
        public float MaxSpeed => _maxSpeed;
        public Vector3 CurrentVelocity => _rb != null ? _rb.velocity : Vector3.zero;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();

            // rotation manually via Quaternion.Slerp in Move() for precise control.
            _rb.constraints = RigidbodyConstraints.FreezePositionY
                              | RigidbodyConstraints.FreezeRotation;

            _rb.useGravity = false; // Gravity is irrelevant in a top-down flat world
        }

        // ── Public Movement API ──────────────────────────────────────────────

        /// <summary>
        /// Primary movement method. Must be called instead of setting
        /// transform.position or Rigidbody.MovePosition directly.
        ///
        /// Steps:
        ///   1. Compute obstacle avoidance correction vector.
        ///   2. Add correction to the primary desired velocity.
        ///   3. Clamp the combined vector to maxSpeed (preserving direction).
        ///   4. Assign to Rigidbody.velocity.
        ///   5. Rotate agent to face the final movement direction.
        /// </summary>
        public void Move(Vector3 desiredVelocity)
        {
            desiredVelocity.y = 0f; // Enforce XZ-plane movement

            // ── Step 1 & 2: Obstacle Avoidance (always applied) ─────────────
            Vector3 avoidance = SteeringBehaviors.ObstacleAvoidance(
                transform.position,
                transform.forward,
                _avoidDistance,
                _avoidForce,
                _obstacleLayer,
                _whiskerAngle);

            Vector3 combined = desiredVelocity + avoidance;
            combined.y = 0f;

            // ── Step 3: Speed Clamp ──────────────────────────────────────────
            // ClampMagnitude preserves the vector's direction, only limiting length.
            Vector3 finalVelocity = Vector3.ClampMagnitude(combined, _maxSpeed);

            // ── Step 4: Apply to Physics ─────────────────────────────────────
            _rb.velocity = finalVelocity;

            // ── Step 5: Rotation ─────────────────────────────────────────────
            // Only rotate when actually moving (sqrMagnitude avoids sqrt)
            if (finalVelocity.sqrMagnitude > 0.01f)
            {
                // LookRotation requires the forward vector; we use finalVelocity so the
                // agent always "looks" where it's actually going (post-avoidance direction)
                Quaternion target = Quaternion.LookRotation(finalVelocity, Vector3.up);

                // Slerp for smooth rotation; hard snap would cause the ConeLOS
                // forward vector to jitter rapidly on direction changes
                _rb.rotation = Quaternion.Slerp(
                    _rb.rotation, target, _rotationSpeed * Time.fixedDeltaTime);
            }
        }

        /// <summary>
        /// Immediately zeros velocity. Use when entering stationary states.
        /// </summary>
        public void Stop()
        {
            _rb.velocity = Vector3.zero;
        }

        // ── Configuration API ────────────────────────────────────────────────
        public void SetMaxSpeed(float speed) => _maxSpeed = Mathf.Max(0f, speed);
    }
}