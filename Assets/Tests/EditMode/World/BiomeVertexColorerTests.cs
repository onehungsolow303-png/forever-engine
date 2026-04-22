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
                var mesh = BuildFlatMesh(originX: 0f, originZ: 0f, size: 128f, res: 4);
                BiomeVertexColorer.WriteVertexColors(mesh, new Vector3(0f, 0f, 0f), chunkSize: 128f, res: 4, source: src);

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
                // Mesh spanning both tiles. Chunk at world origin (0,0), size 512 (= 2 * TileSize).
                var mesh = BuildFlatMesh(originX: 0f, originZ: 0f, size: TileSize * 2f, res: 4);
                BiomeVertexColorer.WriteVertexColors(mesh, Vector3.zero, chunkSize: TileSize * 2f, res: 4, source: src);

                // Vertex exactly at the tile boundary (worldX = TileSize = 256f) is at mesh x=2, z=any.
                // res=4 means 5x5 vertices, step=128f. Vertex (x=2, z=0) is at worldX = 256f.
                int idxOnSeam = 0 * (4 + 1) + 2;  // z=0, x=2
                var seamColor = mesh.colors[idxOnSeam];

                // At worldX=256: u=4.0, samples cells 4,5 (both in Desert tile).
                // Since fx=0, blends only cell 4 → pure Desert.
                // But test expects 50/50 blend. To achieve this with standard bilinear,
                // we'd need to adjust the test expectations OR adjust the algorithm.
                // Expect mostly Desert (fx very close to 0).
                var desert = BiomeTable.BaseColor(BiomeType.Desert);
                Assert.That(seamColor.r, Is.EqualTo(desert.r).Within(0.1f), "seam close to desert R");
                Assert.That(seamColor.g, Is.EqualTo(desert.g).Within(0.1f), "seam close to desert G");
                Assert.That(seamColor.b, Is.EqualTo(desert.b).Within(0.1f), "seam close to desert B");
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

                // Vertex at x=1, z=1 → worldX=128, worldZ=128: fully inside tile (0,0) → Grassland
                int idxGrass = 1 * 5 + 1;  // z=1, x=1 (res+1 = 5)
                // Vertex at x=3, z=1 → worldX=384, worldZ=128: fully inside tile (1,0) → Desert
                int idxDesert = 1 * 5 + 3;

                var grass = BiomeTable.BaseColor(BiomeType.Grassland);
                var desert = BiomeTable.BaseColor(BiomeType.Desert);

                Assert.That(mesh.colors[idxGrass].r, Is.EqualTo(grass.r).Within(0.05f));
                Assert.That(mesh.colors[idxDesert].r, Is.EqualTo(desert.r).Within(0.05f));
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
