using System.Collections.Generic;
using UnityEngine;

namespace Foundation
{
    /// <summary>
    /// Static A* pathfinder operating on a PathNode graph.
    ///
    /// Algorithm - Standard A* with Euclidean heuristic:
    ///   f(n) = g(n) + h(n)
    ///   g(n) = true accumulated cost from start (sum of edge lengths)
    ///   h(n) = straight-line distance to goal (admissible, never overestimates)
    ///
    ///   An admissible heuristic guarantees the first path found is optimal.
    ///   For a flat top-down map with uniform-cost edges, Euclidean distance
    ///   is the tightest admissible heuristic available.
    ///
    /// Open Set - SortedList as min-heap substitute:
    ///   A proper binary min-heap (priority queue) would be O(log n) per
    ///   push/pop.
    ///   SortedList&lt;float, PathNode&gt; is used here:
    ///     - Insert: O(log n)
    ///     - ExtractMin: O(log n) via RemoveAt(0)
    ///   For 50-100 nodes it is fast enough and avoids external dependencies.
    ///
    /// Closed Set - HashSet&lt;PathNode&gt;:
    ///   O(1) membership test. A node enters the closed set when it is popped
    ///   from the open set (the "expanded" set in classic A* terminology).
    ///
    /// Scratch Data Reset:
    ///   PathNode.GCost / HCost / Parent are reset on every node before search
    ///   begins. This avoids stale data from a previous search corrupting
    ///   the current one without requiring a full parallel-array reset.
    ///
    /// Path Reconstruction:
    ///   We walk the Parent back-pointer chain from goal to start, then reverse.
    ///   The returned list goes from [0]=first step to [n-1]=goal.
    ///   Index 0 is typically the node nearest the agent's current position -
    ///   PathfindingState advances through the list with Arrival steering.
    /// </summary>
    public static class AStarPathfinder
    {
        // Small epsilon used to break FCost ties in the SortedList key.
        private const float KeyEpsilon = 0.0001f;

        /// <summary>
        /// Finds the shortest path from <paramref name="start"/> to <paramref name="goal"/>.
        /// Returns an empty list if no path exists.
        /// </summary>
        /// <param name="start">Starting PathNode (nearest to agent position).</param>
        /// <param name="goal">Goal PathNode (nearest to target position).</param>
        /// <param name="allNodes">
        ///   All nodes in the graph - used to reset scratch data before search.
        ///   Pass PathNodeGrid.AllNodes.
        /// </param>
        public static List<PathNode> FindPath(
            PathNode start,
            PathNode goal,
            IReadOnlyList<PathNode> allNodes)
        {
            var result = new List<PathNode>();

            if (start == null || goal == null) return result;

            if (start == goal)
            {
                result.Add(goal);
                return result;
            }

            // ── Reset scratch data on all nodes ──────────────────────────────
            // Ensures GCost/HCost/Parent from a prior search don't pollute this one.
            if (allNodes != null)
            {
                foreach (var node in allNodes)
                    node?.ResetSearchData();
            }

            // ── Data Structures ───────────────────────────────────────────────
            // openSet: min-heap by FCost - SortedList gives us O(log n) insert/extract
            // Key = FCost + tiny unique offset to avoid duplicate-key exceptions
            var openSet = new SortedList<float, PathNode>();
            var closedSet = new HashSet<PathNode>();

            // Counters used solely to generate unique SortedList keys on FCost ties
            int keyCounter = 0;

            // ── Initialize Start Node ─────────────────────────────────────────
            start.GCost = 0f;
            start.HCost = HeuristicCost(start, goal);
            openSet.Add(start.FCost + KeyEpsilon * keyCounter++, start);

            // ── Main A* Loop ──────────────────────────────────────────────────
            while (openSet.Count > 0)
            {
                // Pop node with lowest FCost
                PathNode current = openSet.Values[0];
                openSet.RemoveAt(0);

                // Already processed? (can happen if we updated GCost on a node
                // already in the open set - we re-insert rather than decrease-key)
                if (closedSet.Contains(current)) continue;

                closedSet.Add(current);

                // ── Goal Reached ──────────────────────────────────────────────
                if (current == goal)
                    return ReconstructPath(goal);

                // ── Expand Neighbors ──────────────────────────────────────────
                foreach (var neighbor in current.Neighbors)
                {
                    if (neighbor == null || closedSet.Contains(neighbor)) continue;

                    // Edge cost = Euclidean distance between the two nodes
                    float edgeCost = Vector3.Distance(current.Position, neighbor.Position);
                    float tentativeG = current.GCost + edgeCost;

                    // Found a better path to this neighbor?
                    if (tentativeG < neighbor.GCost)
                    {
                        neighbor.GCost = tentativeG;
                        neighbor.HCost = HeuristicCost(neighbor, goal);
                        neighbor.Parent = current;

                        // Insert (re-insert) with updated priority.
                        // We don't remove the old entry - the closed-set guard
                        // above handles the duplicate when it's eventually popped.
                        openSet.Add(neighbor.FCost + KeyEpsilon * keyCounter++, neighbor);
                    }
                }
            }

            // No path found - return empty list
            Debug.LogWarning($"[AStarPathfinder] No path found from '{start.name}' to '{goal.name}'.");
            return result;
        }

        // ── Private Helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Euclidean distance heuristic.
        /// Admissible (never overestimates) for a graph with straight-line edges.
        /// </summary>
        private static float HeuristicCost(PathNode from, PathNode to)
            => Vector3.Distance(from.Position, to.Position);

        /// <summary>
        /// Walks the Parent back-pointer chain from goal to start and reverses.
        /// Returns the path in traversal order: [start_step, ..., goal].
        /// </summary>
        private static List<PathNode> ReconstructPath(PathNode goal)
        {
            var path = new List<PathNode>();
            PathNode current = goal;

            while (current != null)
            {
                path.Add(current);
                current = current.Parent;
            }

            path.Reverse();
            return path;
        }
    }
}