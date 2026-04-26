using System.Collections.Generic;
using UnityEngine;

namespace ForeverEngine.Procedural
{
    /// <summary>
    /// Per-prefab "how far above pivot is the bottom of my visible geometry."
    ///
    /// Rationale: asset pack prefabs don't agree on a single pivot convention.
    /// NatureManufacture trees pivot at trunk base (bounds.min.y ≈ 0). NatureManufacture
    /// rocks pivot at the geometric center of the rock (bounds.min.y ≈ -radius). Placing
    /// every prefab with `transform.position.y = terrainY` works for trees and buries
    /// half of every rock.
    ///
    /// Correct rule: after setting scale/rotation, the lowest point of the instantiated
    /// geometry should sit on terrainY. We compute `baseOffset = -localBounds.min.y` in
    /// the prefab's local frame (i.e. how much to shift the pivot UP so the bottom lands
    /// at Y=0). Renderer places the instance at `terrainY + baseOffset * uniformScale`.
    ///
    /// Results are cached per-prefab; bounds computation is slow (enumerates all child
    /// MeshFilters) but each prefab is queried once per client session.
    /// </summary>
    public static class PrefabBaseOffset
    {
        private static readonly Dictionary<int, float> _cache = new();

        /// <summary>
        /// Returns the distance from the prefab's pivot down to the bottom of its
        /// visible geometry, in the prefab's local frame (no rotation/scale applied).
        /// Multiply by uniform scale at the call site.
        ///
        /// Returns 0 if the prefab has no mesh renderers or the bounds collapse to a
        /// point — effectively disables the offset for unknown-shape prefabs. Safe.
        /// </summary>
        public static float Compute(GameObject prefab)
        {
            if (prefab == null) return 0f;
            int id = prefab.GetInstanceID();
            if (_cache.TryGetValue(id, out var cached)) return cached;

            var bounds = ComputeLocalBounds(prefab);
            float offset = bounds.HasValue ? -bounds.Value.min.y : 0f;
            // Never push things ABOVE the pivot — negative offsets mean the prefab's
            // bottom is above the pivot anyway (weirdly-authored prefab). Clamp to 0
            // so we don't accidentally float things.
            if (offset < 0f) offset = 0f;
            _cache[id] = offset;
            return offset;
        }

        private static Bounds? ComputeLocalBounds(GameObject prefab)
        {
            var filters = prefab.GetComponentsInChildren<MeshFilter>(includeInactive: true);
            if (filters == null || filters.Length == 0) return null;

            bool any = false;
            Bounds combined = default;
            foreach (var mf in filters)
            {
                if (mf == null || mf.sharedMesh == null) continue;
                var meshBounds = mf.sharedMesh.bounds;
                // Transform mesh-local bounds into prefab-local space. Walk the chain
                // from this MeshFilter up to the prefab root so child transforms
                // (offsets + rotations) are honored.
                var local = TransformBoundsToPrefabLocal(meshBounds, mf.transform, prefab.transform);
                if (!any) { combined = local; any = true; }
                else combined.Encapsulate(local);
            }
            return any ? combined : (Bounds?)null;
        }

        private static Bounds TransformBoundsToPrefabLocal(Bounds meshBounds, Transform from, Transform root)
        {
            // Build matrix from `from` up to `root`. We walk up the chain so this works
            // for nested child meshes.
            var matrix = Matrix4x4.identity;
            var t = from;
            while (t != null && t != root)
            {
                matrix = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale) * matrix;
                t = t.parent;
            }

            // Transform the 8 corners of meshBounds by matrix and rebuild a world-aligned
            // Bounds around the transformed corners.
            var c = meshBounds.center;
            var e = meshBounds.extents;
            var corners = new Vector3[8];
            corners[0] = matrix.MultiplyPoint3x4(c + new Vector3(-e.x, -e.y, -e.z));
            corners[1] = matrix.MultiplyPoint3x4(c + new Vector3( e.x, -e.y, -e.z));
            corners[2] = matrix.MultiplyPoint3x4(c + new Vector3(-e.x,  e.y, -e.z));
            corners[3] = matrix.MultiplyPoint3x4(c + new Vector3( e.x,  e.y, -e.z));
            corners[4] = matrix.MultiplyPoint3x4(c + new Vector3(-e.x, -e.y,  e.z));
            corners[5] = matrix.MultiplyPoint3x4(c + new Vector3( e.x, -e.y,  e.z));
            corners[6] = matrix.MultiplyPoint3x4(c + new Vector3(-e.x,  e.y,  e.z));
            corners[7] = matrix.MultiplyPoint3x4(c + new Vector3( e.x,  e.y,  e.z));

            var result = new Bounds(corners[0], Vector3.zero);
            for (int i = 1; i < 8; i++) result.Encapsulate(corners[i]);
            return result;
        }

        /// <summary>Test-only helper; clears the prefab→offset cache.</summary>
        public static void ClearCache_ForTest() => _cache.Clear();
    }
}
