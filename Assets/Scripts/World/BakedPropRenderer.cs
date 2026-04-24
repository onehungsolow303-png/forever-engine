using ForeverEngine.Core.World.Baked;
using UnityEngine;

namespace ForeverEngine.Procedural
{
    /// <summary>
    /// Instantiates baked prop placements at their exact authored world positions.
    /// Replaces runtime procedural placement (SurfaceDecorator.DecorateFromCatalog)
    /// for biomes with a baked tile — positions are Gaia-physics-settled at author
    /// time so no placement heuristics are needed.
    /// </summary>
    public static class BakedPropRenderer
    {
        private static bool _loggedMissingRegistry;
        private static readonly System.Collections.Generic.HashSet<string> _loggedMissingGuids = new();

        public static void Render(
            BakedPropPlacement[] placements,
            Transform parent,
            PrefabRegistry registry = null)
        {
            if (placements == null || placements.Length == 0) return;

            registry = registry != null ? registry : PrefabRegistry.Instance;
            if (registry == null)
            {
                if (!_loggedMissingRegistry)
                {
                    Debug.LogWarning("[BakedPropRenderer] No PrefabRegistry at Resources/PrefabRegistry — " +
                                     "run Forever Engine → Populate Prefab Registry. Baked props will NOT render.");
                    _loggedMissingRegistry = true;
                }
                return;
            }

            for (int i = 0; i < placements.Length; i++)
            {
                var p = placements[i];
                var prefab = registry.Resolve(p.PrefabGuid);
                PrefabRegistryValidator.RecordLookup(p.PrefabGuid, prefab != null);
                if (prefab == null)
                {
                    if (_loggedMissingGuids.Add(p.PrefabGuid))
                    {
                        Debug.LogWarning(
                            $"[BakedPropRenderer] GUID {p.PrefabGuid} ({p.PrefabResourcePath}) " +
                            $"not in PrefabRegistry. Skipping — repopulate the registry after adding new packs.");
                    }
                    continue;
                }

                var go = Object.Instantiate(prefab, parent);
                go.transform.position = new Vector3(p.WorldX, p.WorldY, p.WorldZ);
                go.transform.rotation = Quaternion.Euler(0f, p.YawDegrees, 0f);
                go.transform.localScale = Vector3.one * p.UniformScale;

                // Strip colliders — props are pass-through in our game (same
                // rationale as SurfaceDecorator.DecorateFromCatalog). Pick the
                // destroy flavor by context: Destroy (deferred, preferred) at
                // runtime; DestroyImmediate in EditMode tests (Destroy is
                // a no-op there and Unity logs an error that fails the runner).
                bool isPlaying = Application.isPlaying;
                foreach (var col in go.GetComponentsInChildren<Collider>(includeInactive: true))
                {
                    if (isPlaying) Object.Destroy(col);
                    else           Object.DestroyImmediate(col);
                }
            }
        }
    }
}
