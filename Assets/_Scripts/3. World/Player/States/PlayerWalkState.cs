using Foundation;
using Core;
using UnityEngine;

namespace World
{
    public sealed class PlayerWalkState : IState<PlayerStateKey>
    {
        public PlayerStateKey StateKey { get; }

        private readonly PlayerController _player;
        private readonly SteeringAgent _agent;
        private readonly float _speed;

        public PlayerWalkState(PlayerStateKey key, PlayerController player,
            SteeringAgent agent, float speed)
        {
            StateKey = key;
            _player = player;
            _agent = agent;
            _speed = speed;
        }

        public void OnEnter()
        {
        }

        public void OnTick(float deltaTime)
        {
            // GetMoveInput returns a normalized direction; scale to desired speed.
            // SteeringAgent.Move() automatically composites obstacle avoidance —
            // the player also benefits from the whisker avoidance system.
            Vector3 direction = _player.GetMoveInput();
            _agent.Move(direction * _speed);
        }

        public void OnExit()
        {
        }
    }
}