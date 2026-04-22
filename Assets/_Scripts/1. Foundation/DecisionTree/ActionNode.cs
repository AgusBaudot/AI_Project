using System;

namespace Foundation
{
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