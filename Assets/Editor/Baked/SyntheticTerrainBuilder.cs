using UnityEngine;

namespace ForeverEngine.Procedural.Editor
{
    /// <summary>
    /// Builds a Unity Terrain in code for headless test scenarios. NOT used
    /// in real bakes - those start with a Gaia-authored terrain in the editor.
    /// </summary>
    public static class SyntheticTerrainBuilder
    {
        public static GameObject Build(float sizeMeters, int resolutionCells)
        {
            var td = new TerrainData { heightmapResolution = resolutionCells };
            td.size = new Vector3(sizeMeters, 200f, sizeMeters);

            var heights = new float[resolutionCells, resolutionCells];
            for (int z = 0; z < resolutionCells; z++)
                for (int x = 0; x < resolutionCells; x++)
                    heights[z, x] = (float)x / resolutionCells * 0.5f;
            td.SetHeights(0, 0, heights);

            var go = new GameObject("SyntheticTerrain");
            var terrain = go.AddComponent<Terrain>();
            terrain.terrainData = td;
            return go;
        }
    }
}
