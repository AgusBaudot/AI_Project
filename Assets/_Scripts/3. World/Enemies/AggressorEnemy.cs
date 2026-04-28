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
    ///   - Never flees; always closes distance.
    ///   - After n patrol cycles, enters Idle to "rest."
    ///
    /// FSM: Patrol <-> Idle <-> Attack
    ///
    /// Decision Tree:
    ///   Root -> IsPlayerVisible?
    ///     YES -> [Action] TransitionTo(Attack)
    ///     NO  -> IsPatrolCycleThresholdReached AND not currently Idle?
    ///           YES -> [Action] TransitionTo(Idle)
    ///           NO  -> [Action] TransitionTo(Patrol)
    ///
    /// Roulette Wheel (on idle exit — determines post-rest patrol variant):
    ///   "Relentless"  weight 0.5 = 50%: patrol at full speed (standard)
    ///   "Cautious"    weight 0.3 = 30%: patrol at 80% speed (wary)
    ///   "Enraged"     weight 0.2 = 20%: patrol at 130% speed + alert event
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

        // Typed FSM for this group
        private StateMachine<AggressorStateKey> _fsm;
        private PatrolState<AggressorStateKey> _patrolState;
        private EnemyIdleState<AggressorStateKey> _idleState;

        // ── Roulette Wheel: Idle exit -> patrol variant ───────────────────────
        // "Relentless" 0.5, "Cautious" 0.3, "Enraged" 0.2 -> 50%/30%/20%
        private static readonly List<(string outcome, float weight)> _patrolVariants
            = new List<(string, float)>
            {
                ("Relentless", 0.5f),
                ("Cautious", 0.3f),
                ("Enraged", 0.2f)
            };

        // ── FSM Setup ────────────────────────────────────────────────────────

        protected override void SetupFSM()
        {
            _fsm = new StateMachine<AggressorStateKey>();

            _patrolState = new PatrolState<AggressorStateKey>(
                AggressorStateKey.Patrol, _steeringAgent, _patrolRoute, _patrolSpeed);

            _idleState = new EnemyIdleState<AggressorStateKey>(
                AggressorStateKey.Idle, _steeringAgent, _idleDuration, _eventChannel);

            _steeringAgent.SetMaxSpeed(_attackSpeed);

            var attackState = new AttackState<AggressorStateKey>(
                AggressorStateKey.Attack, _steeringAgent, _playerTransform,
                _criticalAttackRange, HandleAttackLanded, _eventChannel);

            // When the idle timer expires: apply roulette variant, reset counter, return to patrol
            _idleState.OnIdleComplete += () =>
            {
                ApplyRoulettePatrolVariant();
                _patrolState.ResetCycleCount();
                _fsm.TransitionTo(AggressorStateKey.Patrol);
            };

            _fsm.AddState(_patrolState);
            _fsm.AddState(_idleState);
            _fsm.AddState(attackState);
            _fsm.Start(AggressorStateKey.Patrol);

            // Wrap with FSMRunner so the non-generic AIAgent base can call Tick
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
                }, "GoAttack");

            var idleAction = new ActionNode(
                () =>
                {
                    if (!_fsm.IsInState(AggressorStateKey.Idle))
                        _fsm.TransitionTo(AggressorStateKey.Idle);
                }, "GoIdle");

            var patrolAction = new ActionNode(
                () =>
                {
                    if (!_fsm.IsInState(AggressorStateKey.Patrol))
                        _fsm.TransitionTo(AggressorStateKey.Patrol);
                }, "GoPatrol");

            // ── Inner Branch: Should we rest? ────────────────────────────────
            // "Not currently Idle" guard prevents the decision tree from re-entering
            // Idle every frame while already idling (which would reset the timer).
            var shouldIdleOrPatrol = new QuestionNode(
                condition: () => _fsm.IsInState(AggressorStateKey.Idle) ||
                                 _patrolState.PatrolCycleCount >= _patrolCyclesBeforeIdle,
                trueNode: idleAction,
                falseNode: patrolAction);

            // ── Root Branch: Is the player visible? ──────────────────────────
            return new QuestionNode(
                condition: () => _los.CanSee(_playerTransform),
                trueNode: attackAction,
                falseNode: shouldIdleOrPatrol);
        }

        // ── Roulette Wheel Application ────────────────────────────────────────

        private void ApplyRoulettePatrolVariant()
        {
            string variant = RouletteWheelSelector.Select(_patrolVariants);

            switch (variant)
            {
                case "Relentless":
                    // Standard speed — no change
                    _patrolState.SetPatrolSpeed(_patrolSpeed);
                    break;

                case "Cautious":
                    // Wary after resting — slower patrol
                    _patrolState.SetPatrolSpeed(_patrolSpeed * 0.8f);
                    break;

                case "Enraged":
                    // Alert — faster patrol + hook for audio/VFX
                    _patrolState.SetPatrolSpeed(_patrolSpeed * 1.3f);
                    _eventChannel?.RaiseStateChanged("AggressorEnraged"); // VFX/sound hook
                    break;
            }
        }

        // ── Game-End Handler ─────────────────────────────────────────────────

        private void HandleAttackLanded()
        {
            Debug.Log($"[AggressorEnemy] '{name}' caught the player! Game Over.");
            _eventChannel?.RaiseAttackLanded(_playerTransform.position);

            // Prototype: reload the scene.
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}