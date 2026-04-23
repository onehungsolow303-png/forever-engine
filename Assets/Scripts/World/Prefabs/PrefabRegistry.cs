using System.Collections.Generic;
using UnityEngine;

namespace ForeverEngine.Procedural
{
    /// <summary>
    /// Runtime-accessible prefab lookup keyed by Asset GUID.
    /// Populated at editor time by "Forever Engine → Populate Prefab Registry";
    /// loaded at runtime via Resources.Load from Resources/PrefabRegistry.
    /// Required because Unity's AssetDatabase is editor-only — baked prop files
    /// reference prefabs by GUID, and the runtime needs a resolver.
    /// </summary>
    [CreateAssetMenu(menuName = "Forever Engine/Prefab Registry", fileName = "PrefabRegistry")]
    public class PrefabRegistry : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public string Guid;
            public GameObject Prefab;
        }

        [SerializeField] private List<Entry> _entries = new();

        private Dictionary<string, GameObject> _cache;

        private static PrefabRegistry _singleton;
        public static PrefabRegistry Instance
        {
            get
            {
                if (_singleton == null)
                    _singleton = Resources.Load<PrefabRegistry>("PrefabRegistry");
                return _singleton;
            }
        }

        public GameObject Resolve(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            if (_cache == null)
            {
                _cache = new Dictionary<string, GameObject>(_entries.Count);
                foreach (var e in _entries)
                    if (!string.IsNullOrEmpty(e.Guid) && e.Prefab != null)
                        _cache[e.Guid] = e.Prefab;
            }
            return _cache.TryGetValue(guid, out var prefab) ? prefab : null;
        }

        /// <summary>
        /// Editor-only API used by the populator. Public so editor scripts in a
        /// different assembly can call it.
        /// </summary>
        public void SetEntries_Editor(IEnumerable<Entry> entries)
        {
            _entries = new List<Entry>(entries);
            _cache = null;
        }

        public int Count => _entries.Count;
    }
}
