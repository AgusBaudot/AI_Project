using UnityEngine;

namespace Foundation
{
    /// <summary>
    /// Cone-shaped Line of Sight sensor.
    ///
    /// Detection Pipeline — ordered cheapest -> most expensive:
    ///
    ///   PASS 1: Distance check using sqrMagnitude
    ///     If the target is farther than _detectionRange, reject immediately.
    ///     Using sqrMagnitude avoids the sqrt in Vector3.Distance, which is
    ///     significant when called on 5+ enemies per frame.
    ///
    ///   PASS 2: Field of View angle using Dot Product
    ///     dot(normalize(forward), normalize(toTarget)) = cos(0) //Not 0, theta.
    ///     We precompute cos(halfFOV) once in Awake/OnValidate and compare
    ///     directly. This avoids Mathf.Acos() (inverse trig is expensive)
    ///     and instead leverages the monotonic relationship:
    ///       dot >= cos(halfFOV) -> 0 <= halfFOV -> target inside cone
    ///     NOTE: This comparison only works on normalized vectors.
    ///
    ///   PASS 3: Physics.Linecast for occlusion 
    ///     Only called when the target has already passed the geometric tests.
    ///     Casting from sensor origin to target position; any hit on the
    ///     occlusion layer means the line of sight is broken by geometry.
    ///
    /// Precomputed values (_halfFOVCos, _rangeSquared):
    ///   Refreshed in Awake and OnValidate (for live Inspector tweaking).
    ///   This ensures the math is always in sync with the serialized parameters.
    /// </summary>
    [AddComponentMenu("AI/Sensors/Cone LOS")]
    public class ConeLOS : MonoBehaviour
    {
        [Header("Detection Parameters")]
        [Tooltip("Maximum detection range in world units.")]
        [SerializeField] private float _detectionRange = 10f;

        [Tooltip("Full field of view angle in degrees. 90° means +-45° from forward.")]
        [SerializeField] private float _fieldOfViewDegrees = 90f;

        [Tooltip("Physics layers that occlude line of sight.")]
        [SerializeField] private LayerMask _occlusionMask;

        // Precomputed per-frame constants
        private float _halfFOVCos;   // cos(halfAngle) — used in dot product comparison
        private float _rangeSquared; // _detectionRange^2 — avoids sqrt in distance check

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Awake() => RefreshPrecomputed();
        private void OnValidate() => RefreshPrecomputed(); // Keep in sync during Inspector editing

        private void RefreshPrecomputed()
        {
            _halfFOVCos   = Mathf.Cos(_fieldOfViewDegrees * 0.5f * Mathf.Deg2Rad);
            _rangeSquared = _detectionRange * _detectionRange;
        }

        // ── Public Detection API ─────────────────────────────────────────────

        public bool CanSee(Transform target) => CanSee(target.position);
        
        /// <summary>
        /// Overload accepting a world position directly.
        /// Useful for testing and for checking predicted positions.
        /// </summary>
        public bool CanSee(Vector3 targetPosition)
        {
            Vector3 origin   = transform.position;
            Vector3 toTarget = targetPosition - origin;
            toTarget.y = 0f; // Top-down: ignore Y delta when comparing angles

            // ── PASS 1: Distance ────────────────────────────────────────────
            if (toTarget.sqrMagnitude > _rangeSquared) 
                return false;

            // ── PASS 2: Field of View (Dot Product) ─────────────────────────
            Vector3 flatForward = transform.forward;
            flatForward.y = 0f;

            if (flatForward.sqrMagnitude < 0.0001f) 
                return false;

            // Both vectors must be normalized for dot = cos(0) to hold
            float dot = Vector3.Dot(flatForward.normalized, toTarget.normalized);
            if (dot < _halfFOVCos) 
                return false;

            // ── PASS 3: Occlusion Linecast ───────────────────────────────────
            if (Physics.Linecast(origin, targetPosition, _occlusionMask))
                return false;

            return true;
        }
        
        // ── Scene Gizmos ────────────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            Vector3 pos = transform.position;
            float halfAngle = _fieldOfViewDegrees * 0.5f;

            // Range sphere
            Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
            Gizmos.DrawSphere(pos, _detectionRange);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(pos, _detectionRange);

            // FOV cone edges
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) return;

            Gizmos.color = Color.cyan;
            Vector3 left = Quaternion.Euler(0f, -halfAngle, 0f) * fwd;
            Vector3 right = Quaternion.Euler(0f,  halfAngle, 0f) * fwd;
            Gizmos.DrawRay(pos, left.normalized  * _detectionRange);
            Gizmos.DrawRay(pos, right.normalized * _detectionRange);
        }
    }
}