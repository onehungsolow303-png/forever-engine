using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace ForeverEngine.ECS.Jobs
{
    /// <summary>
    /// A* pathfinding on tile grid — replaces pygame ai.py greedy chase.
    /// Original: Single-step greedy (pick axis with larger delta, blocked = stuck).
    /// Rewrite: Full A* with proper obstacle navigation, Burst-compiled.
    ///
    /// Run as IJobParallelFor to batch-pathfind for all NPCs simultaneously.
    /// Each NPC gets its own path result in the output arrays.
    /// </summary>
    [BurstCompile]
    public struct PathfindJob : IJobParallelFor
    {
        // Map data (shared read-only across all pathfind requests)
        [ReadOnly] public int MapWidth;
        [ReadOnly] public int MapHeight;
        [ReadOnly] public NativeArray<bool> Walkability;
        [ReadOnly] public NativeArray<int2> OccupiedTiles;  // Other creature positions

        // Per-NPC input
        [ReadOnly] public NativeArray<int2> StartPositions;
        [ReadOnly] public NativeArray<int2> TargetPositions;
        [ReadOnly] public NativeArray<int> MaxSteps;

        // Per-NPC output: next tile to move to (-1,-1 if no path)
        [WriteOnly] public NativeArray<int2> NextSteps;
        [WriteOnly] public NativeArray<bool> PathFound;

        public void Execute(int npcIndex)
        {
            var start = StartPositions[npcIndex];
            var target = TargetPositions[npcIndex];
            int maxSteps = MaxSteps[npcIndex];

            // Early out: already adjacent
            if (ManhattanDist(start, target) <= 1)
            {
                NextSteps[npcIndex] = start; // Stay put
                PathFound[npcIndex] = true;
                return;
            }

            // A* search with fixed-size open/closed sets
            // Using NativeHashMap for visited, NativeHeap would be ideal but
            // we use a simple sorted insert into NativeList for Burst compatibility
            var result = AStarSearch(start, target, maxSteps);
            NextSteps[npcIndex] = result.NextStep;
            PathFound[npcIndex] = result.Found;
        }

        private struct SearchResult
        {
            public int2 NextStep;
            public bool Found;
        }

        private SearchResult AStarSearch(int2 start, int2 goal, int maxSteps)
        {
            // Directions: 4-connected grid (no diagonals for D&D movement)
            var dirs = new NativeArray<int2>(4, Allocator.Temp);
            dirs[0] = new int2(0, 1);   // Up
            dirs[1] = new int2(0, -1);  // Down
            dirs[2] = new int2(-1, 0);  // Left
            dirs[3] = new int2(1, 0);   // Right

            var openSet = new NativeList<int2>(64, Allocator.Temp);
            var gScore = new NativeHashMap<int2, int>(256, Allocator.Temp);
            var cameFrom = new NativeHashMap<int2, int2>(256, Allocator.Temp);

            openSet.Add(start);
            gScore[start] = 0;

            var result = new SearchResult { NextStep = new int2(-1, -1), Found = false };
            int iterations = 0;
            int maxIterations = maxSteps * 20; // Safety cap

            while (openSet.Length > 0 && iterations < maxIterations)
            {
                iterations++;

                // Find node with lowest fScore in open set
                int bestIdx = 0;
                int bestF = int.MaxValue;
                for (int i = 0; i < openSet.Length; i++)
                {
                    int g = gScore[openSet[i]];
                    int h = ManhattanDist(openSet[i], goal);
                    int f = g + h;
                    if (f < bestF) { bestF = f; bestIdx = i; }
                }

                var current = openSet[bestIdx];
                openSet.RemoveAtSwapBack(bestIdx);

                // Goal reached — reconstruct first step
                if (current.Equals(goal))
                {
                    result.Found = true;
                    result.NextStep = ReconstructFirstStep(cameFrom, start, goal);
                    break;
                }

                int currentG = gScore[current];

                for (int d = 0; d < 4; d++)
                {
                    var neighbor = current + dirs[d];

                    // Bounds + walkability check
                    if (neighbor.x < 0 || neighbor.x >= MapWidth ||
                        neighbor.y < 0 || neighbor.y >= MapHeight)
                        continue;

                    int idx = neighbor.y * MapWidth + neighbor.x;
                    if (!Walkability[idx])
                        continue;

                    // Check occupied (skip occupied tiles except goal)
                    if (!neighbor.Equals(goal) && IsOccupied(neighbor))
                        continue;

                    int tentativeG = currentG + 1;

                    if (!gScore.TryGetValue(neighbor, out int existingG) || tentativeG < existingG)
                    {
                        gScore[neighbor] = tentativeG;
                        cameFrom[neighbor] = current;

                        // Add to open set if not already there
                        bool inOpen = false;
                        for (int i = 0; i < openSet.Length; i++)
                        {
                            if (openSet[i].Equals(neighbor)) { inOpen = true; break; }
                        }
                        if (!inOpen) openSet.Add(neighbor);
                    }
                }
            }

            dirs.Dispose();
            openSet.Dispose();
            gScore.Dispose();
            cameFrom.Dispose();

            return result;
        }

        private int2 ReconstructFirstStep(NativeHashMap<int2, int2> cameFrom, int2 start, int2 goal)
        {
            var current = goal;
            while (cameFrom.TryGetValue(current, out int2 prev))
            {
                if (prev.Equals(start))
                    return current;
                current = prev;
            }
            return start; // Fallback
        }

        private bool IsOccupied(int2 pos)
        {
            for (int i = 0; i < OccupiedTiles.Length; i++)
            {
                if (OccupiedTiles[i].Equals(pos))
                    return true;
            }
            return false;
        }

        private static int ManhattanDist(int2 a, int2 b)
        {
            return math.abs(a.x - b.x) + math.abs(a.y - b.y);
        }
    }
}
