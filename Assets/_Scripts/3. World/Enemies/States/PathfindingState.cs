using System;
using System.Collections.Generic;
using Core;
using Foundation;
using UnityEngine;

namespace World
{
    /// <summary>
    /// Pathfinding State: follows (or flees along) an A* path using Arrival steering.
    ///
    /// Two Modes -Chase vs Flee:
    ///   Chase mode (_mode = Chase): advances through path nodes toward the goal.
    ///   Flee mode (_mode = Flee): advances through path nodes AWAY from the threat.
    ///
    ///   For flee mode, AStarPathfinder finds the node nearest the threat,
    ///   then finds the node farthest from it that is still reachable —
    ///   specifically, the node with the greatest distance to the threat that
    ///   has a valid path from the agent's nearest node.
    ///   This produces a "flee to the opposite end of the map" behavior.
    ///
    /// Path Refresh:
    ///   The path is refreshed every _refreshInterval seconds. This keeps the
    ///   agent responsive to a moving target without calling A* every frame.
    ///   0.3s interval: visually smooth re-routing, low CPU cost.
    ///
    /// Arrival Steering:
    ///   Each waypoint is approached with SteeringBehaviors.Arrival so the agent
    ///   decelerates smoothly and doesn't oscillate around nodes.
    ///   Obstacle avoidance is composited automatically by SteeringAgent.Move().
    ///
    /// Integration with Steering Handoff:
    ///   This state is entered by the decision tree when CanSee() returns false.
    ///   When CanSee() returns true again, the decision tree transitions back to
    ///   AttackState (Aggressor) or RunAwayState (Coward), resuming steering.
    ///   No explicit handoff code is needed here - the FSM handles it.
    /// </summary>
    public enum PathfindingMode
    {
        Chase,
        Flee
    }

    public sealed class PathfindingState<TKey> : IState<TKey>
        where TKey : struct, IEquatable<TKey>
    {
        public TKey StateKey { get; }
        public bool IsPathComplete => _path == null || _pathIndex >= _path.Count;

        private readonly SteeringAgent _agent;
        private readonly Transform _targetTransform; // Player - either chased or fled from
        private readonly PathNodeGrid _grid;
        private readonly PathfindingMode _mode;
        private readonly float _waypointThreshold; // Distance to consider a node reached
        private readonly float _refreshInterval; // Seconds between path recalculations

        private List<PathNode> _path = new List<PathNode>();
        private int _pathIndex;
        private float _refreshTimer;

        public PathfindingState(
            TKey key,
            SteeringAgent agent,
            Transform targetTransform,
            PathNodeGrid grid,
            PathfindingMode mode = PathfindingMode.Chase,
            float waypointThreshold = 0.6f,
            float refreshInterval = 0.35f)
        {
            StateKey = key;
            _agent = agent;
            _targetTransform = targetTransform;
            _grid = grid;
            _mode = mode;
            _waypointThreshold = waypointThreshold;
            _refreshInterval = refreshInterval;
        }

        // ── IState Lifecycle ─────────────────────────────────────────────────

        public void OnEnter()
        {
            _refreshTimer = _refreshInterval; // Force immediate recalculation on first tick
            _path.Clear();
            _pathIndex = 0;
        }

        public void OnTick(float deltaTime)
        {
            if (_targetTransform == null) return;

            // ── Path Refresh ─────────────────────────────────────────────────
            _refreshTimer -= deltaTime;
            if (_refreshTimer <= 0f)
            {
                _refreshTimer = _refreshInterval;
                RefreshPath();
            }

            // ── Follow Path ──────────────────────────────────────────────────
            if (_path == null || _pathIndex >= _path.Count)
            {
                _agent.Stop();
                return;
            }

            Vector3 myPos = _agent.transform.position;
            Vector3 targetWaypoint = _path[_pathIndex].Position;

            // Arrival: smooth deceleration into each node
            Vector3 desired = SteeringBehaviors.Arrival(
                myPos, targetWaypoint, _agent.MaxSpeed, slowingRadius: 1.2f);

            _agent.Move(desired);

            // Advance to next waypoint when close enough
            float distSqr = (myPos - targetWaypoint).sqrMagnitude;
            if (distSqr < _waypointThreshold * _waypointThreshold)
                _pathIndex++;
        }

        public void OnExit()
        {
            _agent.Stop();
            _path.Clear();
        }

        // ── Path Calculation ─────────────────────────────────────────────────

        private void RefreshPath()
        {
            PathNode startNode = _grid.GetNearestNode(_agent.transform.position);

            PathNode goalNode = _mode == PathfindingMode.Chase
                ? _grid.GetNearestNode(_targetTransform.position)
                : GetFurthestNodeFrom(_targetTransform.position, startNode);

            if (startNode == null || goalNode == null) return;

            var newPath = AStarPathfinder.FindPath(startNode, goalNode, _grid.AllNodes);

            if (newPath != null && newPath.Count > 0)
            {
                _path = newPath;
                _pathIndex = 0;
            }
        }

        /// <summary>
        /// For Flee mode: finds the reachable node that is farthest from the threat.
        ///
        /// Strategy - sample all nodes and pick the one with the greatest
        /// distance to the threat that also has a valid A* path from startNode.
        ///
        /// We use a lightweight candidate-list approach:
        ///   1. Score every node by distance to threat.
        ///   2. Try them in descending order.
        ///   3. Return the first one that A* can actually reach from startNode.
        ///
        /// In practice the first or second candidate always succeeds on a
        /// well-connected graph, so the cost is rarely more than two A* calls.
        /// </summary>
        private PathNode GetFurthestNodeFrom(
            Vector3 threatPos,
            PathNode startNode)
        {
            if (_grid.AllNodes == null || _grid.AllNodes.Count == 0) return null;

            // Score all nodes by distance to threat
            var candidates = new List<(PathNode node, float dist)>();
            foreach (var node in _grid.AllNodes)
            {
                if (node == null) continue;

                candidates.Add((node, Vector3.Distance(node.Position, threatPos)));
            }

            // Sort descending (farthest first)
            candidates.Sort((a, b) => b.dist.CompareTo(a.dist));

            // Return the farthest node reachable from startNode
            // Try up to the top 5 candidates to avoid expensive exhaustive search
            int tries = Mathf.Min(5, candidates.Count);
            for (int i = 0; i < tries; i++)
            {
                var candidate = candidates[i].node;
                if (candidate == startNode) continue;

                var testPath = AStarPathfinder.FindPath(startNode, candidate, _grid.AllNodes);
                if (testPath != null && testPath.Count > 0)
                    return candidate;
            }

            // Fallback: just return the nearest node to something distant
            return candidates.Count > 0 ? candidates[0].node : null;
        }
    }
}