using System;

namespace Foundation
{
    public class QuestionNode : IDecisionNode
    {
        private readonly Func<bool> _condition;
        private readonly IDecisionNode _trueNode; // Branch taken when condition == true
        private readonly IDecisionNode _falseNode; // Branch taken when condition == false

        public QuestionNode(Func<bool> condition, IDecisionNode trueNode, IDecisionNode falseNode)
        {
            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
            _trueNode = trueNode ?? throw new ArgumentNullException(nameof(trueNode));
            _falseNode = falseNode ?? throw new ArgumentNullException(nameof(falseNode));
        }

        /// <summary>
        /// Evaluates the condition and recurses into the appropriate child.
        /// Returns the ActionNode that was ultimately reached and executed.
        /// </summary>
        public IDecisionNode MakeDecision()
        {
            return _condition()
                ? _trueNode.MakeDecision()
                : _falseNode.MakeDecision();
        }
    }
}