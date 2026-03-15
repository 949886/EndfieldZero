using System;
using System.Collections.Generic;
using EndfieldZero.Core;
using EndfieldZero.World;
using Godot;

namespace EndfieldZero.Pathfinding;

/// <summary>
/// A* pathfinding service on the block grid.
/// Operates on world-block coordinates (int x, z).
/// Uses WorldManager.GetBlock() for walkability checks.
/// Returns paths as List&lt;Vector2I&gt; in block coordinates.
///
/// Optimizations:
///   - Max search nodes limit (prevents infinite searching)
///   - Movement cost from BlockDef.MoveSpeedMod
///   - Diagonal movement supported (cost √2)
/// </summary>
public class PathfindingService
{
    /// <summary>Max nodes to expand before giving up.</summary>
    public int MaxSearchNodes { get; set; } = 4096;

    private readonly WorldManager _world;

    public PathfindingService(WorldManager world)
    {
        _world = world;
    }

    /// <summary>Singleton instance.</summary>
    public static PathfindingService Instance { get; set; }

    /// <summary>
    /// Find a path from start to goal in block coordinates.
    /// Returns null if no path found or search exhausted.
    /// </summary>
    public List<Vector2I> FindPath(Vector2I start, Vector2I goal)
    {
        if (start == goal)
            return new List<Vector2I> { start };

        // Check if goal is walkable
        if (!IsWalkable(goal.X, goal.Y))
        {
            // Try to find the nearest walkable block to the goal
            goal = FindNearestWalkable(goal);
            if (goal == start) return new List<Vector2I> { start };
        }

        var openSet = new SortedSet<AStarNode>(new AStarNodeComparer());
        var allNodes = new Dictionary<long, AStarNode>();

        var startNode = new AStarNode(start.X, start.Y, 0f, Heuristic(start, goal), -1, -1);
        openSet.Add(startNode);
        allNodes[PackKey(start.X, start.Y)] = startNode;

        int expanded = 0;

        while (openSet.Count > 0 && expanded < MaxSearchNodes)
        {
            var current = GetFirst(openSet);
            openSet.Remove(current);
            current.Closed = true;
            expanded++;

            // Goal reached
            if (current.X == goal.X && current.Z == goal.Y)
            {
                return ReconstructPath(current, allNodes);
            }

            // Expand neighbors (8-directional)
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;

                    int nx = current.X + dx;
                    int nz = current.Z + dz;

                    if (!IsWalkable(nx, nz)) continue;

                    // Diagonal: check both adjacent cells to prevent corner-cutting
                    if (dx != 0 && dz != 0)
                    {
                        if (!IsWalkable(current.X + dx, current.Z) ||
                            !IsWalkable(current.X, current.Z + dz))
                            continue;
                    }

                    float moveCost = GetMoveCost(nx, nz);
                    float stepCost = (dx != 0 && dz != 0) ? 1.414f * moveCost : moveCost;
                    float newG = current.G + stepCost;

                    long key = PackKey(nx, nz);

                    if (allNodes.TryGetValue(key, out var existing))
                    {
                        if (existing.Closed || newG >= existing.G)
                            continue;

                        // Better path found — update
                        openSet.Remove(existing);
                        existing.G = newG;
                        existing.F = newG + Heuristic(new Vector2I(nx, nz), goal);
                        existing.ParentX = current.X;
                        existing.ParentZ = current.Z;
                        openSet.Add(existing);
                    }
                    else
                    {
                        var neighbor = new AStarNode(
                            nx, nz, newG,
                            newG + Heuristic(new Vector2I(nx, nz), goal),
                            current.X, current.Z
                        );
                        openSet.Add(neighbor);
                        allNodes[key] = neighbor;
                    }
                }
            }
        }

        return null; // No path found
    }

    /// <summary>Convert a block-coordinate path to world-space 3D positions (center of each block).</summary>
    public static List<Vector3> PathToWorld(List<Vector2I> blockPath)
    {
        if (blockPath == null) return null;

        var worldPath = new List<Vector3>(blockPath.Count);
        float half = Settings.BlockPixelSize * 0.5f;

        foreach (var bp in blockPath)
        {
            worldPath.Add(new Vector3(
                bp.X * Settings.BlockPixelSize + half,
                0f,
                bp.Y * Settings.BlockPixelSize + half
            ));
        }

        return worldPath;
    }

    /// <summary>Convert a world 3D position to block coordinates.</summary>
    public static Vector2I WorldToBlock(Vector3 worldPos)
    {
        int bx = Mathf.FloorToInt(worldPos.X / Settings.BlockPixelSize);
        int bz = Mathf.FloorToInt(worldPos.Z / Settings.BlockPixelSize);
        return new Vector2I(bx, bz);
    }

    // --- Internal ---

    private bool IsWalkable(int bx, int bz)
    {
        var block = _world.GetBlock(bx, bz);
        if (block.IsAir) return true;

        var def = BlockRegistry.Instance.GetDef(block.TypeId);
        return def != null && !def.IsSolid && def.MoveSpeedMod > 0f;
    }

    private float GetMoveCost(int bx, int bz)
    {
        var block = _world.GetBlock(bx, bz);
        if (block.IsAir) return 1f;

        var def = BlockRegistry.Instance.GetDef(block.TypeId);
        if (def == null || def.MoveSpeedMod <= 0f) return 999f;

        return 1f / def.MoveSpeedMod; // Slow terrain = higher cost
    }

    private Vector2I FindNearestWalkable(Vector2I center)
    {
        for (int r = 1; r <= 5; r++)
        {
            for (int dz = -r; dz <= r; dz++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dz) != r) continue;
                    int nx = center.X + dx, nz = center.Y + dz;
                    if (IsWalkable(nx, nz))
                        return new Vector2I(nx, nz);
                }
            }
        }
        return center;
    }

    private static float Heuristic(Vector2I a, Vector2I b)
    {
        // Octile distance
        int dx = Math.Abs(a.X - b.X);
        int dz = Math.Abs(a.Y - b.Y);
        return Math.Max(dx, dz) + 0.414f * Math.Min(dx, dz);
    }

    private static long PackKey(int x, int z)
    {
        return ((long)(x + 1000000) << 32) | (uint)(z + 1000000);
    }

    private static List<Vector2I> ReconstructPath(AStarNode goal, Dictionary<long, AStarNode> allNodes)
    {
        var path = new List<Vector2I>();
        var current = goal;

        while (current != null)
        {
            path.Add(new Vector2I(current.X, current.Z));
            if (current.ParentX == -1) break;

            long parentKey = PackKey(current.ParentX, current.ParentZ);
            current = allNodes.GetValueOrDefault(parentKey);
        }

        path.Reverse();
        return path;
    }

    private static AStarNode GetFirst(SortedSet<AStarNode> set)
    {
        using var e = set.GetEnumerator();
        e.MoveNext();
        return e.Current;
    }

    // --- A* Node ---

    private class AStarNode
    {
        public int X, Z;
        public float G, F;
        public int ParentX, ParentZ;
        public bool Closed;
        private readonly int _id;
        private static int _nextId;

        public AStarNode(int x, int z, float g, float f, int px, int pz)
        {
            X = x; Z = z; G = g; F = f; ParentX = px; ParentZ = pz;
            _id = _nextId++;
        }

        public int Id => _id;
    }

    private class AStarNodeComparer : IComparer<AStarNode>
    {
        public int Compare(AStarNode a, AStarNode b)
        {
            int cmp = a.F.CompareTo(b.F);
            return cmp != 0 ? cmp : a.Id.CompareTo(b.Id);
        }
    }
}
