using System.IO;
using NUnit.Framework;
using UnityEngine;
using ForeverEngine.Procedural;
using ForeverEngine.Core.World.Baked;
using ForeverEngine.Tests.EditMode.World.Baked;

namespace ForeverEngine.Tests.EditMode.World
{
    public class BiomeVertexColorerTests
    {
        private const float TileSize = 256f;   // small for fast test
        private const float CellSize = 64f;    // 4 cells per tile edge
        private const byte GrasslandByte = (byte)BiomeType.Grassland;
        private const byte DesertByte = (byte)BiomeType.Desert;

        [Test]
        public void WriteVertexColors_UniformBiome_WritesUniformColor()
        {
            var layerDir = WriteSingleTile(GrasslandByte);
            try
            {
                var src = BakedChunkSource.Load(layerDir, layerId: 0);
                // Mesh spans [64, 192] — entirely interior to tile [0, 256).
                // Vertex at worldX=64 (cell 1's left rim under shifted formula) → u=0.5, samples cells 0+1, both Grassland.
                // Interior vertices behave similarly — every sample lands within tile 0's cells.
                var mesh = BuildFlatMesh(originX: 0f, originZ: 0f, size: 128f, res: 4);
                BiomeVertexColorer.WriteVertexColors(mesh, new Vector3(64f, 0f, 64f), chunkSize: 128f, res: 4, source: src);

                var expected = BiomeTable.BaseColor(BiomeType.Grassland);
                foreach (var c in mesh.colors)
                {
                    Assert.That(c.r, Is.EqualTo(expected.r).Within(0.001f));
                    Assert.That(c.g, Is.EqualTo(expected.g).Within(0.001f));
                    Assert.That(c.b, Is.EqualTo(expected.b).Within(0.001f));
                }
            }
            finally { try { Directory.Delete(layerDir, recursive: true); } catch { } }
        }

        [Test]
        public void WriteVertexColors_CrossBiomeEdge_BlendsBilinearly()
        {
            // Two tiles side by side: tile (0,0)=Grassland, tile (1,0)=Desert.
            var layerDir = WriteTwoTiles(GrasslandByte, DesertByte);
            try
            {
                var src = BakedChunkSource.Load(layerDir, layerId: 0);
                // Mesh spanning [0, 512]. Chunk at world origin (0,0), size 512 (= 2 * TileSize).
                // Vertex (x=2, z=?) on a 5x5 mesh lands at worldX = 256 — exactly at the tile boundary.
                // Shifted bilinear: u = 256/64 - 0.5 = 3.5, cx0=3 (tile 0 cell 3, Grassland),
                // cx1=4 (tile 1 cell 4, Desert), fx=0.5 → 50/50 Grassland+Desert.
                var mesh = BuildFlatMesh(originX: 0f, originZ: 0f, size: TileSize * 2f, res: 4);
                BiomeVertexColorer.WriteVertexColors(mesh, Vector3.zero, chunkSize: TileSize * 2f, res: 4, source: src);

                // Vertex at (x=2, z=2) → worldX=256, worldZ=256. Both axes at tile boundaries; should
                // blend across all four surrounding cells: (3,3)=Grassland, (4,3)=Desert, (3,4)=Grassland(*),
                // (4,4)=Desert(*). (*) Note tile (1,1) and (0,1) don't exist → ocean for those corners.
                // To avoid the ocean complication, test the seam on x with z at cell 1 (interior),
                // so z=1 means worldZ=128, inside tile 0 entirely.
                int idxOnSeam = 1 * (4 + 1) + 2;  // z=1, x=2 → worldX=256, worldZ=128
                var seamColor = mesh.colors[idxOnSeam];

                var expectedMid = Color.Lerp(
                    BiomeTable.BaseColor(BiomeType.Grassland),
                    BiomeTable.BaseColor(BiomeType.Desert),
                    0.5f);

                Assert.That(seamColor.r, Is.EqualTo(expectedMid.r).Within(0.02f), "seam R");
                Assert.That(seamColor.g, Is.EqualTo(expectedMid.g).Within(0.02f), "seam G");
                Assert.That(seamColor.b, Is.EqualTo(expectedMid.b).Within(0.02f), "seam B");
            }
            finally { try { Directory.Delete(layerDir, recursive: true); } catch { } }
        }

