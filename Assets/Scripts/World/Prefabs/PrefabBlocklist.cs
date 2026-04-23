using System.Collections.Generic;
using UnityEngine;

namespace ForeverEngine.Procedural
{
    /// <summary>
    /// Prefab GUIDs excluded from the PrefabRegistry because they use shaders
    /// that don't render correctly under URP (typically `Resources/unity_builtin_extra`
    /// built-in materials, or late-imported sample content that escaped URP conversion).
    ///
    /// Populated manually during Phase B URP conversion. Consulted by
    /// PopulatePrefabRegistry.Run() at registry-build time.
    /// </summary>
    [CreateAssetMenu(fileName = "PrefabBlocklist", menuName = "Forever Engine/Prefab Blocklist")]
    public class PrefabBlocklist : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public string Guid;
            public string Reason;
        }

        public List<Entry> Excluded = new();

        public bool Contains(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return false;
            foreach (var e in Excluded)
                if (e.Guid == guid) return true;
            return false;
        }

        private static PrefabBlocklist _cached;
        public static PrefabBlocklist Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<PrefabBlocklist>("PrefabBlocklist");
            return _cached;
        }
    }
}
