using System.Collections.Generic;
using Core;
using Foundation;
using UnityEngine;

namespace World
{
    [AddComponentMenu("AI/Enemies/Coward Enemy")]
    public class CowardEnemy : AIAgent
    {
        /// <summary>
        /// Group B: The Coward
        ///
        /// Behavioral Profile:
        ///   - Patrols slowly and nervously using ping-pong waypoints.
        ///   - Flees immediately on player detection — never fights back.
        ///   - Uses a high Evasion blend (0.8) to predictively escape.
        ///   - After n patrol cycles, rests briefly before resuming.
        ///
        /// FSM: Patrol <-> Idle <-> RunAway
        ///
        /// Decision Tree:
        ///   Root -> IsPlayerVisible?
        ///     YES -> [Action] TransitionTo(RunAway)
        ///     NO  -> IsPatrolCycleThresholdReached AND not currently Idle?
        ///           YES -> [Action] TransitionTo(Idle)
        ///           NO  -> [Action] TransitionTo(Patrol)
        ///
        /// Roulette Wheel (on idle exit — determines post-rest speed tier):
        ///   "SlowShuffle" weight 0.5 -> 50%: very slow (timid after fright)
        ///   "Normal"      weight 0.3 -> 30%: standard speed
        ///   "Skittish"    weight 0.2 -> 20%: fast, erratic patrol speed
        /// </summary>
        [Header("Coward Configuration")] [SerializeField]
        private PatrolRoute _patrolRoute;

        [SerializeField] private float _patrolSpeed = 2f;
        [SerializeField] private float _runAwaySpeed = 7f; // Cowards are fast when scared
        [SerializeField] private float _evasionBlend = 0.8f; // Heavily evasion-dominant
        [SerializeField] private float _safeEscapeDistance = 15f;
        [SerializeField] private int _patrolCyclesBeforeIdle = 2; // Rests more frequently
        [SerializeField] private float _idleDuration = 2f;
        [SerializeField] private AIEventChannel _eventChannel;

        private StateMachine<CowardStateKey> _fsm;
        private PatrolState<CowardStateKey> _patrolState;
        private EnemyIdleState<CowardStateKey> _idleState;

        // ── Roulette Wheel: Idle exit -> patrol speed tier ────────────────────
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

            // RunAway speed is higher than patrol speed — cowards sprint when scared
            _steeringAgent.SetMaxSpeed(_runAwaySpeed);
            var runAwayState = new RunAwayState<CowardStateKey>(
                CowardStateKey.RunAway, _steeringAgent, _playerTransform,
                _evasionBlend, _eventChannel);

            _idleState.OnIdleComplete += () =>
            {
                ApplyRouletteSpeedTier();
                _patrolState.ResetCycleCount();
                _fsm.TransitionTo(CowardStateKey.Patrol);
            };

            _fsm.AddState(_patrolState);
            _fsm.AddState(_idleState);
            _fsm.AddState(runAwayState);
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
                }, "GoRunAway");

            var idleAction = new ActionNode(
                () =>
                {
                    if (!_fsm.IsInState(CowardStateKey.Idle))
                        _fsm.TransitionTo(CowardStateKey.Idle);
                }, "GoIdle");

            var patrolAction = new ActionNode(
                () =>
                {
                    if (!_fsm.IsInState(CowardStateKey.Patrol))
                        _fsm.TransitionTo(CowardStateKey.Patrol);
                }, "GoPatrol");

            var shouldIdleOrPatrol = new QuestionNode(
                condition: () => _fsm.IsInState(CowardStateKey.Idle) ||
                                 _patrolState.PatrolCycleCount >= _patrolCyclesBeforeIdle,
                trueNode: idleAction,
                falseNode: patrolAction);

            // Coward always runs when the player is visible — no attack ever
            return new QuestionNode(
                condition: () => 
                {
                    //Run if player is seen
                    if (_los.CanSee(_playerTransform)) return true;

                    //If we are escaping, continue running away even if player isn't seen
                    if (_fsm.IsInState(CowardStateKey.RunAway))
                    {
                        float distSqr = (transform.position - _playerTransform.position).sqrMagnitude;
                        if (distSqr < _safeEscapeDistance * _safeEscapeDistance)
                            return true; // Still too close.
                    }

                    //If we can't see them and far away, it's safe.
                    return false;
                },
                trueNode: runAwayAction,
                falseNode: shouldIdleOrPatrol);
        }

        // ── Roulette Wheel Application ────────────────────────────────────────

        private void ApplyRouletteSpeedTier()
        {
            string tier = RouletteWheelSelector.Select(_speedTiers);

            float chosenSpeed = tier switch
            {
                "SlowShuffle" => _patrolSpeed * 0.5f, // Barely moving after a scare
                "Normal" => _patrolSpeed,
                "Skittish" => _patrolSpeed * 1.8f, // Jumpy, erratic speed
                _ => _patrolSpeed
            };

            _patrolState.SetPatrolSpeed(chosenSpeed);
            _eventChannel?.RaiseStateChanged($"CowardSpeed:{tier}"); // Hook for debug/VFX
        }
    }
}