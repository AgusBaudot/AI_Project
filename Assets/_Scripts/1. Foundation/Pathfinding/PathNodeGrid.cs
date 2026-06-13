using System.Collections.Generic;
using UnityEngine;

namespace Foundation
{
    /// <summary>
    /// Discovers all PathNodes in the scene and connects them as neighbors.
    ///
    /// Connection Strategy - Proximity + Line-of-Sight:
    ///   Two nodes become neighbors if:
    ///     1. Their distance is within _connectionRadius.
    ///     2. No obstacle (on _obstacleLayer) occludes the straight line between them.
    ///   This mirrors how the LOS sensor works — the same walls that block
    ///   vision also block pathfinding edges. A node behind a wall is never
    ///   a direct neighbor of a node in front of it.
    ///
    /// Why FindObjectsOfType at Start?
    ///   Nodes are placed as scene GameObjects. FindObjectsOfType&lt;PathNode&gt;
    ///   is called once at Start and the result is cached. The per-frame cost
    ///   is zero - only the one-time startup scan is O(n^2) in node count.
    ///   For ~50-100 nodes this is imperceptible (&lt; 1ms).
    ///
    /// Nearest-Node Lookup (GetNearestNode):
    ///   Linear scan - O(n). For 50-100 nodes this is faster than building a
    ///   spatial hash, which would dominate the startup budget for zero
    ///   runtime benefit at this scale.
    /// </summary>
    [AddComponentMenu("AI/Pathfinding/Path Node Grid")]
    public class PathNodeGrid : MonoBehaviour
    {
        [Header("Connection Settings")]
        [Tooltip("Maximum distance between two nodes for them to be considered neighbors.")]
        [SerializeField]
        private float _connectionRadius = 4f;

        [Tooltip("Physics layers that block the line-of-sight connection check.")] [SerializeField]
        private LayerMask _obstacleLayer;

        [Tooltip("Height offset applied when raycasting between nodes (avoids ground hits).")] [SerializeField]
        private float _raycastHeightOffset = 0.5f;

        // All nodes discovered in the scene
        private PathNode[] _allNodes;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Start()
        {
            _allNodes = FindObjectsOfType<PathNode>();

            if (_allNodes.Length == 0)
            {
                Debug.LogWarning("[PathNodeGrid] No PathNodes found in the scene. " +
                                 "Place PathNode components in the scene and ensure they are active.");
                return;
            }

            ConnectNeighbors();
        }

        // ── Neighbor Connection ──────────────────────────────────────────────

        /// <summary>
        /// For each pair of nodes within range, checks LOS and adds them as neighbors.
        /// O(n^2) - runs once at Start.
        /// </summary>
        private void ConnectNeighbors()
        {
            float radiusSqr = _connectionRadius * _connectionRadius;

            for (int i = 0; i < _allNodes.Length; i++)
            {
                for (int j = i + 1; j < _allNodes.Length; j++)
                {
                    PathNode a = _allNodes[i];
                    PathNode b = _allNodes[j];

                    // Fast distance gate - sqrMagnitude avoids sqrt
                    Vector3 delta = b.Position - a.Position;
                    if (delta.sqrMagnitude > radiusSqr) continue;

                    // LOS gate - reject pairs occluded by walls
                    Vector3 aPos = a.Position + Vector3.up * _raycastHeightOffset;
                    Vector3 bPos = b.Position + Vector3.up * _raycastHeightOffset;

                    if (Physics.Linecast(aPos, bPos, _obstacleLayer)) continue;

                    // Bidirectional connection
                    a.AddNeighbor(b);
                    b.AddNeighbor(a);
                }
            }
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Returns the PathNode whose world position is closest to <paramref name="worldPos"/>.
        /// Used by agents to snap their start/goal positions onto the graph.
        /// </summary>
        public PathNode GetNearestNode(Vector3 worldPos)
        {
            if (_allNodes == null || _allNodes.Length == 0) return null;

            PathNode nearest = null;
            float nearestSqr = float.MaxValue;

            foreach (var node in _allNodes)
            {
                if (node == null) continue;

                float sqr = (node.Position - worldPos).sqrMagnitude;

                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = node;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Returns a read-only view of all nodes. Used by AStarPathfinder to
        /// reset scratch data before each search.
        /// </summary>
        public IReadOnlyList<PathNode> AllNodes => _allNodes;

        // ── Scene Gizmos ─────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            // Draw the connection radius around the grid object as a guide
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, _connectionRadius);
        }
    }
}