        [Test]
        public void WriteVertexColors_DeepInsideTile_PureBiomeColor()
        {
            var layerDir = WriteTwoTiles(GrasslandByte, DesertByte);
            try
            {
                var src = BakedChunkSource.Load(layerDir, layerId: 0);
                var mesh = BuildFlatMesh(originX: 0f, originZ: 0f, size: TileSize * 2f, res: 4);
                BiomeVertexColorer.WriteVertexColors(mesh, Vector3.zero, chunkSize: TileSize * 2f, res: 4, source: src);

                // Vertex at x=1, z=1 → worldX=128, worldZ=128: both axes deep in tile 0.
                // Shifted: u=1.5, cx0=1, cx1=2, fx=0.5. Both cells in tile 0 → pure Grassland.
                int idxGrass = 1 * 5 + 1;
                // Vertex at x=3, z=1 → worldX=384, worldZ=128: deep in tile 1 on x, deep in tile 0 on z.
                // But z is worldZ=128, tile (1,0) covers z ∈ [0, 256), so vertex is in tile (1,0). Desert.
                int idxDesert = 1 * 5 + 3;

                var grass = BiomeTable.BaseColor(BiomeType.Grassland);
                var desert = BiomeTable.BaseColor(BiomeType.Desert);

                Assert.That(mesh.colors[idxGrass].r, Is.EqualTo(grass.r).Within(0.01f));
                Assert.That(mesh.colors[idxGrass].g, Is.EqualTo(grass.g).Within(0.01f));
                Assert.That(mesh.colors[idxGrass].b, Is.EqualTo(grass.b).Within(0.01f));
                Assert.That(mesh.colors[idxDesert].r, Is.EqualTo(desert.r).Within(0.01f));
                Assert.That(mesh.colors[idxDesert].g, Is.EqualTo(desert.g).Within(0.01f));
                Assert.That(mesh.colors[idxDesert].b, Is.EqualTo(desert.b).Within(0.01f));
            }
            finally { try { Directory.Delete(layerDir, recursive: true); } catch { } }
        }

        [Test]
        public void WriteVertexColors_NullSource_WritesAllWhite()
        {
            var mesh = BuildFlatMesh(originX: 0f, originZ: 0f, size: 128f, res: 4);
            BiomeVertexColorer.WriteVertexColors(mesh, Vector3.zero, chunkSize: 128f, res: 4, source: null);
            foreach (var c in mesh.colors)
                Assert.AreEqual(Color.white, c);
        }

        private static Mesh BuildFlatMesh(float originX, float originZ, float size, int res)
        {
            var mesh = new Mesh();
            int vertCount = (res + 1) * (res + 1);
            var verts = new Vector3[vertCount];
            float step = size / res;
            for (int z = 0; z <= res; z++)
                for (int x = 0; x <= res; x++)
                    verts[z * (res + 1) + x] = new Vector3(x * step, 0f, z * step);
            mesh.vertices = verts;
            return mesh;
        }

        private static string WriteSingleTile(byte biomeByte)
        {
            return BakedTestFixtures.WriteSyntheticLayer(
                layerId: 0, tileSize: TileSize, cellSize: CellSize,
                originX: 0f, originZ: 0f,
                tiles: new[] { new BakedTestFixtures.TileSpec(0, 0, 100f, biomeByte) });
        }

        private static string WriteTwoTiles(byte byte00, byte byte10)
        {
            return BakedTestFixtures.WriteSyntheticLayer(
                layerId: 0, tileSize: TileSize, cellSize: CellSize,
                originX: 0f, originZ: 0f,
                tiles: new[]
                {
                    new BakedTestFixtures.TileSpec(0, 0, 100f, byte00),
                    new BakedTestFixtures.TileSpec(1, 0, 100f, byte10),
                });
        }
    }
}
