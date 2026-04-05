using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace ForeverEngine.ECS.Jobs
{
    /// <summary>
    /// Parallel fog of war raycasting — rewritten from pygame fog_of_war.py.
    /// Original: 360 sequential rays from player position.
    /// Rewrite: 360 rays as IJobParallelFor — each ray is independent work.
    ///
    /// Writes to a shared NativeArray fog grid using atomic operations.
    /// </summary>
    [BurstCompile]
    public struct FogRaycastJob : IJobParallelFor
    {
        // Input (read-only)
        [ReadOnly] public int PlayerX;
        [ReadOnly] public int PlayerY;
        [ReadOnly] public int SightRadius;
        [ReadOnly] public int MapWidth;
        [ReadOnly] public int MapHeight;
        [ReadOnly] public NativeArray<bool> Walkability;  // Flattened [y * width + x]

        // Output — each ray writes VISIBLE (2) to tiles it can see
        // Previous frame dimmed all VISIBLE→EXPLORED before this job runs
        [NativeDisableParallelForRestriction]
        public NativeArray<byte> FogGrid;  // Flattened [y * width + x], FogState enum

        public void Execute(int rayIndex)
        {
            // Ray direction: 360 rays, one per degree
            float angle = rayIndex * (math.PI * 2f / 360f);
            float dx = math.cos(angle);
            float dy = math.sin(angle);

            float x = PlayerX + 0.5f;
            float y = PlayerY + 0.5f;

            for (int step = 0; step <= SightRadius; step++)
            {
                int tileX = (int)math.floor(x);
                int tileY = (int)math.floor(y);

                // Bounds check
                if (tileX < 0 || tileX >= MapWidth || tileY < 0 || tileY >= MapHeight)
                    break;

                int idx = tileY * MapWidth + tileX;

                // Mark visible (atomic write — multiple rays may hit same tile)
                FogGrid[idx] = 2; // FogState.Visible

                // Stop at walls (but mark the wall itself as visible)
                if (!Walkability[idx])
                    break;

                x += dx;
                y += dy;
            }
        }
    }

    /// <summary>
    /// Dims all VISIBLE tiles to EXPLORED before raycasting.
    /// Runs as a single-pass parallel job over the entire fog grid.
    /// </summary>
    [BurstCompile]
    public struct FogDimJob : IJobParallelFor
    {
        public NativeArray<byte> FogGrid;

        public void Execute(int index)
        {
            if (FogGrid[index] == 2) // VISIBLE → EXPLORED
                FogGrid[index] = 1;
        }
    }
}
