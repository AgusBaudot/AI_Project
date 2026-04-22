using UnityEngine;

namespace Foundation
{
    /// <summary>
    /// Isolated position-prediction utility for Pursuit and Evasion.
    /// Extracting prediction logic lets us swap the algorithm
    /// (e.g., replace first-order with a Kalman filter) without
    /// touching the behavior layer in SteeringBehaviors.cs.
    /// It is also independently unit-testable.
    /// </summary>
    public static class Predictor
    {
        /// <summary>
        /// Estimates where a moving target will be when the seeker arrives.
        ///
        /// Algorithm — First-Order Linear Extrapolation:
        ///   lookAheadTime = distance(seeker, target) / seekerSpeed
        ///   predictedPos  = targetPos + targetVelocity × lookAheadTime
        ///
        /// Since the FSM re-evaluates pursuit every frame, creating a closed
        /// feedback loop. Prediction errors from frame N are corrected in
        /// frame N+1, making higher-order terms computationally wasteful.
        /// First-order is a near-optimal cost/quality trade-off for game AI.
        ///
        /// maxLookAhead Clamp:
        ///   Without this, a slow seeker far from the target would predict a
        ///   position many seconds into the future — potentially off the level.
        ///   Clamping to 2 seconds keeps the predicted point reachable and
        ///   prevents degenerate pursuit paths across large open spaces.
        /// </summary>
        public static Vector3 PredictPosition(
            Vector3 targetPos,
            Vector3 targetVelocity,
            Vector3 seekerPos,
            float seekerSpeed,
            float maxLookAhead = 2f)
        {
            // Guard: a stationary seeker cannot intercept — return current target pos
            if (seekerSpeed < 0.001f) return targetPos;

            float distance = Vector3.Distance(seekerPos, targetPos);
            float lookAheadTime = Mathf.Clamp(distance / seekerSpeed, 0f, maxLookAhead);

            // Project the target forward along its current velocity
            return targetPos + targetVelocity * lookAheadTime;
        }
    }
}