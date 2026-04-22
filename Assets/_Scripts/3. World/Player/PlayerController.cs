using Core;
using Foundation;
using UnityEngine;

namespace World
{
    [RequireComponent(typeof(SteeringAgent))]
    [AddComponentMenu("AI/World/Player Controller")]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")] [SerializeField] private float _moveSpeed = 5f;

        private SteeringAgent _steeringAgent;
        private StateMachine<PlayerStateKey> _fsm;

        // Enemies read this for Pursuit / Evasion prediction
        public Vector3 Velocity => _steeringAgent.CurrentVelocity;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            _steeringAgent = GetComponent<SteeringAgent>();
            _steeringAgent.SetMaxSpeed(_moveSpeed);
        }

        private void Start()
        {
            _fsm = new StateMachine<PlayerStateKey>();

            _fsm.AddState(new PlayerIdleState(PlayerStateKey.Idle, _steeringAgent));
            _fsm.AddState(new PlayerWalkState(PlayerStateKey.Walk, this, _steeringAgent, _moveSpeed));

            _fsm.Start(PlayerStateKey.Idle);
        }

        private void Update()
        {
            // Transition logic lives here (not in states) to keep states stateless
            bool isMoving = GetMoveInput().sqrMagnitude > 0.01f;

            if (isMoving && _fsm.IsInState(PlayerStateKey.Idle))
                _fsm.TransitionTo(PlayerStateKey.Walk);
            else if (!isMoving && _fsm.IsInState(PlayerStateKey.Walk))
                _fsm.TransitionTo(PlayerStateKey.Idle);

            _fsm.Tick(Time.deltaTime);
        }

        // ── Input Helper ─────────────────────────────────────────────────────

        /// <summary>Returns a normalized XZ movement vector from arrow key / WASD input.</summary>
        public Vector3 GetMoveInput()
        {
            // GetAxisRaw: no inertia, immediate response — correct for top-down feel
            Vector3 dir = new Vector3(
                Input.GetAxisRaw("Horizontal"),
                0f,
                Input.GetAxisRaw("Vertical"));

            // Normalize prevents diagonal movement being √2× faster than cardinal
            return dir.normalized;
        }
    }
}