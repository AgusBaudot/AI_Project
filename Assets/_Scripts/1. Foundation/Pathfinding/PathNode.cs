using System.Collections.Generic;
using UnityEngine;

namespace Foundation
{
    /// <summary>
    /// A single node in the A* pathfinding graph.
    ///
    /// Responsibility:
    ///   Holds world position and a list of directly reachable neighbors.
    ///   Neighbors are wired at runtime by PathNodeGrid - PathNode itself
    ///   knows nothing about the grid layout or obstacle detection.
    ///
    /// A* scratch fields (GCost, HCost, Parent):
    ///   These are written by AStarPathfinder during a search pass and reset
    ///   before each new search. They live here (rather than in a parallel
    ///   dictionary) to avoid dictionary overhead on hot inner-loop lookups.
    ///   Trade-off: PathNode is not safe for concurrent searches on the same
    ///   graph, which is fine - AI runs single-threaded on the main thread.
    ///
    /// MonoBehaviour choice:
    ///   Nodes are GameObjects so they can be placed, snapped, and visualized
    ///   in the Scene view without a custom Editor. The overhead of ~50-100
    ///   MonoBehaviours is negligible at this scale.
    /// </summary>
    [AddComponentMenu("AI/Pathfinding/Path Node")]
    public class PathNode : MonoBehaviour
    {
        // ── Neighbor Graph ───────────────────────────────────────────────────

        // Populated by PathNodeGrid.ConnectNeighbors() at runtime.
        // ReadOnly at search time - never modified during A*.
        public List<PathNode> Neighbors { get; } = new List<PathNode>();

        // ── A* Scratch Space ─────────────────────────────────────────────────

        // GCost: accumulated cost from the start node.
        // HCost: heuristic estimate to the goal.
        // FCost: GCost + HCost - the priority key used by the open set.
        public float GCost { get; set; }
        public float HCost { get; set; }
        public float FCost => GCost + HCost;

        // Back-pointer for path reconstruction.
        public PathNode Parent { get; set; }

        // ── Convenience ──────────────────────────────────────────────────────

        public Vector3 Position => transform.position;

        /// <summary>
        /// Resets A* scratch fields before each new search.
        /// Called by AStarPathfinder.FindPath() on every node it opens.
        /// </summary>
        public void ResetSearchData()
        {
            GCost = float.MaxValue;
            HCost = 0f;
            Parent = null;
        }

        public void AddNeighbor(PathNode node)
        {
            if (node != null && node != this && !Neighbors.Contains(node))
                Neighbors.Add(node);
        }

        // ── Scene Gizmos ─────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            Gizmos.DrawSphere(transform.position, 0.18f);

            // Draw neighbor connections as thin lines
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
            foreach (var neighbor in Neighbors)
            {
                if (neighbor != null)
                    Gizmos.DrawLine(transform.position, neighbor.Position);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            foreach (var neighbor in Neighbors)
            {
                if (neighbor != null)
                    Gizmos.DrawLine(transform.position, neighbor.Position);
            }
        }
    }
}