using UnityEngine;
using ForeverEngine.Core.World.Baked;

namespace ForeverEngine.Procedural
{
    /// <summary>
    /// Writes per-vertex biome colours into a chunk mesh via bilinear blending
    /// of the 4 surrounding macro biome cells. Edge vertices between chunks
    /// sample identical world coordinates so neighbours converge → seamless
    /// colour gradient across biome boundaries. When no baked source is
    /// available, writes white (no tint) so the underlying material shows
    /// through unchanged.
    /// </summary>
    public static class BiomeVertexColorer
    {
        /// <summary>
        /// Populate <paramref name="mesh"/>.colors using biome colours sampled
        /// from <paramref name="source"/> at each vertex's world position.
        ///
        /// <paramref name="chunkOrigin"/> is the chunk's world-origin corner
        /// (a vertex at mesh-local (0,0,0) corresponds to this world position).
        /// <paramref name="chunkSize"/> is the chunk's physical edge length.
        /// <paramref name="res"/> is the mesh subdivision count (mesh has
        /// (res+1) × (res+1) vertices).
        /// </summary>
        public static void WriteVertexColors(
            Mesh mesh,
            Vector3 chunkOrigin,
            float chunkSize,
            int res,
            BakedChunkSource source)
        {
            int vertCount = (res + 1) * (res + 1);
            var colors = new Color[vertCount];

            if (source == null)
            {
                for (int i = 0; i < vertCount; i++) colors[i] = Color.white;
                mesh.colors = colors;
                return;
            }

            var index = source.Index;
            float cellSize = index.CellSize;
            float originX = index.Origin.X;
            float originZ = index.Origin.Z;
            float step = chunkSize / res;

            for (int z = 0; z <= res; z++)
            {
                for (int x = 0; x <= res; x++)
                {
                    float worldX = chunkOrigin.x + x * step;
                    float worldZ = chunkOrigin.z + z * step;

                    // Continuous cell coord relative to layer origin.
                    float u = (worldX - originX) / cellSize;
                    float v = (worldZ - originZ) / cellSize;

                    int cx0 = Mathf.FloorToInt(u);
                    int cz0 = Mathf.FloorToInt(v);
                    int cx1 = cx0 + 1;
                    int cz1 = cz0 + 1;
                    float fx = u - cx0;
                    float fz = v - cz0;

                    var c00 = SampleCellColor(source, cx0, cz0, originX, originZ, cellSize);
                    var c10 = SampleCellColor(source, cx1, cz0, originX, originZ, cellSize);
                    var c01 = SampleCellColor(source, cx0, cz1, originX, originZ, cellSize);
                    var c11 = SampleCellColor(source, cx1, cz1, originX, originZ, cellSize);

                    float w00 = (1 - fx) * (1 - fz);
                    float w10 = fx * (1 - fz);
                    float w01 = (1 - fx) * fz;
                    float w11 = fx * fz;

                    var blended = new Color(
                        w00 * c00.r + w10 * c10.r + w01 * c01.r + w11 * c11.r,
                        w00 * c00.g + w10 * c10.g + w01 * c01.g + w11 * c11.g,
                        w00 * c00.b + w10 * c10.b + w01 * c01.b + w11 * c11.b,
                        1f);
                    colors[z * (res + 1) + x] = blended;
                }
            }

            mesh.colors = colors;
        }

        private static Color SampleCellColor(
            BakedChunkSource source, int cellX, int cellZ,
            float originX, float originZ, float cellSize)
        {
            // Convert absolute cell coord to world coord at cell center.
            float worldX = originX + (cellX + 0.5f) * cellSize;
            float worldZ = originZ + (cellZ + 0.5f) * cellSize;

            if (!source.TryGetTileForWorld(worldX, worldZ, out var macro))
                return BiomeTable.BaseColor((BiomeType)0);  // missing tile = ocean byte 0

            // Cell local index within this tile.
            var h = macro.Header;
            int tileCellOffsetX = Mathf.FloorToInt((h.WorldMinX - originX) / cellSize);
            int tileCellOffsetZ = Mathf.FloorToInt((h.WorldMinZ - originZ) / cellSize);
            int localX = cellX - tileCellOffsetX;
            int localZ = cellZ - tileCellOffsetZ;

            if (localX < 0 || localX >= h.MacroWidthCells || localZ < 0 || localZ >= h.MacroHeightCells)
                return BiomeTable.BaseColor((BiomeType)0);

            byte biomeByte = macro.Biome[localZ * h.MacroWidthCells + localX];
            return BiomeTable.BaseColor((BiomeType)biomeByte);
        }
    }
}
