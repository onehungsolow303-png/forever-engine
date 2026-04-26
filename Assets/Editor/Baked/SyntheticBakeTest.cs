#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ForeverEngine.Procedural.Editor
{
    /// <summary>
    /// B6 verification harness for the runtime-bake-extension Sub B work.
    /// Builds a 2×2 grid of 1024 m Terrains entirely in code with three real
    /// terrain layers, a non-trivial alphamap, and ten tree instances per tile,
    /// then drives them directly through MacroBakeTool. Bypasses Gaia's spawn
    /// pipeline so the bake-writer + sampler can be exercised without
    /// depending on Gaia's compute shaders (currently broken — see
    /// project_gaia_bake_csmain_blocked.md).
    ///
    ///   Unity.exe -batchmode -nographics -projectPath "C:/Dev/Forever engine" \
    ///     -executeMethod ForeverEngine.Procedural.Editor.SyntheticBakeTest.Run \
    ///     -logFile "C:/tmp/synthetic-bake.log"
    ///
    /// On success the bake at C:/Dev/.shared/baked/planet/layer_0/tile_*_*/macro/
    /// will contain splat_hires.bin and tree_instances.bin alongside the
    /// existing pre-Sub-B files. Exit code 0 = pass, 1 = fail.
    /// </summary>
    public static class SyntheticBakeTest
    {
        private const string ScenePath = "Assets/Scenes/SyntheticBakeTest.unity";
        private const string TreePrefabPath = "Assets/BakedTestResources/SyntheticTree.prefab";
        private const string LayersDir = "Assets/BakedTestResources/TerrainLayers";
        private const string AssetDir = "Assets/BakedTestResources";

        private const float TileSize = 1024f;
        private const float TileHeight = 1000f;
        private const int HeightmapResolution = 513;
        private const int AlphamapResolution = 1024;
        private const int TreeInstanceCount = 10;
        private const int TilesPerSide = 2;

        public static void Run()
        {
            try
            {
                var treePrefab = EnsureSyntheticTreePrefab();
                var layers = LoadTestTerrainLayers();
                if (layers.Length < 2)
                    throw new InvalidOperationException(
                        $"Expected ≥2 .terrainlayer assets in {LayersDir}, found {layers.Length}.");

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log($"[SyntheticBake] new scene at {ScenePath}");

                for (int tz = 0; tz < TilesPerSide; tz++)
                for (int tx = 0; tx < TilesPerSide; tx++)
                    BuildSyntheticTerrain(tx, tz, layers, treePrefab);

                MacroBakeTool.BakeAllTilesInSceneOrThrow();

                VerifyAllTileOutputs(layers.Length);

                Debug.Log("[SyntheticBake] === PASS ===");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SyntheticBake] FAIL: {e}");
                EditorApplication.Exit(1);
            }
        }

        private static GameObject EnsureSyntheticTreePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(TreePrefabPath);
            if (existing != null) return existing;

            Directory.CreateDirectory(Path.GetDirectoryName(TreePrefabPath)!);
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            temp.name = "SyntheticTree";
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, TreePrefabPath);
            UnityEngine.Object.DestroyImmediate(temp);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SyntheticBake] created tree prefab at {TreePrefabPath} (GUID {AssetDatabase.AssetPathToGUID(TreePrefabPath)})");
            return prefab;
        }

        private static TerrainLayer[] LoadTestTerrainLayers()
        {
            var guids = AssetDatabase.FindAssets("t:TerrainLayer", new[] { LayersDir });
            var picked = new System.Collections.Generic.List<TerrainLayer>();
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                if (layer != null)
                {
                    picked.Add(layer);
                    if (picked.Count == 3) break;
                }
            }
            Debug.Log($"[SyntheticBake] loaded {picked.Count} terrain layers from {LayersDir}");
            return picked.ToArray();
        }

        private static void BuildSyntheticTerrain(int tileX, int tileZ, TerrainLayer[] layers, GameObject treePrefab)
        {
            var td = new TerrainData
            {
                heightmapResolution = HeightmapResolution,
                alphamapResolution = AlphamapResolution,
                baseMapResolution = 256,
            };
            td.size = new Vector3(TileSize, TileHeight, TileSize);

            // Per-tile height offset gives the playtest something visible to walk over.
            float baseHeight = 0.05f + 0.02f * (tileX + tileZ);
            var heights = new float[HeightmapResolution, HeightmapResolution];
            for (int z = 0; z < HeightmapResolution; z++)
            for (int x = 0; x < HeightmapResolution; x++)
            {
                float fx = (float)x / (HeightmapResolution - 1);
                float fz = (float)z / (HeightmapResolution - 1);
                heights[z, x] = baseHeight + 0.10f * Mathf.PerlinNoise((tileX + fx) * 4f, (tileZ + fz) * 4f);
            }
            td.SetHeights(0, 0, heights);

            td.terrainLayers = layers;

            int layerCount = layers.Length;
            var splat = new float[AlphamapResolution, AlphamapResolution, layerCount];
            // Per-tile dominant-layer rotation: gives each tile a different
            // visible biome so the boundary between tiles is obvious in playtest.
            int dominant = (tileX + tileZ * TilesPerSide) % layerCount;
            for (int z = 0; z < AlphamapResolution; z++)
            for (int x = 0; x < AlphamapResolution; x++)
            {
                float u = (float)x / (AlphamapResolution - 1);
                for (int l = 0; l < layerCount; l++)
                {
                    float center = (l + 0.5f) / layerCount;
                    float dist = Mathf.Abs(u - center);
                    splat[z, x, l] = Mathf.Clamp01(1f - dist * layerCount);
                }
                // Dominant-layer bias: 50% weight to the tile's dominant layer.
                splat[z, x, dominant] += 0.5f;
                float sum = 0f;
                for (int l = 0; l < layerCount; l++) sum += splat[z, x, l];
                if (sum > 0f)
                    for (int l = 0; l < layerCount; l++) splat[z, x, l] /= sum;
                else
                    splat[z, x, 0] = 1f;
            }
            td.SetAlphamaps(0, 0, splat);

            td.treePrototypes = new[] { new TreePrototype { prefab = treePrefab } };

            var instances = new TreeInstance[TreeInstanceCount];
            // Seed varies per tile so each tile has a distinct tree pattern.
            var rng = new System.Random(42 + tileX * 17 + tileZ * 31);
            for (int i = 0; i < TreeInstanceCount; i++)
            {
                instances[i] = new TreeInstance
                {
                    position = new Vector3((float)rng.NextDouble(), 0f, (float)rng.NextDouble()),
                    prototypeIndex = 0,
                    widthScale = 1f,
                    heightScale = 1f,
                    color = Color.white,
                    lightmapColor = Color.white,
                    rotation = (float)(rng.NextDouble() * Math.PI * 2.0),
                };
            }
            td.SetTreeInstances(instances, snapToHeightmap: false);

            var tdPath = $"{AssetDir}/SyntheticTerrainData_{tileX}_{tileZ}.asset";
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(tdPath) != null)
                AssetDatabase.DeleteAsset(tdPath);
            AssetDatabase.CreateAsset(td, tdPath);
            AssetDatabase.SaveAssets();

            var go = Terrain.CreateTerrainGameObject(td);
            go.name = $"SyntheticTerrain_{tileX}_{tileZ}";
            go.transform.position = new Vector3(tileX * TileSize, 0f, tileZ * TileSize);

            Debug.Log($"[SyntheticBake] terrain ({tileX},{tileZ}) ready: world {go.transform.position}, {layerCount} layers, dominant={dominant}, {instances.Length} trees");
        }

        private static void VerifyAllTileOutputs(int expectedLayerCount)
        {
            for (int tz = 0; tz < TilesPerSide; tz++)
            for (int tx = 0; tx < TilesPerSide; tx++)
                VerifyOneTileOutput(tx, tz, expectedLayerCount);
        }

        private static void VerifyOneTileOutput(int tileX, int tileZ, int expectedLayerCount)
        {
            string macroDir = $"C:/Dev/.shared/baked/planet/layer_0/tile_{tileX}_{tileZ}/macro";
            var splatPath = Path.Combine(macroDir, "splat_hires.bin");
            var treesPath = Path.Combine(macroDir, "tree_instances.bin");

            if (!File.Exists(splatPath))
                throw new InvalidOperationException($"missing {splatPath}");
            if (!File.Exists(treesPath))
                throw new InvalidOperationException($"missing {treesPath}");

            var splatBytes = File.ReadAllBytes(splatPath);
            if (splatBytes.Length < 5)
                throw new InvalidOperationException($"splat_hires.bin too small: {splatBytes.Length}");
            byte writtenLayerCount = splatBytes[0];
            int writtenGrid = BitConverter.ToInt32(splatBytes, 1);
            int payloadExpected = writtenLayerCount * writtenGrid * writtenGrid;
            int payloadActual = splatBytes.Length - 5;
            if (payloadActual != payloadExpected)
                throw new InvalidOperationException(
                    $"tile ({tileX},{tileZ}) splat_hires.bin payload mismatch: layers={writtenLayerCount} grid={writtenGrid} " +
                    $"expected payload {payloadExpected} actual {payloadActual}");

            var treeBytes = File.ReadAllBytes(treesPath);
            if (treeBytes.Length < 4)
                throw new InvalidOperationException($"tree_instances.bin too small: {treeBytes.Length}");
            int treeCount = BitConverter.ToInt32(treeBytes, 0);
            int treesPayloadExpected = treeCount * 26;
            int treesPayloadActual = treeBytes.Length - 4;
            if (treesPayloadActual != treesPayloadExpected)
                throw new InvalidOperationException(
                    $"tile ({tileX},{tileZ}) tree_instances.bin payload mismatch: count={treeCount} expected {treesPayloadExpected} actual {treesPayloadActual}");

            Debug.Log($"[SyntheticBake] tile ({tileX},{tileZ}): splat_hires {writtenLayerCount}×{writtenGrid}² ({splatBytes.Length}B), trees {treeCount} ({treeBytes.Length}B)");

            if (treeCount != TreeInstanceCount)
                throw new InvalidOperationException($"tile ({tileX},{tileZ}) tree count {treeCount} != expected {TreeInstanceCount}");
        }
    }
}
#endif
