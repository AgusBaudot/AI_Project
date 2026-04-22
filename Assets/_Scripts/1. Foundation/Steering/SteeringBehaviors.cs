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
    ///   uses 3D physics on a flat top-down surface with billboard sprites.
    ///
    /// Composition model:
    ///   Callers combine multiple desired velocities before applying them.
    ///   ObstacleAvoidance is ALWAYS composited inside SteeringAgent.Move(),
    ///   so it never needs to be called manually — it is architecturally guaranteed.
    /// </summary>
    public static class SteeringBehaviors
    {
        public static Vector3 Seek(Vector3 position, Vector3 target, float maxSpeed)
        {
            Vector3 toTarget = target - position;
            toTarget.y = 0f;

            // sqrMagnitude threshold avoids a sqrt and prevents normalization
            // of a near-zero vector (which would produce NaN or Infinity)
            if (toTarget.sqrMagnitude < 0.0001f) return Vector3.zero;

            return toTarget.normalized * maxSpeed;
        }

        public static Vector3 Flee(Vector3 position, Vector3 threat, float maxSpeed)
        {
            Vector3 away = position - threat;
            away.y = 0f;

            if (away.sqrMagnitude < 0.0001f) return Vector3.zero;

            return away.normalized * maxSpeed;
        }

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

        public static Vector3 Pursuit(
            Vector3 position, float agentMaxSpeed,
            Vector3 targetPosition, Vector3 targetVelocity)
        {
            Vector3 predicted = Predictor.PredictPosition(
                targetPosition, targetVelocity, position, agentMaxSpeed);

            return Seek(position, predicted, agentMaxSpeed);
        }

        public static Vector3 Evasion(
            Vector3 position, float agentMaxSpeed,
            Vector3 pursuerPosition, Vector3 pursuerVelocity)
        {
            Vector3 predictedThreat = Predictor.PredictPosition(
                pursuerPosition, pursuerVelocity, position, agentMaxSpeed);

            return Flee(position, predictedThreat, agentMaxSpeed);
        }

        public static Vector3 ObstacleAvoidance(
            Vector3 position,
            Vector3 forward,
            float avoidDistance,
            float avoidForce,
            LayerMask obstacleLayer,
            float whiskerAngle = 30f)
        {
            Vector3 totalAvoidance = Vector3.zero;

            // Project forward onto XZ plane — we don't want to steer up/down
            Vector3 flatForward = forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.0001f) return Vector3.zero;
            flatForward.Normalize();

            // Define three whisker directions and their effective lengths
            Quaternion leftRot = Quaternion.Euler(0f, whiskerAngle, 0f);
            Quaternion rightRot = Quaternion.Euler(0f, -whiskerAngle, 0f);

            var whiskers = new (Vector3 dir, float length)[]
            {
                (flatForward, avoidDistance), // Center — longest, most urgent
                (leftRot * flatForward, avoidDistance * 0.75f), // Left diagonal
                (rightRot * flatForward, avoidDistance * 0.75f) // Right diagonal
            };

            foreach (var (dir, length) in whiskers)
            {
                if (!Physics.Raycast(position, dir, out RaycastHit hit, length, obstacleLayer))
                    continue;

                // Normalized proximity [0 = at max distance, 1 = at contact surface]
                float proximity = 1f - (hit.distance / length);

                // Surface normal in XZ plane (ignore Y component of normal for flat world)
                Vector3 normal = hit.normal;
                normal.y = 0f;

                // Proximity² creates a sharper, more urgent avoidance near surfaces
                totalAvoidance += normal * (avoidForce * proximity * proximity);

                // Debug visualization — disable in release builds
                Debug.DrawRay(position, dir * hit.distance, Color.red);
            }

            return totalAvoidance;
        }
    }
}