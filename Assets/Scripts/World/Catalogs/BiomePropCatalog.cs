using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForeverEngine.Procedural
{
    [Serializable]
    public class BiomePropRule
    {
        public BiomeType Biome;
        public GameObject[] Prefabs = Array.Empty<GameObject>();
        public int Count = 10;
        public float MinSpacing = 20f;
        public float BaseScale = 1f;
    }

    /// <summary>
    /// Per-biome prop prefabs for SurfaceDecorator. Loaded via Resources.Load.
    /// A biome with no rules falls back to the procedural primitives in
    /// SurfaceDecorator.GetBiomeRules.
    /// </summary>
    [CreateAssetMenu(fileName = "BiomePropCatalog", menuName = "ForeverEngine/Biome Prop Catalog")]
    public class BiomePropCatalog : ScriptableObject
    {
        public BiomePropRule[] Rules = Array.Empty<BiomePropRule>();

        /// <summary>Returns every rule across all biomes. Used by editor populators.</summary>
        public IEnumerable<BiomePropRule> GetAllRules() => Rules ?? Array.Empty<BiomePropRule>();

        /// <summary>Returns all rules for the given biome (may be empty).</summary>
        public BiomePropRule[] GetRules(BiomeType biome)
        {
            if (Rules == null) return Array.Empty<BiomePropRule>();
            var matches = new List<BiomePropRule>();
            for (int i = 0; i < Rules.Length; i++)
                if (Rules[i].Biome == biome)
                    matches.Add(Rules[i]);
            return matches.ToArray();
        }

        private static BiomePropCatalog _cached;

        public static BiomePropCatalog Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<BiomePropCatalog>("BiomePropCatalog");
            return _cached;
        }
    }
}
