using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ForeverEngine.Procedural.Editor
{
    /// <summary>
    /// One-click scaffolder for the bake pipeline's manual acceptance step.
    /// Builds a saved scene containing a 1024x1024 Terrain with Perlin-noise hills,
    /// 4 solid-color TerrainLayers assigned, and a 4-quadrant alphamap paint so the
    /// macro-baker has something non-trivial to consume. Not part of Phase 1 runtime
    /// code - this only exists to eliminate manual terrain-painting clicks.
    /// </summary>
    public static class CreateTestBakeScene
    {
        private const string SceneDir = "Assets/Scenes";
        private const string ScenePath = "Assets/Scenes/TestBake.unity";
        private const string LayerDir = "Assets/BakedTestResources/TerrainLayers";
        private const int HeightmapRes = 257;
        private const int AlphamapRes = 256;
        private const float TerrainSize = 1024f;
        private const float TerrainHeight = 200f;

        [MenuItem("Forever Engine/Bake/Create Test Bake Scene")]
        public static void Build()
        {
            Directory.CreateDirectory(SceneDir);
            Directory.CreateDirectory(LayerDir);

            // Phase 1: batch-create all asset files (textures + terrain layers)
            // inside a StartAssetEditing/StopAssetEditing pair so Unity processes
            // the imports as one transaction. Mixing file I/O with individual
            // ImportAsset calls outside this pair can deadlock the AssetDatabase
            // lock on Unity 6 ("Access version should be odd when acquiring lock").
            TerrainLayer[] layers = null;
            try
            {
                AssetDatabase.StartAssetEditing();

                // 1a. Write all 4 PNG textures to disk + queue for import.
                var texPaths = new[]
                {
                    (name: "TL_Grass",  color: new Color(0.35f, 0.55f, 0.20f)),
                    (name: "TL_Forest", color: new Color(0.15f, 0.30f, 0.10f)),
                    (name: "TL_Rock",   color: new Color(0.45f, 0.45f, 0.45f)),
                    (name: "TL_Sand",   color: new Color(0.80f, 0.70f, 0.45f)),
                };
                foreach (var (name, color) in texPaths)
                {
                    var texPath = Path.Combine(LayerDir, name + "_diffuse.png").Replace('\\', '/');
                    if (!File.Exists(texPath))
                    {
                        var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                        var pixels = new Color[32 * 32];
                        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
                        tex.SetPixels(pixels);
                        tex.Apply();
                        File.WriteAllBytes(texPath, tex.EncodeToPNG());
                        Object.DestroyImmediate(tex);
                    }
                }
            }
            finally
            {
                // StopAssetEditing must run even if something above threw,
                // otherwise the AssetDatabase stays locked forever.
                AssetDatabase.StopAssetEditing();
            }

            // Phase 2: force-import the textures outside the batch so Unity
            // processes them synchronously and they're ready when we reference
            // them from the TerrainLayers.
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // Phase 3: create TerrainLayer assets, each referencing its texture.
            try
            {
                AssetDatabase.StartAssetEditing();

                layers = new TerrainLayer[4];
                var layerSpecs = new[]
                {
                    (name: "TL_Grass",  color: new Color(0.35f, 0.55f, 0.20f)),
                    (name: "TL_Forest", color: new Color(0.15f, 0.30f, 0.10f)),
                    (name: "TL_Rock",   color: new Color(0.45f, 0.45f, 0.45f)),
                    (name: "TL_Sand",   color: new Color(0.80f, 0.70f, 0.45f)),
                };
                for (int i = 0; i < 4; i++)
                {
                    var layerPath = Path.Combine(LayerDir, layerSpecs[i].name + ".terrainlayer").Replace('\\', '/');
                    var texPath   = Path.Combine(LayerDir, layerSpecs[i].name + "_diffuse.png").Replace('\\', '/');
                    var loadedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

                    var existing = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
                    if (existing != null)
                    {
                        existing.diffuseTexture = loadedTex;
                        existing.tileSize = new Vector2(16f, 16f);
                        EditorUtility.SetDirty(existing);
                        layers[i] = existing;
                    }
                    else
                    {
                        var layer = new TerrainLayer { diffuseTexture = loadedTex, tileSize = new Vector2(16f, 16f) };
                        AssetDatabase.CreateAsset(layer, layerPath);
                        layers[i] = layer;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Phase 4: build the scene (no asset-database batching here -
            // scene save is a separate operation).
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var td = new TerrainData { heightmapResolution = HeightmapRes };
            td.size = new Vector3(TerrainSize, TerrainHeight, TerrainSize);

            var heights = new float[HeightmapRes, HeightmapRes];
            for (int z = 0; z < HeightmapRes; z++)
            {
                for (int x = 0; x < HeightmapRes; x++)
                {
                    float u = (float)x / HeightmapRes;
                    float v = (float)z / HeightmapRes;
                    float h = 0f;
                    float amp = 0.3f;
                    float freq = 4f;
                    for (int o = 0; o < 4; o++)
                    {
                        h += Mathf.PerlinNoise(u * freq, v * freq) * amp;
                        freq *= 2f; amp *= 0.5f;
                    }
                    heights[z, x] = Mathf.Clamp01(h);
                }
            }
            td.SetHeights(0, 0, heights);

            td.terrainLayers = layers;
            td.alphamapResolution = AlphamapRes;

            var alpha = new float[AlphamapRes, AlphamapRes, 4];
            for (int z = 0; z < AlphamapRes; z++)
            {
                for (int x = 0; x < AlphamapRes; x++)
                {
                    int dominant;
                    if (x < AlphamapRes / 2 && z < AlphamapRes / 2) dominant = 0;
                    else if (x >= AlphamapRes / 2 && z < AlphamapRes / 2) dominant = 1;
                    else if (x < AlphamapRes / 2 && z >= AlphamapRes / 2) dominant = 2;
                    else dominant = 3;
                    for (int l = 0; l < 4; l++) alpha[z, x, l] = (l == dominant) ? 0.95f : 0.01667f;
                }
            }
            td.SetAlphamaps(0, 0, alpha);

            var go = Terrain.CreateTerrainGameObject(td);
            go.transform.position = Vector3.zero;
            go.name = "TestBakeTerrain";

            var lightGO = new GameObject("Sun");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log("[CreateTestBakeScene] Ready. Run Forever Engine -> Bake -> Macro (Active Terrain) next.");
            EditorUtility.DisplayDialog("Test Bake Scene",
                $"Created scene at {ScenePath}.\nNext: Forever Engine -> Bake -> Categorize Asset Packs, then Macro.",
                "OK");
        }
    }
}
