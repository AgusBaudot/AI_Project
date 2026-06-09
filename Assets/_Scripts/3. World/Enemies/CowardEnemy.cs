using System.Collections.Generic;
using Core;
using Foundation;
using UnityEngine;

namespace World
{
    /// <summary>
    /// Group B: The Coward
    ///
    /// Behavioral Profile:
    ///   - Patrols slowly and nervously using ping-pong waypoints.
    ///   - Flees immediately on player detection using Evasion steering.
    ///   - When player is NOT visible but still too close, flees via A* pathfinding
    ///     to the node farthest from the player.
    ///   - When safe distance is reached and player not visible, resumes patrol.
    ///   - After n patrol cycles, rests briefly before resuming.
    ///
    /// FSM States: Patrol &lt;-&gt; Idle &lt;-&gt; RunAway &lt;-&gt; PathfindingFlee
    ///
    /// Roulette Wheel (on idle exit - determines post-rest speed tier):
    ///   "SlowShuffle" weight 0.5 -> 50%: very slow (timid after fright)
    ///   "Normal"      weight 0.3 -> 30%: standard speed
    ///   "Skittish"    weight 0.2 -> 20%: fast, erratic patrol speed
    /// </summary>
    [AddComponentMenu("AI/Enemies/Coward Enemy")]
    public class CowardEnemy : AIAgent
    {
        [Header("Coward Configuration")] [SerializeField]
        private PatrolRoute _patrolRoute;

        [SerializeField] private float _patrolSpeed = 2f;
        [SerializeField] private float _runAwaySpeed = 7f;
        [SerializeField] private float _evasionBlend = 0.8f;
        [SerializeField] private float _safeEscapeDistance = 15f;
        [SerializeField] private int _patrolCyclesBeforeIdle = 2;
        [SerializeField] private float _idleDuration = 2f;
        [SerializeField] private AIEventChannel _eventChannel;

        [Header("Pathfinding")] [Tooltip("Assign the PathNodeGrid present in the scene.")] [SerializeField]
        private PathNodeGrid _pathNodeGrid;

        private StateMachine<CowardStateKey> _fsm;
        private PatrolState<CowardStateKey> _patrolState;
        private EnemyIdleState<CowardStateKey> _idleState;

        // ── Roulette Wheel ────────────────────────────────────────────────────
        private static readonly List<(string outcome, float weight)> _speedTiers
            = new()
            {
                ("SlowShuffle", 0.5f),
                ("Normal", 0.3f),
                ("Skittish", 0.2f)
            };

        // ── FSM Setup ────────────────────────────────────────────────────────

        protected override void SetupFSM()
        {
            _fsm = new StateMachine<CowardStateKey>();

            _patrolState = new PatrolState<CowardStateKey>(
                CowardStateKey.Patrol, _steeringAgent, _patrolRoute, _patrolSpeed);

            _idleState = new EnemyIdleState<CowardStateKey>(
                CowardStateKey.Idle, _steeringAgent, _idleDuration, _eventChannel);

            _steeringAgent.SetMaxSpeed(_runAwaySpeed);

            var runAwayState = new RunAwayState<CowardStateKey>(
                CowardStateKey.RunAway, _steeringAgent, _playerTransform,
                _evasionBlend, _eventChannel);

            // ── Pathfinding Flee State ────────────────────────────────────────
            // Entered when player is NOT visible but coward is still too close.
            // A* finds the node farthest from the player and routes there.
            var pathfindingFleeState = new PathfindingState<CowardStateKey>(
                CowardStateKey.PathfindingFlee,
                _steeringAgent,
                _playerTransform,
                _pathNodeGrid,
                PathfindingMode.Flee);

            _idleState.OnIdleComplete += () =>
            {
                ApplyRouletteSpeedTier();
                _patrolState.ResetCycleCount();
                _fsm.TransitionTo(CowardStateKey.Patrol);
            };

            _fsm.AddState(_patrolState);
            _fsm.AddState(_idleState);
            _fsm.AddState(runAwayState);
            _fsm.AddState(pathfindingFleeState);
            _fsm.Start(CowardStateKey.Patrol);

            _fsmRunner = new FSMRunner<CowardStateKey>(_fsm);
        }

        // ── Decision Tree ────────────────────────────────────────────────────

        protected override IDecisionNode BuildDecisionTree()
        {
            var runAwayAction = new ActionNode(
                () =>
                {
                    if (!_fsm.IsInState(CowardStateKey.RunAway))
                        _fsm.TransitionTo(CowardStateKey.RunAway);
                },
                "GoRunAway");

            var pathfindFleeAction = new ActionNode(
                () =>
                {
                    if (!_fsm.IsInState(CowardStateKey.PathfindingFlee))
                        _fsm.TransitionTo(CowardStateKey.PathfindingFlee);
                },
                "GoPathfindingFlee");

            var idleAction = new ActionNode(
                () =>
                {
                    if (!_fsm.IsInState(CowardStateKey.Idle))
                        _fsm.TransitionTo(CowardStateKey.Idle);
                },
                "GoIdle");

            var patrolAction = new ActionNode(
                () =>
                {
                    if (!_fsm.IsInState(CowardStateKey.Patrol))
                        _fsm.TransitionTo(CowardStateKey.Patrol);
                },
                "GoPatrol");

            // ── Innermost branch: already fleeing via pathfinding? ────────────
            // If we had no LOS and entered PathfindingFlee, we stay in it until
            // safe distance is reached - prevents flip-flopping to Patrol too soon.
            var continueFleeOrPatrol = new QuestionNode(
                condition: () => _fsm.IsInState(CowardStateKey.PathfindingFlee) &&
                                 (transform.position - _playerTransform.position).sqrMagnitude
                                 < _safeEscapeDistance * _safeEscapeDistance,
                trueNode: pathfindFleeAction,
                falseNode: patrolAction);

            // ── Middle branch: should we rest or keep fleeing? ────────────────
            var shouldIdleOrFlee = new QuestionNode(
                condition: () => _fsm.IsInState(CowardStateKey.Idle) ||
                                 _patrolState.PatrolCycleCount >= _patrolCyclesBeforeIdle,
                trueNode: idleAction,
                falseNode: continueFleeOrPatrol);

            // ── Root: immediate threat? ───────────────────────────────────────
            // Visible player -> Evasion steering (RunAway).
            // Not visible but still too close in RunAway -> also keep RunAway.
            return new QuestionNode(
                condition: () =>
                {
                    if (_los.CanSee(_playerTransform)) return true;

                    if (_fsm.IsInState(CowardStateKey.RunAway))
                    {
                        float distSqr = (transform.position - _playerTransform.position).sqrMagnitude;
                        if (distSqr < _safeEscapeDistance * _safeEscapeDistance)
                            return true;
                    }

                    return false;
                },
                trueNode: runAwayAction,
                falseNode: shouldIdleOrFlee);
        }

        // ── Roulette Wheel Application ────────────────────────────────────────

        private void ApplyRouletteSpeedTier()
        {
            string tier = RouletteWheelSelector.Select(_speedTiers);

            float chosenSpeed = tier switch
            {
                "SlowShuffle" => _patrolSpeed * 0.5f,
                "Normal" => _patrolSpeed,
                "Skittish" => _patrolSpeed * 1.8f,
                _ => _patrolSpeed
            };

            _patrolState.SetPatrolSpeed(chosenSpeed);
            _eventChannel?.RaiseStateChanged($"CowardSpeed:{tier}");
        }
    }
}