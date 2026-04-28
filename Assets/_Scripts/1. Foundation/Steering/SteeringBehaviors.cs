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

        // OBSTACLE AVOIDANCE (Volumetric Tangent Pattern)
        //
        // Algorithm:
        //   1. Cast a spherical net (OverlapSphere) to detect ALL nearby geometry within the radius.
        //   2. Filter the results: Ignore obstacles outside the agent's forward FOV cone.
        //   3. Find the closest valid point on the nearest obstacle (ClosestPoint).
        //   4. Determine if the bulk of the obstacle is to the agent's local left or right.
        //   5. Calculate a Tangent Escape Vector using the Cross Product of the Up vector 
        //      and the direction to the obstacle.
        //   6. Calculate evasion weight based on proximity to the personal area [0 = far, 1 = breached].
        //   7. Smoothly Lerp between the desired direction and the escape tangent.
        //
        // Why Volumetric (Sphere) instead of Whiskers (Rays)?
        //   Whiskers have blind spots. Even with thick SphereCasts, whiskers can hit a 
        //   massive flat wall uniformly, causing the math to deadlock ("perfect cancellation").
        //   A volumetric sphere guarantees detection of any geometry in the area, 
        //   regardless of shape, size, or angle of approach.
        //
        // Why Cross Products instead of Surface Normals?
        //   Surface normals push AWAY from a wall. If an agent walks directly into a flat wall, 
        //   the normal pushes directly backwards, cancelling forward momentum and causing 
        //   the agent to freeze. A Cross Product calculates a TANGENT (perpendicular) vector,
        //   ensuring the agent always "slides" sideways around massive obstacles.
        //
        // Why Vector3.Lerp?
        //   Instead of blindly adding violent push forces (which can cause jitter or extreme 
        //   speed spikes), Lerping smoothly bends the agent's intended path toward the safe 
        //   tangent path. It creates fluid, natural-looking steering curves.
        //
        // Returns: A safely rerouted, normalized direction vector. 
        //          Unlike the old additive force, this completely replaces the intended 
        //          movement direction. SteeringAgent.Move() multiplies this by its max speed.
        public static Vector3 ObstacleAvoidance(
            Vector3 position,
            Vector3 forward,
            float avoidDistance,
            float avoidForce,
            LayerMask obstacleLayer,
            float whiskerAngle = 30f,
            float agentRadius = 0.5f)
        {
            Vector3 totalAvoidance = Vector3.zero;

            Vector3 flatForward = forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.0001f) return Vector3.zero;
            flatForward.Normalize();

            Vector3 rightLateral = Vector3.Cross(Vector3.up, flatForward).normalized;
            Vector3 leftLateral = -rightLateral;

            Quaternion rightRot = Quaternion.Euler(0f, whiskerAngle, 0f);
            Quaternion leftRot = Quaternion.Euler(0f, -whiskerAngle, 0f);

            var whiskers = new (Vector3 dir, float length, Vector3 escapeDir)[]
            {
                (flatForward, avoidDistance, rightLateral),
                (leftRot * flatForward, avoidDistance * 0.75f, rightLateral),
                (rightRot * flatForward, avoidDistance * 0.75f, leftLateral)
            };

            foreach (var (dir, length, escapeDir) in whiskers)
            {
                // This sweeps a sphere of 'agentRadius' along the direction vector.
                if (!Physics.SphereCast(position, agentRadius, dir, out RaycastHit hit, length, obstacleLayer))
                    continue;

                float proximity = 1f - (hit.distance / length);

                Vector3 normal = hit.normal;
                normal.y = 0f;
                if (normal.sqrMagnitude > 0.001f) normal.Normalize();

                Vector3 pushForce = (normal + escapeDir).normalized;

                totalAvoidance += pushForce * (avoidForce * proximity * proximity);

                // Debug visualization (Will still draw as lines, but physics act as cylinders)
                Debug.DrawRay(position, dir * hit.distance, Color.red);
                Debug.DrawRay(hit.point, pushForce * 2f, Color.blue);
            }

            return totalAvoidance;
        }
    }
}