using System;
using UnityEngine;

namespace ForeverEngine.Procedural
{
    [Serializable]
    public class BiomeMaterialEntry
    {
        public BiomeType Biome;
        public Material Material;
    }

    /// <summary>
    /// Per-biome terrain materials. Loaded at runtime via Resources.Load;
    /// populated by the `Forever Engine → Populate Biome Material Catalog`
    /// editor menu. TerrainGenerator.GetBiomeMaterial queries this first
    /// and falls back to procedural solid-color when a biome is absent.
    /// </summary>
    [CreateAssetMenu(fileName = "BiomeMaterialCatalog", menuName = "ForeverEngine/Biome Material Catalog")]
    public class BiomeMaterialCatalog : ScriptableObject
    {
        public BiomeMaterialEntry[] Entries = Array.Empty<BiomeMaterialEntry>();

        /// <summary>Returns the material for <paramref name="biome"/> or null if unassigned.</summary>
        public Material GetMaterial(BiomeType biome)
        {
            if (Entries == null) return null;
            for (int i = 0; i < Entries.Length; i++)
                if (Entries[i].Biome == biome)
                    return Entries[i].Material;
            return null;
        }

        private static BiomeMaterialCatalog _cached;

        /// <summary>
        /// Loads the catalog from `Assets/Resources/BiomeMaterialCatalog.asset`.
        /// Returns null if no asset exists (fallback path applies).
        /// </summary>
        public static BiomeMaterialCatalog Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<BiomeMaterialCatalog>("BiomeMaterialCatalog");
            return _cached;
        }
    }
}
