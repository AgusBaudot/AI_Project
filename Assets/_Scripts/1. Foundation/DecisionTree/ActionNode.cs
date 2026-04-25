using System;

namespace Foundation
{
    /// <summary>
    /// A leaf node that executes a side-effectful action and ends tree traversal.
    ///
    /// Action (void delegate) is used over a return type because the purpose of
    /// leaf actions is to cause state transitions or flag changes — they produce
    /// side effects, not values. Keeping the signature as Action makes construction
    /// at the call site clean:
    ///   new ActionNode(() => _fsm.TransitionTo(EnemyState.Attack), "GoAttack")
    ///
    /// The optional Label field supports decision logging and debugging:
    ///   var leaf = (ActionNode)_decisionRoot.MakeDecision();
    ///   Debug.Log(leaf.Label); returns "GoAttack"
    /// </summary>
    public class ActionNode : IDecisionNode
    {
        private readonly Action _action;
        public readonly string Label; // Optional, for debugging/logging

        public ActionNode(Action action, string label = "Unnamed")
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            Label = label;
        }

        /// <summary>
        /// Executes the action and returns 'this'.
        /// Returning 'this' lets the caller identify which action ran
        /// without requiring out parameters or tracking state externally.
        /// </summary>
        public IDecisionNode MakeDecision()
        {
            _action();
            return this;
        }
    }
}