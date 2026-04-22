using Foundation;
using UnityEngine;

namespace Core
{
    // ── FSM Runner bridge ────────────────────────────────────────────────────
    // AIAgent is non-generic, but it needs to Tick a generic StateMachine<T>.
    // IFSMRunner erases the generic parameter so the base class remains clean.
    // Concrete subclasses create a FSMRunner<TKey> and assign it to _fsmRunner.

    public interface IFSMRunner
    {
        void Tick(float deltaTime);
    }

    public sealed class FSMRunner<TKey> : IFSMRunner where TKey : struct, System.IEquatable<TKey>
    {
        private readonly StateMachine<TKey> _fsm;
        public FSMRunner(StateMachine<TKey> fsm) => _fsm = fsm;
        public void Tick(float deltaTime) => _fsm.Tick(deltaTime);
    }

    [RequireComponent(typeof(SteeringAgent))]
    [RequireComponent(typeof(ConeLOS))]
    public abstract class AIAgent : MonoBehaviour
    {
        [Header("Shared References")] [SerializeField]
        protected Transform _playerTransform;

        // Composed components — resolved in Awake, shared with states via constructor injection
        protected SteeringAgent _steeringAgent;
        protected ConeLOS _los;

        // Populated by subclasses in SetupFSM()
        protected IFSMRunner _fsmRunner;

        // Populated by subclasses in BuildDecisionTree()
        protected IDecisionNode _decisionTreeRoot;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        protected virtual void Awake()
        {
            _steeringAgent = GetComponent<SteeringAgent>();
            _los = GetComponent<ConeLOS>();
        }

        protected virtual void Start()
        {
            SetupFSM();
            _decisionTreeRoot = BuildDecisionTree();
        }

        protected virtual void Update()
        {
            // 1. Decision tree first: may trigger state transitions
            _decisionTreeRoot?.MakeDecision();

            // 2. FSM tick: executes the (potentially new) current state
            _fsmRunner?.Tick(Time.deltaTime);
        }

        // ── Abstract Interface ───────────────────────────────────────────────

        /// <summary>
        /// Subclasses register states and start the FSM here.
        /// </summary>
        protected abstract void SetupFSM();

        /// <summary>
        /// Subclasses construct and return the decision tree root here.
        /// </summary>
        protected abstract IDecisionNode BuildDecisionTree();
    }
}