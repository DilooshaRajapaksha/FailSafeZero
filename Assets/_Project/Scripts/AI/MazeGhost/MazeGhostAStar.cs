using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hand-coded grid A* pathfinder for the maze ghost.
/// f(n) = g(n) + w * h(n). The NavMeshAgent only follows the waypoints.
/// </summary>
public class MazeGhostAStar : MonoBehaviour
{
    [Header("Grid Settings")]
    public Collider mazeBounds;
    public Transform seeker;
    [Min(0.4f)] public float cellSize = 1f;
    [Min(0.1f)] public float agentRadius = 0.42f;
    [Min(0.5f)] public float agentHeight = 1.8f;
    [Min(0f)] public float floorClearance = 0.12f;

    [Header("A* Settings")]
    [Range(1f, 2f)] public float heuristicWeight = 1f;
    public bool allowDiagonal = true;
    public bool preventCornerCutting = true;
    [Min(100)] public int maxExploredNodes = 6000;

    public int LastExploredNodes { get; private set; }
    public int LastPathCells { get; private set; }
    public float LastPathCost { get; private set; }
    public string LastResult { get; private set; } = "Not run";

    private bool[,] walkable;
    private Vector3 gridMin;
    private int width;
    private int depth;
    private float floorY;
    private bool gridDirty = true;
    private readonly Collider[] overlapBuffer = new Collider[64];

    public void Configure(Collider bounds, Transform seekerTransform)
    {
        if (bounds != null)
            mazeBounds = bounds;
        seeker = seekerTransform;
    }

    public void MarkGridDirty()
    {
        gridDirty = true;
    }

