using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using ForeverEngine.Core.World.Baked;

namespace ForeverEngine.Tests.EditMode.World.Baked
{
    /// <summary>
    /// Programmatically builds a 2x2 synthetic Terrain scene (flat heights per tile
    /// with a per-tile signature), invokes BakeAllTilesInScene, reads back via
    /// BakedChunkSource, and asserts every macro cell matches its source tile's
    /// signature. Guards the full writer-index-reader-chunksource pipeline.
    ///
    /// Height scaling note: UnityTerrainSampler.SampleHeightmap normalises against
    /// terrainData.size.y then multiplies by MacroBakeTool.MaxHeightMeters (1000f).
    /// With terrain size.y = 500f the output is signatureHeight * (1000/500) = 2x.
    /// Assertions use expectedBaked = signatureHeight * 2f.
    /// </summary>
    public class MacroBakeMultiTileE2ETests
    {
        private const float TileSize = 1024f;
        private const int HeightmapResolution = 65;  // small enough to keep test fast
        private const string LayerDir = "C:/Dev/.shared/baked/planet/layer_0";

        [SetUp]
        public void RequireCatalog()
        {
            var catalog = ForeverEngine.Procedural.AssetPackBiomeCatalog.Load();
            if (catalog == null || catalog.Entries == null)
                Assert.Ignore(
                    "AssetPackBiomeCatalog not found in Resources/. " +
                    "Run 'Forever Engine → Bake → Categorize Asset Packs' first.");
        }

        [Test]
        public void Bake_2x2_Scene_RoundTripsThroughBakedChunkSource()
        {
            // Clean prior state so this test is deterministic.
            if (Directory.Exists(LayerDir))
                Directory.Delete(LayerDir, recursive: true);

            // Create a throwaway scene with 4 flat Terrains at grid positions.
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);

            var tiles = new (int x, int z, float signatureHeight)[]
            {
                (0, 0, 100f),
                (1, 0, 110f),
                (0, 1, 120f),
                (1, 1, 130f),
            };

            foreach (var t in tiles)
            {
                var data = new TerrainData
                {
                    heightmapResolution = HeightmapResolution,
                    size = new Vector3(TileSize, 500f, TileSize),
                };
                // Flat heights = signatureHeight / 500f (TerrainData stores normalized).
                float norm = t.signatureHeight / 500f;
                int res = data.heightmapResolution;
                var heights = new float[res, res];
                for (int iz = 0; iz < res; iz++)
                    for (int ix = 0; ix < res; ix++)
                        heights[iz, ix] = norm;
                data.SetHeights(0, 0, heights);

                var go = Terrain.CreateTerrainGameObject(data);
                go.transform.position = new Vector3(t.x * TileSize, 0f, t.z * TileSize);
            }

            // Invoke the throwing entry point so failures propagate as test failures.
            ForeverEngine.Procedural.Editor.MacroBakeTool.BakeAllTilesInSceneOrThrow();

            try
            {
                // Load back through BakedChunkSource.
                var src = BakedChunkSource.Load(LayerDir, layerId: 0);
                foreach (var t in tiles)
                {
                    Assert.IsTrue(src.TryGetTile(t.x, t.z, out var macro),
                        $"tile ({t.x},{t.z}) missing from cache");

                    // UnityTerrainSampler normalises against terrain size.y (500f) then
                    // multiplies by MaxHeightMeters (1000f), so baked = signatureHeight * 2.
                    float expectedBaked = t.signatureHeight * 2f;

                    float min = float.MaxValue, max = float.MinValue;
                    for (int i = 0; i < macro.Heightmap.Length; i++)
                    { min = Mathf.Min(min, macro.Heightmap[i]); max = Mathf.Max(max, macro.Heightmap[i]); }
                    Assert.That(min, Is.EqualTo(expectedBaked).Within(2f),
                        $"tile ({t.x},{t.z}) min height drift");
                    Assert.That(max, Is.EqualTo(expectedBaked).Within(2f),
                        $"tile ({t.x},{t.z}) max height drift");
                }

                // Index.json sanity.
                var index = BakedWorldReader.LoadLayerIndex(LayerDir);
                Assert.AreEqual(4, index.Tiles.Length);
                Assert.AreEqual(0, index.Grid.MinTileX);
                Assert.AreEqual(1, index.Grid.MaxTileX);
                Assert.AreEqual(0, index.Grid.MinTileZ);
                Assert.AreEqual(1, index.Grid.MaxTileZ);
            }
            finally
            {
                // Cleanup: the test writes to the shared baked planet dir, which is
                // gitignored but shouldn't linger. Restore so no stale state leaks
                // into subsequent tests or the 5950X deploy step.
                try { if (Directory.Exists(LayerDir)) Directory.Delete(LayerDir, recursive: true); }
                catch { /* best-effort */ }
            }
        }
    }
}
