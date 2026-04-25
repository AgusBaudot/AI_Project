namespace Foundation
{
    /// <summary>
    /// Uniform interface for all Decision Tree nodes.
    ///
    /// Design Pattern — Composite:
    ///   QuestionNode (branch) and ActionNode (leaf) both implement this interface.
    ///   The tree can be traversed uniformly with a single MakeDecision() call
    ///   on the root — no instanceof checks or type casts are needed anywhere.
    ///
    /// MakeDecision() contract:
    ///   - QuestionNode: evaluates its condition, delegates to a child.
    ///   - ActionNode:   executes its action, returns 'this' to signal termination.
    ///   The returned node is the leaf that ran; callers can inspect it for
    ///   logging or debugging without coupling to the action's implementation.
    /// </summary>
    public interface IDecisionNode
    {
        IDecisionNode MakeDecision();
    }
}