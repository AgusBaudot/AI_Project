using Foundation;
using Core;

namespace World
{
    public sealed class PlayerIdleState : IState<PlayerStateKey>
    {
        public PlayerStateKey StateKey => PlayerStateKey.Idle;

        private readonly SteeringAgent _agent;

        public PlayerIdleState(PlayerStateKey key, SteeringAgent agent)
        {
            _agent = agent;
        }

        // Zero velocity on entry — prevents the agent sliding after releasing keys
        public void OnEnter() => _agent.Stop();

        public void OnTick(float dt)
        {
            /* Idle: do nothing */
        }

        public void OnExit()
        {
        }
    }
}