    public List<Vector3> FindPath(Vector3 start, Vector3 target)
    {
        LastExploredNodes = 0;
        LastPathCells = 0;
        LastPathCost = 0f;

        if (!EnsureGrid())
            return null;

        int startX;
        int startZ;
        int goalX;
        int goalZ;

        if (!TryGetCell(start, out startX, out startZ, true))
        {
            LastResult = "Start not on grid";
            return null;
        }

        if (!TryGetCell(target, out goalX, out goalZ, true))
        {
            LastResult = "Target not on grid";
            return null;
        }

        int total = width * depth;
        int startIndex = Index(startX, startZ);
        int goalIndex = Index(goalX, goalZ);

        int[] cameFrom = new int[total];
        float[] gScore = new float[total];
        float[] hScore = new float[total];
        float[] fScore = new float[total];
        bool[] closed = new bool[total];
        bool[] inOpen = new bool[total];
        List<int> open = new List<int>(1024);

        for (int i = 0; i < total; i++)
        {
            cameFrom[i] = -1;
            gScore[i] = float.PositiveInfinity;
            hScore[i] = float.PositiveInfinity;
            fScore[i] = float.PositiveInfinity;
        }

        gScore[startIndex] = 0f;
        hScore[startIndex] = Heuristic(startX, startZ, goalX, goalZ);
        fScore[startIndex] = gScore[startIndex] + heuristicWeight * hScore[startIndex];
        open.Add(startIndex);
        inOpen[startIndex] = true;

        int explored = 0;
        while (open.Count > 0 && explored < maxExploredNodes)
        {
            // OPEN SET: choose the node with the lowest f(n), tie-break by h(n).
            int best = 0;
            for (int i = 1; i < open.Count; i++)
            {
                int candidate = open[i];
                int currentBest = open[best];
                if (fScore[candidate] < fScore[currentBest] ||
                    (Mathf.Approximately(fScore[candidate], fScore[currentBest]) &&
                     hScore[candidate] < hScore[currentBest]))
                {
                    best = i;
                }
            }

            int current = open[best];
            open.RemoveAt(best);
            inOpen[current] = false;

            if (current == goalIndex)
            {
                explored++;
                LastExploredNodes = explored;
                List<Vector3> path = ReconstructPath(cameFrom, goalIndex, start, target, goalX, goalZ);
                LastPathCells = path.Count;
                LastPathCost = gScore[goalIndex] + Vector3.Distance(CellCenter(goalX, goalZ, floorY), target);
                LastResult = "Found";
                return path;
            }

            closed[current] = true;
            explored++;

            int x = current % width;
            int z = current / width;

            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0)
                        continue;

                    bool diagonal = dx != 0 && dz != 0;
                    if (diagonal && !allowDiagonal)
                        continue;

                    int nx = x + dx;
                    int nz = z + dz;
                    if (!InBounds(nx, nz) || !walkable[nx, nz])
                        continue;

                    if (diagonal && preventCornerCutting &&
                        (!walkable[x + dx, z] || !walkable[x, z + dz]))
                        continue;

                    int neighbour = Index(nx, nz);
                    if (closed[neighbour])
                        continue;

                    // g(n): real distance travelled so far.
                    float stepCost = diagonal ? cellSize * 1.41421356f : cellSize;
                    float tentativeG = gScore[current] + stepCost;

                    if (!inOpen[neighbour] || tentativeG < gScore[neighbour])
                    {
                        cameFrom[neighbour] = current;
                        gScore[neighbour] = tentativeG;
                        hScore[neighbour] = Heuristic(nx, nz, goalX, goalZ);

                        // f(n) = g(n) + w * h(n).
                        fScore[neighbour] = gScore[neighbour] + heuristicWeight * hScore[neighbour];

                        if (!inOpen[neighbour])
                        {
                            open.Add(neighbour);
                            inOpen[neighbour] = true;
                        }
                    }
                }
            }
        }

        LastExploredNodes = explored;
        LastResult = "No path";
        return null;
    }

    private bool EnsureGrid()
    {
        if (!gridDirty && walkable != null)
            return true;

        if (mazeBounds == null)
        {
            GameObject floor = GameObject.Find("Maze_floor");
            if (floor != null)
                mazeBounds = floor.GetComponent<Collider>();
        }

        if (mazeBounds == null)
        {
            LastResult = "No maze bounds";
            return false;
        }

        Bounds bounds = mazeBounds.bounds;
        gridMin = new Vector3(bounds.min.x, bounds.center.y, bounds.min.z);
        width = Mathf.Max(2, Mathf.CeilToInt(bounds.size.x / cellSize));
        depth = Mathf.Max(2, Mathf.CeilToInt(bounds.size.z / cellSize));
        floorY = bounds.max.y;

        walkable = new bool[width, depth];
        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                walkable[x, z] = IsWalkable(CellCenter(x, z, floorY));
            }
        }

        gridDirty = false;
        return true;
    }

    private bool IsWalkable(Vector3 cellCenter)
    {
        Vector3 bottom = cellCenter + Vector3.up * (floorClearance + agentRadius);
        Vector3 top = cellCenter + Vector3.up * (floorClearance + agentHeight - agentRadius);

        int count = Physics.OverlapCapsuleNonAlloc(
            bottom,
            top,
            agentRadius,
            overlapBuffer,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < count; i++)
        {
            Collider hit = overlapBuffer[i];
            if (hit == null || hit.isTrigger)
                continue;

            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            if (seeker != null &&
                (hit.transform == seeker || hit.transform.IsChildOf(seeker)))
                continue;

            if (hit.CompareTag("Player"))
                continue;

            // Skip floor slabs. Only vertical walls/obstacles block a cell.
            if (hit.bounds.max.y <= floorY + floorClearance + 0.05f)
                continue;

            return false;
        }

        return true;
    }

    private List<Vector3> ReconstructPath(int[] cameFrom, int goalIndex, Vector3 start, Vector3 target, int goalX, int goalZ)
    {
        List<int> cells = new List<int>();
        int current = goalIndex;
        while (current != -1)
        {
            cells.Add(current);
            current = cameFrom[current];
        }
        cells.Reverse();

        List<Vector3> path = new List<Vector3>(cells.Count + 1);
        for (int i = 0; i < cells.Count; i++)
        {
            int index = cells[i];
            path.Add(CellCenter(index % width, index / width, floorY));
        }

        if (path.Count > 0)
            path[0] = start;

        Vector3 goalCenter = CellCenter(goalX, goalZ, floorY);
        if (path.Count == 0 || Vector3.Distance(goalCenter, target) > cellSize * 0.15f)
            path.Add(target);
        else
            path[path.Count - 1] = target;

        return path;
    }

    private float Heuristic(int x, int z, int goalX, int goalZ)
    {
        float dx = Mathf.Abs(x - goalX);
        float dz = Mathf.Abs(z - goalZ);

        if (!allowDiagonal)
            return cellSize * (dx + dz);

        // Octile distance: admissible heuristic for 8-direction grid movement.
        float straight = cellSize;
        float diagonal = cellSize * 1.41421356f;
        return diagonal * Mathf.Min(dx, dz) + straight * Mathf.Abs(dx - dz);
    }

    private bool TryGetCell(Vector3 world, out int x, out int z, bool snapToWalkable)
    {
        x = Mathf.Clamp(Mathf.FloorToInt((world.x - gridMin.x) / cellSize), 0, width - 1);
        z = Mathf.Clamp(Mathf.FloorToInt((world.z - gridMin.z) / cellSize), 0, depth - 1);

        if (walkable[x, z])
            return true;

        if (!snapToWalkable)
            return false;

        return FindNearestWalkable(x, z, out x, out z);
    }

    private bool FindNearestWalkable(int x, int z, out int nx, out int nz)
    {
        for (int radius = 1; radius <= 8; radius++)
        {
            for (int dz = -radius; dz <= radius; dz++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != radius)
                        continue;

                    int cx = x + dx;
                    int cz = z + dz;
                    if (InBounds(cx, cz) && walkable[cx, cz])
                    {
                        nx = cx;
                        nz = cz;
                        return true;
                    }
                }
            }
        }

        nx = x;
        nz = z;
        return false;
    }

    private Vector3 CellCenter(int x, int z, float y)
    {
        return new Vector3(
            gridMin.x + (x + 0.5f) * cellSize,
            y,
            gridMin.z + (z + 0.5f) * cellSize
        );
    }

    private bool InBounds(int x, int z)
    {
        return x >= 0 && x < width && z >= 0 && z < depth;
    }

    private int Index(int x, int z)
    {
        return z * width + x;
    }
}
