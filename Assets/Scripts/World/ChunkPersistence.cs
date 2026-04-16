// Assets/Scripts/World/ChunkPersistence.cs
using System.IO;
using UnityEngine;

namespace ForeverEngine.Procedural
{
    /// <summary>
    /// Save/load ChunkData to/from disk as JSON files.
    /// Path: {persistentDataPath}/world/{seed}/chunks/cx_{X}_cz_{Z}.json
    /// </summary>
    public static class ChunkPersistence
    {
        private static string RootPath(int seed) =>
            Path.Combine(Application.persistentDataPath, "world", seed.ToString(), "chunks");

        private static string ChunkPath(int seed, ChunkCoord coord) =>
            Path.Combine(RootPath(seed), $"cx_{coord.X}_cz_{coord.Z}.json");

        public static bool Exists(int seed, ChunkCoord coord) =>
            File.Exists(ChunkPath(seed, coord));

        public static void Save(int seed, ChunkCoord coord, ChunkData data)
        {
            string dir = RootPath(seed);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string json = JsonUtility.ToJson(data);
            File.WriteAllText(ChunkPath(seed, coord), json);
        }

        public static ChunkData Load(int seed, ChunkCoord coord)
        {
            string path = ChunkPath(seed, coord);
            if (!File.Exists(path)) return null;

            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<ChunkData>(json);
        }

        public static void Delete(int seed, ChunkCoord coord)
        {
            string path = ChunkPath(seed, coord);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
