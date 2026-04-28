using UnityEngine;

namespace Foundation
{
    /// <summary>
    /// Pure-static library of steering behavior functions.
    ///
    /// Design Philosophy:
    ///   Every method returns a "desired velocity" — not a force delta.
    ///   The actual steering force is:
    ///     steeringForce = desiredVelocity − currentVelocity
    ///   but we let SteeringAgent (the application layer) handle that math,
    ///   keeping these functions as clean mathematical primitives.
    ///
    ///   All methods operate in the XZ plane (Y forced to 0) because the game
    ///   uses 3D physics on a flat top-down surface.
    ///
    /// Composition model:
    ///   Callers combine multiple desired velocities before applying them.
    ///   ObstacleAvoidance is ALWAYS composited inside SteeringAgent.Move(),
    ///   so it never needs to be called manually — it is architecturally guaranteed.
    /// </summary>
    public static class SteeringBehaviors
    {
        // ── Pre-allocated Buffer ─────────────────────────────────────────────
        // Allocating this once statically prevents the Physics.OverlapSphere 
        // from generating garbage collection spikes every frame.
        private static readonly Collider[] _avoidanceHitBuffer = new Collider[10];

        // SEEK
        // Move directly toward a target position at maximum speed.
        //
        // Math:
        //   desiredVelocity = normalize(target − position) × maxSpeed
        //
        // Use: Foundation for Pursuit; rarely useful alone because it
        //      overshoots and produces jitter at the target point.
        public static Vector3 Seek(Vector3 position, Vector3 target, float maxSpeed)
        {
            Vector3 toTarget = target - position;
            toTarget.y = 0f;

            // sqrMagnitude threshold avoids a sqrt and prevents normalization
            // of a near-zero vector (which would produce NaN or Infinity)
            if (toTarget.sqrMagnitude < 0.0001f) return Vector3.zero;

            return toTarget.normalized * maxSpeed;
        }

        // FLEE
        // Move directly away from a threat position at maximum speed.
        //
        // Math:
        //   desiredVelocity = normalize(position − threat) × maxSpeed
        //   This is simply Seek with the direction vector negated.
        //
        // Limitation: Flee reacts to the threat's CURRENT position.
        //   Against a fast pursuer this causes the agent to run toward
        //   where the pursuer will be. Use Evasion for smarter flight.
        public static Vector3 Flee(Vector3 position, Vector3 threat, float maxSpeed)
        {
            Vector3 away = position - threat;
            away.y = 0f;

            if (away.sqrMagnitude < 0.0001f) return Vector3.zero;

            return away.normalized * maxSpeed;
        }

        // ARRIVAL
        // Seek a target but decelerate smoothly inside a "slowing radius."
        //
        // Math:
        //   if distance > slowingRadius: desiredSpeed = maxSpeed  (full speed)
        //   if distance <= slowingRadius: desiredSpeed = maxSpeed × (d / slowingRadius)
        //
        // Why linear scaling inside the radius?
        //   A linear ramp produces a constant-deceleration profile, which looks
        //   natural and prevents the overshooting / oscillation that pure Seek
        //   exhibits at waypoints. Quadratic or exponential ramps overshoot less
        //   but are harder to tune;
        public static Vector3 Arrival(Vector3 position, Vector3 target,
            float maxSpeed, float slowingRadius = 2f)
        {
            Vector3 toTarget = target - position;
            toTarget.y = 0f;

            float distance = toTarget.magnitude;
            if (distance < 0.05f) return Vector3.zero; // Close enough: stop

            float desiredSpeed = distance < slowingRadius
                ? maxSpeed * (distance / slowingRadius)
                : maxSpeed;

            return toTarget.normalized * desiredSpeed;
        }

        // PURSUIT
        // Chase a moving target by seeking its predicted future position.
        //
        // Math (delegates to Predictor):
        //   predictedPos  = targetPos + targetVelocity × (distance / agentSpeed)
        //   desiredVelocity = Seek(position, predictedPos, maxSpeed)
        //
        // Why prediction beats Seek for pursuit?
        //   Seek chases where the target IS; the target moves away faster than
        //   the agent can close the gap if both have similar speeds. Prediction
        //   moves toward where the target WILL BE, cutting off escape routes.
        public static Vector3 Pursuit(
            Vector3 position, float agentMaxSpeed,
            Vector3 targetPosition, Vector3 targetVelocity)
        {
            Vector3 predicted = Predictor.PredictPosition(
                targetPosition, targetVelocity, position, agentMaxSpeed);

            return Seek(position, predicted, agentMaxSpeed);
        }


