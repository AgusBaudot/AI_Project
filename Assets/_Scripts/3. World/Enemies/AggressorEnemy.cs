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
    ///   - Patrols diligently using loop waypoints.
    ///   - Detects the player via ConeLOS and immediately pursues (Pursuit steering).
    ///   - If the player was being chased and line of sight is LOST, switches to
    ///     A* pathfinding to close the gap.
    ///   - When line of sight is REGAINED during pathfinding, switches back to Attack.
    ///   - After n patrol cycles, enters Idle to "rest."
    ///
    /// FSM States: Patrol <-> Idle <-> Attack <-> PathfindingChase
    ///
    /// Decision Tree:
    ///   Root -> IsPlayerVisible?
    ///     YES -> TransitionTo(Attack)
    ///     NO  -> WasChasing (Attack or PathfindingChase)?
    ///           YES -> TransitionTo(PathfindingChase)   <- lost the player mid-chase
    ///           NO  -> CycleThresholdReached?
    ///                 YES -> TransitionTo(Idle)
    ///                 NO  -> TransitionTo(Patrol)       <- normal patrol
    ///
    /// The key fix: PathfindingChase is only entered when the enemy was already
    /// chasing. During normal patrol the no-LOS branch goes straight to Patrol,
    /// not to pathfinding.
    /// </summary>
    [AddComponentMenu("AI/Enemies/Aggressor Enemy")]
    public class AggressorEnemy : AIAgent
    {
        [Header("Aggressor Configuration")]
        [SerializeField] private PatrolRoute _patrolRoute;
        [SerializeField] private float _patrolSpeed = 3f;
        [SerializeField] private float _attackSpeed = 6f;
        [SerializeField] private float _criticalAttackRange = 1.2f;
        [SerializeField] private int _patrolCyclesBeforeIdle = 3;
        [SerializeField] private float _idleDuration = 3f;
        [SerializeField] private float _investigateDuration = 4f;
        [SerializeField] private AIEventChannel _eventChannel;

        [Header("Pathfinding")]
        [Tooltip("Assign the PathNodeGrid present in the scene.")]
        [SerializeField] private PathNodeGrid _pathNodeGrid;

        [Header("Dynamic Roulette")]
        [SerializeField] private float _enragedWeightBase      = 0.2f;
        [SerializeField] private float _enragedWeightIncrement = 0.05f;
        [SerializeField] private float _enragedWeightMax       = 0.6f;

        private StateMachine<AggressorStateKey> _fsm;
        private PatrolState<AggressorStateKey>  _patrolState;
        private EnemyIdleState<AggressorStateKey> _idleState;
        private PathfindingState<AggressorStateKey> _pathfindingChaseState;
        private InvestigateState<AggressorStateKey> _investigateState;
        private Vector3 _lastKnownPlayerPosition;
        private bool _attackLandedThisEntry;
        private float _enragedWeight;

        // ── Roulette Wheel ────────────────────────────────────────────────────

        private List<(string outcome, float weight)> BuildPatrolVariants() =>
            new List<(string, float)>
            {
                ("Relentless", 0.5f),
                ("Cautious",   0.3f),
                ("Enraged",    _enragedWeight)
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

            _pathfindingChaseState = new PathfindingState<AggressorStateKey>(
                AggressorStateKey.PathfindingChase,
                _steeringAgent,
                () => _lastKnownPlayerPosition,
                _pathNodeGrid,
                PathfindingMode.Chase);

            _investigateState = new InvestigateState<AggressorStateKey>(
                AggressorStateKey.Investigate, _steeringAgent, _investigateDuration, events: _eventChannel);

            _idleState.OnIdleComplete += () =>
            {
                ApplyRoulettePatrolVariant();
                _patrolState.ResetCycleCount();
                _fsm.TransitionTo(AggressorStateKey.Patrol);
            };

            attackState.OnEnterCallback += () => _attackLandedThisEntry = false;
            attackState.OnExitCallback  += () =>
            {
                if (!_attackLandedThisEntry)
                    GrowEnragedWeight();
            };

            _investigateState.OnInvestigateComplete += () =>
            {
                _fsm.TransitionTo(AggressorStateKey.Patrol);
            };

            _fsm.AddState(_patrolState);
            _fsm.AddState(_idleState);
            _fsm.AddState(attackState);
            _fsm.AddState(_pathfindingChaseState);
            _fsm.AddState(_investigateState);
            _fsm.Start(AggressorStateKey.Patrol);

            _fsmRunner = new FSMRunner<AggressorStateKey>(_fsm);
        }

        // ── Decision Tree ────────────────────────────────────────────────────

        protected override IDecisionNode BuildDecisionTree()
        {
            var attackAction = new ActionNode(
                () => { if (!_fsm.IsInState(AggressorStateKey.Attack))
                            _fsm.TransitionTo(AggressorStateKey.Attack); },
                "GoAttack");

            var pathfindChaseAction = new ActionNode(
                () => { if (!_fsm.IsInState(AggressorStateKey.PathfindingChase))
                            _fsm.TransitionTo(AggressorStateKey.PathfindingChase); },
                "GoPathfindingChase");

            var idleAction = new ActionNode(
                () => { if (!_fsm.IsInState(AggressorStateKey.Idle))
                            _fsm.TransitionTo(AggressorStateKey.Idle); },
                "GoIdle");

            var patrolAction = new ActionNode(
                () => { if (!_fsm.IsInState(AggressorStateKey.Patrol))
                            _fsm.TransitionTo(AggressorStateKey.Patrol); },
                "GoPatrol");
            
            var investigateAction = new ActionNode(
                () => { if (!_fsm.IsInState(AggressorStateKey.Investigate))
                            _fsm.TransitionTo(AggressorStateKey.Investigate); },
                "GoInvestigate");

            // ── Innermost: normal patrol or rest? ─────────────────────────────
            var shouldIdleOrPatrol = new QuestionNode(
                condition: () => _fsm.IsInState(AggressorStateKey.Idle) ||
                                 _patrolState.PatrolCycleCount >= _patrolCyclesBeforeIdle,
                trueNode:  idleAction,
                falseNode: patrolAction);
            
            // ── Middle: Should we stop and look around? ───────────────────────
            var shouldInvestigate = new QuestionNode(
                condition: () => _fsm.IsInState(AggressorStateKey.Investigate) ||
                                 (_fsm.IsInState(AggressorStateKey.PathfindingChase) && _pathfindingChaseState.IsPathComplete),
                trueNode: investigateAction,
                falseNode: shouldIdleOrPatrol);

            // ── Middle: was chasing and lost player? → keep pursuing via A* ───
            // Only enter PathfindingChase if we were already in a chase state.
            // During normal patrol, no-LOS drops straight through to patrol/idle.
            var wasChasing = new QuestionNode(
                condition: () => 
                {
                    if (_fsm.IsInState(AggressorStateKey.Attack))
                    {
                        _lastKnownPlayerPosition = _playerTransform.position;
                        return true;
                    }
        
                    return _fsm.IsInState(AggressorStateKey.PathfindingChase) && !_pathfindingChaseState.IsPathComplete;
                },
                trueNode:  pathfindChaseAction,
                falseNode: shouldInvestigate);

            // ── Root: can we see the player? ──────────────────────────────────
            return new QuestionNode(
                condition: () => _los.CanSee(_playerTransform),
                trueNode:  attackAction,
                falseNode: wasChasing);
        }

        // ── Dynamic Roulette Weight ───────────────────────────────────────────

        private void GrowEnragedWeight()
        {
            _enragedWeight = Mathf.Min(_enragedWeight + _enragedWeightIncrement, _enragedWeightMax);
            _eventChannel?.RaiseStateChanged($"AggressorEnragedWeight:{_enragedWeight:F2}");
        }

        private void ResetEnragedWeight() => _enragedWeight = _enragedWeightBase;

        // ── Roulette Wheel Application ────────────────────────────────────────

        private void ApplyRoulettePatrolVariant()
        {
            string variant = RouletteWheelSelector.Select(BuildPatrolVariants());

            switch (variant)
            {
                case "Relentless": _patrolState.SetPatrolSpeed(_patrolSpeed);         break;
                case "Cautious":   _patrolState.SetPatrolSpeed(_patrolSpeed * 0.8f);  break;
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
            ResetEnragedWeight();
            Debug.Log($"[AggressorEnemy] '{name}' caught the player! Game Over.");
            _eventChannel?.RaiseAttackLanded(_playerTransform.position);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}