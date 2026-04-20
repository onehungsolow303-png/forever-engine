using UnityEngine;

namespace ForeverEngine.Procedural
{
    /// <summary>
    /// Per-chunk GPU-instanced grass renderer. Scatters blades deterministically
    /// by chunk seed, then draws them each frame via Graphics.DrawMeshInstanced.
    /// Skipped when the camera is beyond MaxDrawDistance from chunk center.
    /// Kill-switch: GrassInstancer.Enabled = false to disable globally.
    /// </summary>
    public class GrassInstancer : UnityEngine.MonoBehaviour
    {
        public static bool Enabled = true;

        private Matrix4x4[][] _batches;
        private Mesh _mesh;
        private Material _material;
        private float _maxDrawSqr;
        private Vector3 _chunkCenter;

        /// <summary>
        /// Configure this instancer. Matrices must be in WORLD space. Matrices
        /// are split into ≤1023-sized batches during Setup (DrawMeshInstanced cap).
        /// </summary>
        public void Setup(Mesh mesh, Material material, Matrix4x4[] matrices,
                          Vector3 chunkCenter, float maxDrawDistance)
        {
            _mesh = mesh;
            _material = material;
            _chunkCenter = chunkCenter;
            _maxDrawSqr = maxDrawDistance * maxDrawDistance;
            Debug.Log($"[GRASS-DIAG] Chunk setup: {matrices.Length} instances, mesh={mesh?.name}, mat={material?.name}, supportsInstancing={material != null && material.enableInstancing}");

            const int batchCap = 1023;
            int total = matrices != null ? matrices.Length : 0;
            int batchCount = (total + batchCap - 1) / batchCap;
            _batches = new Matrix4x4[batchCount][];
            for (int b = 0; b < batchCount; b++)
            {
                int start = b * batchCap;
                int size = Mathf.Min(batchCap, total - start);
                var arr = new Matrix4x4[size];
                System.Array.Copy(matrices, start, arr, 0, size);
                _batches[b] = arr;
            }
        }

        private void Update()
        {
            if (!Enabled) return;
            if (_batches == null || _batches.Length == 0) return;
            if (_mesh == null || _material == null) return;

            // No distance gate — a hard threshold made grass pop in/out as the player
            // walked. The chunk itself unloads via ChunkManager.UnloadDistantChunks,
            // which destroys this component along with its parent. GPU frustum culling
            // hides instances that aren't on-screen. _maxDrawSqr is kept for future
            // use (e.g. LOD hopping) but no longer gates rendering.
            // Shadow casting on instanced grass batches flickers at frustum edges
            // because the shadow frustum culls independently of the main view.
            // Off for now — ground shadow on ~3000 thin blades per chunk is also
            // invisible in practice and costs real GPU time.
            for (int i = 0; i < _batches.Length; i++)
                Graphics.DrawMeshInstanced(
                    _mesh, 0, _material, _batches[i], _batches[i].Length,
                    properties: null,
                    castShadows: UnityEngine.Rendering.ShadowCastingMode.Off,
                    receiveShadows: true);
        }
    }
}
