using System.Collections.Generic;
using UnityEngine;

namespace ForeverEngine.Procedural
{
    /// <summary>
    /// Runtime-resolvable map from BakedLayerIndex GUIDs (SplatLayerGuids,
    /// TreePrototypeGuids) to actual TerrainLayer + prefab references. Built
    /// at bake-time by BakedAssetRegistryPopulator (editor); consumed at
    /// runtime by the chunk renderers. Lives in Resources/ so a standalone
    /// build can find it via Resources.Load.
    /// </summary>
    [CreateAssetMenu(fileName = "BakedAssetRegistry", menuName = "Forever Engine/Baked Asset Registry")]
    public sealed class BakedAssetRegistry : ScriptableObject
    {
        [System.Serializable]
        public struct TerrainLayerEntry
        {
            public string Guid;
            public TerrainLayer Layer;
        }

        [System.Serializable]
        public struct PrefabEntry
        {
            public string Guid;
            public GameObject Prefab;
        }

        public TerrainLayerEntry[] TerrainLayers = System.Array.Empty<TerrainLayerEntry>();
        public PrefabEntry[] TreePrefabs = System.Array.Empty<PrefabEntry>();

        private Dictionary<string, TerrainLayer> _layerCache;
        private Dictionary<string, GameObject> _prefabCache;

        public TerrainLayer ResolveTerrainLayer(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            EnsureCaches();
            return _layerCache.TryGetValue(guid, out var l) ? l : null;
        }

        public GameObject ResolveTreePrefab(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            EnsureCaches();
            return _prefabCache.TryGetValue(guid, out var p) ? p : null;
        }

        private void EnsureCaches()
        {
            if (_layerCache != null && _prefabCache != null) return;
            _layerCache = new Dictionary<string, TerrainLayer>(TerrainLayers.Length);
            foreach (var e in TerrainLayers)
                if (!string.IsNullOrEmpty(e.Guid) && e.Layer != null)
                    _layerCache[e.Guid] = e.Layer;
            _prefabCache = new Dictionary<string, GameObject>(TreePrefabs.Length);
            foreach (var e in TreePrefabs)
                if (!string.IsNullOrEmpty(e.Guid) && e.Prefab != null)
                    _prefabCache[e.Guid] = e.Prefab;
        }

        private static BakedAssetRegistry _cached;
        private static bool _loadAttempted;

        public static BakedAssetRegistry Load()
        {
            if (_loadAttempted) return _cached;
            _loadAttempted = true;
            _cached = Resources.Load<BakedAssetRegistry>("BakedAssetRegistry");
            if (_cached == null)
                Debug.LogWarning("[BakedAssetRegistry] not found at Resources/BakedAssetRegistry. " +
                                 "Run Forever Engine → Bake → Populate Asset Registry.");
            return _cached;
        }
    }
}
