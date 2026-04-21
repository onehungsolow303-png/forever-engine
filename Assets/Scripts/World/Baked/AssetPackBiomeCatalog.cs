using System;
using UnityEngine;

namespace ForeverEngine.Procedural
{
    [Serializable]
    public class AssetPackBiomeEntry
    {
        public string PackName;
        public BiomeType[] SuitableBiomes;
        public GameObject[] TreePrefabs;
        public GameObject[] RockPrefabs;
        public GameObject[] BushPrefabs;
        public GameObject[] StructurePrefabs;
        public Material[] TerrainMaterials;
        public AudioClip[] AmbientAudio;
    }

    /// <summary>
    /// Maps imported asset packs to biome suitability. Populated interactively
    /// via `Forever Engine → Bake → Categorize Asset Packs`. Consumed at bake
    /// time by MacroBakeTool and HeroBakeTool.
    /// </summary>
    [CreateAssetMenu(fileName = "AssetPackBiomeCatalog", menuName = "ForeverEngine/Asset Pack Biome Catalog")]
    public class AssetPackBiomeCatalog : ScriptableObject
    {
        public AssetPackBiomeEntry[] Entries = Array.Empty<AssetPackBiomeEntry>();

        public AssetPackBiomeEntry[] GetEntriesForBiome(BiomeType biome)
        {
            if (Entries == null || Entries.Length == 0)
                return Array.Empty<AssetPackBiomeEntry>();

            var matches = new System.Collections.Generic.List<AssetPackBiomeEntry>();
            foreach (var e in Entries)
            {
                if (e.SuitableBiomes == null) continue;
                foreach (var b in e.SuitableBiomes)
                    if (b == biome) { matches.Add(e); break; }
            }
            return matches.ToArray();
        }

        private static AssetPackBiomeCatalog _cached;
        public static AssetPackBiomeCatalog Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<AssetPackBiomeCatalog>("AssetPackBiomeCatalog");
            return _cached;
        }
    }
}
