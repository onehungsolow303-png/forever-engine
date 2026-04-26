#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using ForeverEngine.Core.World.Baked;
using UnityEditor;
using UnityEngine;

namespace ForeverEngine.Procedural.Editor
{
    // Walks a Gaia-authored Terrain's descendant prefab instances and emits a
    // BakedPropPlacement per instance. Replaces PropPlacementSampler as the
    // bake-time content source. Props are already physics-settled by Gaia's
    // spawn rules, so the GO transform is the truth — no elevation re-sample,
    // no slope filter. Organizational containers Gaia inserts (one wrapper per
    // spawner) are descended through; only nodes that resolve to a prefab GUID
    // become placements.
    public static class GaiaPlacementExtractor
    {
        public static BakedPropPlacement[] Extract(
            GameObject terrainGO,
            Func<GameObject, string> resolvePrefabGuid = null)
        {
            if (terrainGO == null) throw new ArgumentNullException(nameof(terrainGO));

            resolvePrefabGuid ??= DefaultGuidResolver;

            var results = new List<BakedPropPlacement>();

            // 1. TerrainData.treeInstances — Gaia's primary spawn path. High-density
            //    trees / rocks / props that Gaia paints onto the terrain via
            //    Unity's tree instance system rather than as discrete GameObjects.
            //    These are the bulk of a Gaia biome's content.
            var terrain = terrainGO.GetComponent<Terrain>();
            if (terrain != null && terrain.terrainData != null)
                ExtractTerrainTreeInstances(terrain, resolvePrefabGuid, results);

            // 2. GameObject descendants — any Spawner that targets the GO hierarchy
            //    instead of the tree-instance system, or POIs that spawn as
            //    discrete prefab instances.
            foreach (Transform child in terrainGO.transform)
                Visit(child, resolvePrefabGuid, results);

            return results.ToArray();
        }

        private static void ExtractTerrainTreeInstances(
            Terrain terrain,
            Func<GameObject, string> resolveGuid,
            List<BakedPropPlacement> results)
        {
            var data = terrain.terrainData;
            var prototypes = data.treePrototypes;
            if (prototypes == null || prototypes.Length == 0) return;

            // Resolve prototype prefab → GUID once per prototype, not per instance.
            var protoGuids = new string[prototypes.Length];
            var protoPaths = new string[prototypes.Length];
            for (int i = 0; i < prototypes.Length; i++)
            {
                var prefab = prototypes[i].prefab;
                if (prefab == null) { protoGuids[i] = null; protoPaths[i] = string.Empty; continue; }
                protoGuids[i] = resolveGuid(prefab);
                protoPaths[i] = AssetDatabase.GetAssetPath(prefab) ?? string.Empty;
            }

            var size = data.size;
            var origin = terrain.transform.position;
            var instances = data.treeInstances;
            for (int i = 0; i < instances.Length; i++)
            {
                var inst = instances[i];
                if (inst.prototypeIndex < 0 || inst.prototypeIndex >= protoGuids.Length) continue;
                var guid = protoGuids[inst.prototypeIndex];
                if (string.IsNullOrEmpty(guid)) continue;

                // TreeInstance.position is normalized [0..1] over terrain size.
                float wx = origin.x + inst.position.x * size.x;
                float wz = origin.z + inst.position.z * size.z;
                float wy = origin.y + inst.position.y * size.y;
                float yaw = inst.rotation * Mathf.Rad2Deg;
                // Unity's TreeInstance has separate width/height scale; for our
                // wire format (uniform scale), average them.
                float scale = 0.5f * (inst.widthScale + inst.heightScale);

                results.Add(new BakedPropPlacement(
                    PrefabGuid: guid,
                    PrefabResourcePath: protoPaths[inst.prototypeIndex],
                    WorldX: wx, WorldY: wy, WorldZ: wz,
                    YawDegrees: yaw,
                    UniformScale: scale));
            }
        }

        private static void Visit(
            Transform t,
            Func<GameObject, string> resolveGuid,
            List<BakedPropPlacement> results)
        {
            string guid = resolveGuid(t.gameObject);
            if (!string.IsNullOrEmpty(guid))
            {
                string path = ResolvePrefabPath(t.gameObject);
                float yaw = t.rotation.eulerAngles.y;
                float scale = t.localScale.x;
                results.Add(new BakedPropPlacement(
                    PrefabGuid: guid,
                    PrefabResourcePath: path,
                    WorldX: t.position.x, WorldY: t.position.y, WorldZ: t.position.z,
                    YawDegrees: yaw,
                    UniformScale: scale));
                return;
            }

            foreach (Transform grandchild in t)
                Visit(grandchild, resolveGuid, results);
        }

        private static string DefaultGuidResolver(GameObject go)
        {
            var src = PrefabUtility.GetCorrespondingObjectFromSource(go);
            if (src == null) return null;
            string path = AssetDatabase.GetAssetPath(src);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.AssetPathToGUID(path);
        }

        private static string ResolvePrefabPath(GameObject go)
        {
            var src = PrefabUtility.GetCorrespondingObjectFromSource(go);
            return src != null ? AssetDatabase.GetAssetPath(src) : string.Empty;
        }
    }
}
#endif
