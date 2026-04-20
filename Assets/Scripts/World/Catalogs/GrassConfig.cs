using UnityEngine;

namespace ForeverEngine.Procedural
{
    /// <summary>
    /// Per-project grass rendering config. Single mesh + material applied via
    /// Graphics.DrawMeshInstanced per chunk. Populated by
    /// `Forever Engine → Populate Grass Config` editor menu.
    /// </summary>
    [CreateAssetMenu(fileName = "GrassConfig", menuName = "ForeverEngine/Grass Config")]
    public class GrassConfig : ScriptableObject
    {
        public Mesh GrassMesh;
        public Material GrassMaterial;

        [Header("Scatter")]
        public int CountPerChunk = 3000;
        public float MinSpacing = 1f;
        public float BaseScale = 2f;

        [Header("Draw")]
        public float MaxDrawDistance = 100f;

        private static GrassConfig _cached;
        public static GrassConfig Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<GrassConfig>("GrassConfig");
            return _cached;
        }
    }
}
