using System.Collections.Generic;
using Core;
using Foundation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace World
{
    /// <summary>
    /// Group A: The Aggressor
    ///
    /// Behavioral Profile:
    ///   - Patrols diligently using ping-pong waypoints.
    ///   - Detects the player via ConeLOS and immediately pursues.
    ///   - When line of sight is LOST, switches to A* pathfinding to close the gap.
    ///   - When line of sight is REGAINED, switches back to Pursuit steering.
    ///   - Never flees; always closes distance.
    ///   - After n patrol cycles, enters Idle to "rest."
    ///
    /// FSM States: Patrol &lt;-&gt; Idle &lt;-&gt; Attack &lt;-&gt; PathfindingChase
    ///
    /// Roulette Wheel (on idle exit - determines post-rest patrol variant):
    ///   "Relentless" weight 0.5 (base): patrol at full speed
    ///   "Cautious" weight 0.3 (base): patrol at 80% speed
    ///   "Enraged" weight 0.2 (dynamic): patrol at 130% speed + alert event
    ///
    /// Dynamic Weight - "Enraged":
    ///   Each time the Aggressor exits AttackState WITHOUT landing a hit
    ///   (player escaped), _enragedWeight grows by +EnragedWeightIncrement,
    ///   capped at EnragedWeightMax. This makes the Aggressor progressively
    ///   more frantic the more times the player slips away.
    ///   The weight resets to its base value after it successfully lands a hit.
    /// </summary>
    [AddComponentMenu("AI/Enemies/Aggressor Enemy")]
    public class AggressorEnemy : AIAgent
    {
        [Header("Aggressor Configuration")] [SerializeField]
        private PatrolRoute _patrolRoute;

        [SerializeField] private float _patrolSpeed = 3f;
        [SerializeField] private float _attackSpeed = 6f;
        [SerializeField] private float _criticalAttackRange = 1.2f;
        [SerializeField] private int _patrolCyclesBeforeIdle = 3;
        [SerializeField] private float _idleDuration = 3f;
        [SerializeField] private AIEventChannel _eventChannel;

        [Header("Pathfinding")] [Tooltip("Assign the PathNodeGrid present in the scene.")] [SerializeField]
        private PathNodeGrid _pathNodeGrid;

        [Header("Dynamic Roulette")] [SerializeField]
        private float _enragedWeightBase = 0.2f;

        [SerializeField] private float _enragedWeightIncrement = 0.05f;
        [SerializeField] private float _enragedWeightMax = 0.6f;

        // Typed FSM for this group
        private StateMachine<AggressorStateKey> _fsm;
        private PatrolState<AggressorStateKey> _patrolState;
        private EnemyIdleState<AggressorStateKey> _idleState;

        // Tracks whether the current AttackState entry landed a hit
        private bool _attackLandedThisEntry;

        // ── Roulette Wheel - Enraged weight is dynamic ───────────────────────
        private float _enragedWeight;

        private List<(string outcome, float weight)> BuildPatrolVariants() =>
            new()
            {
                ("Relentless", 0.5f),
                ("Cautious", 0.3f),
                ("Enraged", _enragedWeight) // dynamic - read at selection time
            };

        // ── FSM Setup ────────────────────────────────────────────────────────

        protected override void SetupFSM()
        {
            _enragedWeight = _enragedWeightBase;

            _fsm = new StateMachine<AggressorStateKey>();

            _patrolState = new PatrolState<AggressorStateKey>(
                AggressorStateKey.Patrol, _steeringAgent, _patrolRoute, _patrolSpeed);

            _idleState = new EnemyIdleState<AggressorStateKey>(
                AggressorStateKey.Idle, _steeringAgent, _idleDuration, _eventChannel);

            _steeringAgent.SetMaxSpeed(_attackSpeed);

            var attackState = new AttackState<AggressorStateKey>(
                AggressorStateKey.Attack, _steeringAgent, _playerTransform,
                _criticalAttackRange, HandleAttackLanded, _eventChannel);

            // ── Pathfinding Chase State ───────────────────────────────────────
            // Entered when the player is NOT visible - A* closes the gap blindly.
            var pathfindingChaseState = new PathfindingState<AggressorStateKey>(
                AggressorStateKey.PathfindingChase,
                _steeringAgent,
                _playerTransform,
                _pathNodeGrid);

            // When idle timer expires: apply roulette variant, reset counter,
            // return to patrol.
            _idleState.OnIdleComplete += () =>
            {
                ApplyRoulettePatrolVariant();
                _patrolState.ResetCycleCount();
                _fsm.TransitionTo(AggressorStateKey.Patrol);
            };

            // Track whether attack landed for dynamic roulette weight update
            // We hook into the FSM transition via a wrapper:
            // OnExit of AttackState fires before the next state's OnEnter.
            // We detect "exit without landing" by checking _attackLandedThisEntry.
            // The flag is set to true inside HandleAttackLanded.
            // Reset it to false when we enter AttackState.
            attackState.OnEnterCallback += () => _attackLandedThisEntry = false;
            attackState.OnExitCallback += () =>
            {
                if (!_attackLandedThisEntry)
                    GrowEnragedWeight(); // Player escaped - Aggressor gets angrier
            };

            _fsm.AddState(_patrolState);
            _fsm.AddState(_idleState);
            _fsm.AddState(attackState);
            _fsm.AddState(pathfindingChaseState);
            _fsm.Start(AggressorStateKey.Patrol);

            _fsmRunner = new FSMRunner<AggressorStateKey>(_fsm);
        }

        // ── Decision Tree ────────────────────────────────────────────────────

        protected override IDecisionNode BuildDecisionTree()
        {
            // ── Leaf Actions ─────────────────────────────────────────────────
            var attackAction = new ActionNode(
                () =>
                {
                    if (!_fsm.IsInState(AggressorStateKey.Attack))
                        _fsm.TransitionTo(AggressorStateKey.Attack);
                },
                "GoAttack");

            var pathfindChaseAction = new ActionNode(
                () =>
                {
                    if (!_fsm.IsInState(AggressorStateKey.PathfindingChase))
                        _fsm.TransitionTo(AggressorStateKey.PathfindingChase);
                },
                "GoPathfindingChase");

            var idleAction = new ActionNode(
                () =>
                {
                    if (!_fsm.IsInState(AggressorStateKey.Idle))
                        _fsm.TransitionTo(AggressorStateKey.Idle);
                },
                "GoIdle");

            var patrolAction = new ActionNode(
                () =>
                {
                    if (!_fsm.IsInState(AggressorStateKey.Patrol))
                        _fsm.TransitionTo(AggressorStateKey.Patrol);
                },
                "GoPatrol");

            // ── Inner Branch: Should we rest or chase blindly? ────────────────
            // No LOS: if cycle threshold reached -> Idle, else -> PathfindingChase
            var shouldIdleOrPathfind = new QuestionNode(
                condition: () => _fsm.IsInState(AggressorStateKey.Idle) ||
                                 _patrolState.PatrolCycleCount >= _patrolCyclesBeforeIdle,
                trueNode: idleAction,
                falseNode: pathfindChaseAction);

            // ── Root Branch: Is the player visible? ──────────────────────────
            // LOS -> Attack (Pursuit steering), no LOS -> idle-or-pathfind branch
            return new QuestionNode(
                condition: () => _los.CanSee(_playerTransform),
                trueNode: attackAction,
                falseNode: shouldIdleOrPathfind);
        }

        // ── Dynamic Roulette Weight ───────────────────────────────────────────

        private void GrowEnragedWeight()
        {
            _enragedWeight = Mathf.Min(_enragedWeight + _enragedWeightIncrement, _enragedWeightMax);
            _eventChannel?.RaiseStateChanged($"AggressorEnragedWeight:{_enragedWeight:F2}");
        }

        private void ResetEnragedWeight()
        {
            _enragedWeight = _enragedWeightBase;
        }

        // ── Roulette Wheel Application ────────────────────────────────────────

        private void ApplyRoulettePatrolVariant()
        {
            // Rebuild list each time so the dynamic weight is current
            var variants = BuildPatrolVariants();
            string variant = RouletteWheelSelector.Select(variants);

            switch (variant)
            {
                case "Relentless":
                    _patrolState.SetPatrolSpeed(_patrolSpeed);
                    break;

                case "Cautious":
                    _patrolState.SetPatrolSpeed(_patrolSpeed * 0.8f);
                    break;

                case "Enraged":
                    _patrolState.SetPatrolSpeed(_patrolSpeed * 1.3f);
                    _eventChannel?.RaiseStateChanged("AggressorEnraged");
                    break;
            }
        }

        // ── Game-End Handler ─────────────────────────────────────────────────

        private void HandleAttackLanded()
        {
            _attackLandedThisEntry = true;
            ResetEnragedWeight(); // Caught the player - anger satisfied

            Debug.Log($"[AggressorEnemy] '{name}' caught the player! Game Over.");
            _eventChannel?.RaiseAttackLanded(_playerTransform.position);

            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}