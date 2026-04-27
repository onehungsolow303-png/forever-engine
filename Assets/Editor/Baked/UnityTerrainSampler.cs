using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ForeverEngine.Core.World.Baked;

namespace ForeverEngine.Procedural.Editor
{
    /// <summary>
    /// Samples a Unity Terrain (typically Gaia-authored) at arbitrary
    /// resolutions. Heightmap is returned in meters (unnormalized).
    /// Biome / splat / features sampled from the Terrain's paint layers.
    /// </summary>
    public static class UnityTerrainSampler
    {
        // Heightmap samplers store TRUE world Y in meters. The maxHeightMeters
        // parameter is retained as an upper-bound sanity clamp only; values
        // above it are clamped down. Earlier code rescaled by
        // (world_y / size.y) * maxHeightMeters, which inflated stored values
        // by maxHeightMeters/size.y when those differed (e.g. m_tileHeight=800
        // in BuildConiferousMedium → 1.25x inflation), causing runtime terrain
        // mesh to render 25% taller than reality while GaiaPlacementExtractor's
        // tree props stayed at TRUE world Y → props rendered ~20% below the
        // visible surface. Storing true meters keeps both axes in the same
        // reference frame.
        public static float[] SampleHeightmap(Terrain terrain, int widthCells, int heightCells, float maxHeightMeters)
        {
            var td = terrain.terrainData;
            var result = new float[widthCells * heightCells];
            for (int z = 0; z < heightCells; z++)
            {
                for (int x = 0; x < widthCells; x++)
                {
                    float nx = (float)x / (widthCells - 1);
                    float nz = (float)z / (heightCells - 1);
                    float worldY = td.GetInterpolatedHeight(nx, nz);
                    result[z * widthCells + x] = Mathf.Min(worldY, maxHeightMeters);
                }
            }
            return result;
        }

        /// <summary>
        /// High-res heightmap for Unity Terrain rendering at runtime. Output
        /// is a vertexCount × vertexCount float array in TRUE WORLD METERS
        /// (not normalized, not rescaled). Runtime BakedTerrainTileRenderer
        /// uses TileHeightMeters=1000 as TerrainData.size.y; heights up to
        /// 1000 round-trip exactly. vertexCount must be 2^n + 1 to map
        /// directly to TerrainData.heightmapResolution.
        /// </summary>
        public static float[] SampleHighResHeightmap(Terrain terrain, int vertexCount, float maxHeightMeters)
        {
            var td = terrain.terrainData;
            var result = new float[vertexCount * vertexCount];
            for (int z = 0; z < vertexCount; z++)
            {
                float nz = (float)z / (vertexCount - 1);
                for (int x = 0; x < vertexCount; x++)
                {
                    float nx = (float)x / (vertexCount - 1);
                    float worldY = td.GetInterpolatedHeight(nx, nz);
                    result[z * vertexCount + x] = Mathf.Min(worldY, maxHeightMeters);
                }
            }
            return result;
        }