        // EVASION
        // Flee from a pursuer by fleeing its predicted future position.
        //
        // Math (delegates to Predictor):
        //   predictedThreat = pursuerPos + pursuerVelocity × (distance / agentSpeed)
        //   desiredVelocity = Flee(position, predictedThreat, maxSpeed)
        //
        // Why not plain Flee?
        //   Plain Flee from a pursuer heading right toward you causes you to
        //   run into the pursuer's future path. Evading the predicted position
        //   steers away from where the threat will be — a much harder target.
        public static Vector3 Evasion(
            Vector3 position, float agentMaxSpeed,
            Vector3 pursuerPosition, Vector3 pursuerVelocity)
        {
            Vector3 predictedThreat = Predictor.PredictPosition(
                pursuerPosition, pursuerVelocity, position, agentMaxSpeed);

            return Flee(position, predictedThreat, agentMaxSpeed);
        }

        // OBSTACLE AVOIDANCE (Volumetric Tangent Pattern with Outward Bias)
        //
        // Algorithm:
        //   1. Cast a spherical net (OverlapSphere) to detect all nearby geometry within the radius.
        //   2. Filter the results: Ignore obstacles outside the forward FOV cone (with a 20° 
        //      peripheral buffer so the agent doesn't "forget" walls it is actively scraping).
        //   3. Find the closest valid surface point on the nearest obstacle.
        //   4. Determine if the bulk of the obstacle is to the agent's local left or right.
        //   5. Calculate a Tangent Vector to "slide" parallel to the wall (Cross Product).
        //   6. Calculate an Outward Bias to actively push away from the wall (Corner Clearance).
        //   7. Scale the final blended escape vector based on proximity [0 = far edge, 1 = breached].
        //
        // Why Volumetric (Sphere) instead of Whiskers (Rays)?
        //   Whiskers have blind spots. Even with thick SphereCasts, whiskers can hit a 
        //   massive flat wall uniformly, causing the math to deadlock ("perfect cancellation").
        //   A volumetric sphere guarantees detection of any geometry in the area.
        //
        // Why Cross Products instead of Surface Normals?
        //   Surface normals push purely AWAY from a wall. If an agent walks directly into a flat 
        //   wall, the normal pushes directly backwards, cancelling momentum and causing freezing. 
        //   A Cross Product calculates a TANGENT, ensuring the agent always slides sideways.
        //
        // Returns: 
        //   An additive avoidance vector (Direction * Force). This is the raw required "push".
        //   The caller is responsible for blending this with its desired velocity.
        private static readonly Collider[] _collidersBuffer = new Collider[10];

        public static Vector3 ObstacleAvoidance(
            Vector3 position,
            Vector3 forward,
            float avoidRadius,
            float avoidForce,
            LayerMask obstacleLayer,
            float fovAngle = 180f,
            float personalArea = 0.5f,
            float cornerClearance = 0.6f)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) return Vector3.zero;
            forward.Normalize();

            int count = Physics.OverlapSphereNonAlloc(position, avoidRadius, _collidersBuffer, obstacleLayer);

            Collider nearColl = null;
            float nearCollDistance = float.MaxValue;
            Vector3 nearClosestPoint = Vector3.zero;

            for (int i = 0; i < count; i++)
            {
                var currColl = _collidersBuffer[i];

                Vector3 closestPoint = currColl.ClosestPoint(position);
                closestPoint.y = position.y;

                float distance = (closestPoint - position).magnitude;

                if (distance < 0.001f) continue;

                float currAngle = Vector3.Angle(forward, (closestPoint - position).normalized);

                if (currAngle > (fovAngle / 2f) + 20f) continue;

                if (nearColl == null || distance < nearCollDistance)
                {
                    nearColl = currColl;
                    nearCollDistance = distance;
                    nearClosestPoint = closestPoint;
                }
            }

            if (nearColl == null) return Vector3.zero;

            Vector3 dirToClosestPoint = (nearClosestPoint - position).normalized;
            Vector3 rightLateral = Vector3.Cross(Vector3.up, forward).normalized;

            Vector3 tangentDir = Vector3.Dot(rightLateral, dirToClosestPoint) < 0
                ? Vector3.Cross(Vector3.up, dirToClosestPoint).normalized
                : -Vector3.Cross(Vector3.up, dirToClosestPoint).normalized;

            float proximity = 1f - Mathf.Clamp01((nearCollDistance - personalArea) / (avoidRadius - personalArea));

            return (tangentDir + (-dirToClosestPoint * cornerClearance)).normalized * (avoidForce * proximity);
        }
    }
}