        public static byte[] SampleSplat(Terrain terrain, int widthCells, int heightCells)
        {
            var td = terrain.terrainData;
            int am = td.alphamapResolution;
            var raw = td.GetAlphamaps(0, 0, am, am);
            int layerCount = Mathf.Min(4, raw.GetLength(2));
            var result = new byte[widthCells * heightCells * 4];
            for (int z = 0; z < heightCells; z++)
            {
                for (int x = 0; x < widthCells; x++)
                {
                    float nx = (float)x / (widthCells - 1) * (am - 1);
                    float nz = (float)z / (heightCells - 1) * (am - 1);
                    int ix = Mathf.Clamp(Mathf.RoundToInt(nx), 0, am - 1);
                    int iz = Mathf.Clamp(Mathf.RoundToInt(nz), 0, am - 1);
                    int baseIdx = (z * widthCells + x) * 4;
                    for (int layer = 0; layer < 4; layer++)
                    {
                        byte w = layer < layerCount ? (byte)(Mathf.Clamp01(raw[iz, ix, layer]) * 255) : (byte)0;
                        result[baseIdx + layer] = w;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// High-res splat for the wire path. Box-filter downsamples the
        /// terrain's alphamapTextures from native resolution (typically 1024)
        /// to gridSize × gridSize. Each layer in the union tree is read via
        /// perTerrainLayerToUnionIndex; tile layers that don't appear in the
        /// union are dropped, union layers absent on this tile contribute zero.
        ///
        /// Output packed as [layer * grid * grid + z * grid + x], byte weights
        /// 0-255. Layer count = unionLayerCount (the layer-level union size).
        /// </summary>
        public static byte[] SampleHighResSplat(
            Terrain terrain,
            int gridSize,
            int unionLayerCount,
            int[] perTerrainLayerToUnionIndex)
        {
            var td = terrain.terrainData;
            int am = td.alphamapResolution;
            int tileLayerCount = td.terrainLayers != null ? td.terrainLayers.Length : 0;
            if (tileLayerCount == 0 || unionLayerCount == 0)
                return System.Array.Empty<byte>();

            var raw = td.GetAlphamaps(0, 0, am, am);
            int srcLayerCount = System.Math.Min(tileLayerCount, raw.GetLength(2));
            int cells = gridSize * gridSize;
            var result = new byte[unionLayerCount * cells];

            // Box-filter: each output cell averages the source pixels under it.
            // Source span per output cell = am / gridSize (e.g. 1024/256 = 4).
            float spanF = (float)am / gridSize;
            for (int z = 0; z < gridSize; z++)
            {
                int srcZ0 = (int)(z * spanF);
                int srcZ1 = System.Math.Min(am, (int)((z + 1) * spanF));
                if (srcZ1 <= srcZ0) srcZ1 = srcZ0 + 1;
                for (int x = 0; x < gridSize; x++)
                {
                    int srcX0 = (int)(x * spanF);
                    int srcX1 = System.Math.Min(am, (int)((x + 1) * spanF));
                    if (srcX1 <= srcX0) srcX1 = srcX0 + 1;
                    int sampleCount = (srcZ1 - srcZ0) * (srcX1 - srcX0);

                    for (int srcL = 0; srcL < srcLayerCount; srcL++)
                    {
                        int unionIdx = perTerrainLayerToUnionIndex[srcL];
                        if (unionIdx < 0 || unionIdx >= unionLayerCount) continue;
                        float sum = 0f;
                        for (int sz = srcZ0; sz < srcZ1; sz++)
                            for (int sx = srcX0; sx < srcX1; sx++)
                                sum += raw[sz, sx, srcL];
                        float avg = sum / sampleCount;
                        result[unionIdx * cells + z * gridSize + x] =
                            (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(avg) * 255f), 0, 255);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Reads terrain.terrainData.treeInstances → BakedTreeInstance[] in
        /// world coordinates. PrototypeIndex is the union-table index resolved
        /// via perTerrainProtoToUnionIndex (-1 entries skip — prototype not in
        /// union, e.g. its prefab GUID couldn't be resolved).
        /// </summary>
        public static BakedTreeInstance[] SampleTreeInstances(
            Terrain terrain,
            int[] perTerrainProtoToUnionIndex)
        {
            var td = terrain.terrainData;
            var src = td.treeInstances;
            if (src == null || src.Length == 0) return System.Array.Empty<BakedTreeInstance>();

            var origin = terrain.transform.position;
            var size = td.size;
            var result = new List<BakedTreeInstance>(src.Length);
            for (int i = 0; i < src.Length; i++)
            {
                var t = src[i];
                if (t.prototypeIndex < 0 || t.prototypeIndex >= perTerrainProtoToUnionIndex.Length) continue;
                int union = perTerrainProtoToUnionIndex[t.prototypeIndex];
                if (union < 0 || union > ushort.MaxValue) continue;

                float worldX = origin.x + t.position.x * size.x;
                float worldY = origin.y + t.position.y * size.y;
                float worldZ = origin.z + t.position.z * size.z;
                float yawDeg = t.rotation * Mathf.Rad2Deg;
                result.Add(new BakedTreeInstance(
                    PrototypeIndex: (ushort)union,
                    WorldX: worldX, WorldY: worldY, WorldZ: worldZ,
                    YawDegrees: yawDeg,
                    WidthScale: t.widthScale, HeightScale: t.heightScale));
            }
            return result.ToArray();
        }

        /// <summary>
        /// Returns the Unity Asset GUID for a UnityEngine.Object (TerrainLayer,
        /// prefab GameObject, etc.). Empty string if the object isn't an asset
        /// or AssetDatabase can't resolve a path.
        /// </summary>
        public static string AssetGuidOf(Object asset)
        {
            if (asset == null) return string.Empty;
            var path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path)) return string.Empty;
            return AssetDatabase.AssetPathToGUID(path) ?? string.Empty;
        }

        public static byte[] SampleBiome(Terrain terrain, int widthCells, int heightCells, BiomeType[] splatLayerToBiome)
        {
            var splat = SampleSplat(terrain, widthCells, heightCells);
            var result = new byte[widthCells * heightCells];
            for (int i = 0; i < result.Length; i++)
            {
                int baseIdx = i * 4;
                int maxLayer = 0;
                byte maxWeight = splat[baseIdx];
                for (int layer = 1; layer < 4; layer++)
                {
                    if (splat[baseIdx + layer] > maxWeight)
                    {
                        maxWeight = splat[baseIdx + layer];
                        maxLayer = layer;
                    }
                }
                var biome = maxLayer < splatLayerToBiome.Length ? splatLayerToBiome[maxLayer] : BiomeType.Grassland;
                result[i] = (byte)biome;
            }
            return result;
        }
    }
}
