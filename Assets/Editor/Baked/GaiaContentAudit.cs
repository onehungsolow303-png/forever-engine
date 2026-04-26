#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ForeverEngine.Procedural.Editor
{
    /// <summary>
    /// Read-only audit of what Gaia actually populated into the saved Alpine
    /// Meadow scene. Dumps per-terrain layers/trees/details + the BiomePreset
    /// spawner list to /c/tmp/gaia-content-audit.txt so we can see where the
    /// authored asset chain breaks (Gaia-default layers vs real PBR, null
    /// FBX-mat trees, missing tree prototypes, etc.).
    ///
    ///   Unity.exe -batchmode -projectPath "C:/Dev/Forever engine" \
    ///     -executeMethod ForeverEngine.Procedural.Editor.GaiaContentAudit.AuditAlpineMeadow \
    ///     -quit -logFile "C:/tmp/gaia-audit.log"
    /// </summary>
    public static class GaiaContentAudit
    {
        private const string ReportPath = "C:/tmp/gaia-content-audit.txt";

        public static void AuditAlpineMeadow()
        {
            try { AuditScene("Assets/Scenes/GaiaWorld_Alpine_Meadow_Medium.unity"); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogError($"[GaiaAudit] FAIL: {e}"); EditorApplication.Exit(1); }
        }

        private static void AuditScene(string scenePath)
        {
            var sb = new StringBuilder();
            void W(string m) { sb.AppendLine(m); Debug.Log($"[GaiaAudit] {m}"); }

            W($"=== Gaia content audit: {scenePath} ===");
            W($"timestamp: {DateTime.Now:O}");
            W("");

            if (!File.Exists(scenePath))
                throw new InvalidOperationException($"scene not found: {scenePath}");

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var tileScenes = FindLatestSessionTerrainScenes();
            W($"Loading {tileScenes.Length} additive terrain scenes:");
            foreach (var ts in tileScenes)
            {
                W($"  + {ts}");
                EditorSceneManager.OpenScene(ts, OpenSceneMode.Additive);
            }
            W("");

            var terrains = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            W($"=== {terrains.Length} Terrain(s) ===");
            foreach (var t in terrains)
            {
                W($"--- Terrain '{t.name}' @ {t.transform.position} ---");
                var td = t.terrainData;
                if (td == null) { W("  terrainData is NULL"); continue; }

                // TerrainLayers (splat)
                var layers = td.terrainLayers ?? Array.Empty<TerrainLayer>();
                W($"  TerrainLayers: {layers.Length}");
                for (int i = 0; i < layers.Length; i++)
                {
                    var l = layers[i];
                    if (l == null) { W($"    [{i}] <NULL TerrainLayer>"); continue; }
                    var assetPath = AssetDatabase.GetAssetPath(l);
                    var diffPath = l.diffuseTexture != null ? AssetDatabase.GetAssetPath(l.diffuseTexture) : "<no diffuse>";
                    var normalPath = l.normalMapTexture != null ? AssetDatabase.GetAssetPath(l.normalMapTexture) : "<no normal>";
                    bool isGaiaAuto = assetPath != null && assetPath.Contains("Gaia User Data");
                    bool isPackTex  = diffPath != null && (diffPath.Contains("NatureManufacture") || diffPath.Contains("PW_") || diffPath.Contains("Pegasus") || diffPath.Contains("ProceduralWorlds"));
                    W($"    [{i}] {l.name}  (asset={assetPath})");
                    W($"         diffuse={diffPath}  ({Truncate(diffPath, 80)})");
                    W($"         normal={normalPath}");
                    W($"         tileSize={l.tileSize}  GAIA-AUTO={isGaiaAuto}  PACK-TEXTURE={isPackTex}");
                }

                // TreePrototypes
                var trees = td.treePrototypes ?? Array.Empty<TreePrototype>();
                W($"  TreePrototypes: {trees.Length}, treeInstances: {td.treeInstanceCount}");
                for (int i = 0; i < trees.Length; i++)
                {
                    var p = trees[i];
                    var prefab = p.prefab;
                    if (prefab == null) { W($"    [{i}] <NULL prefab>"); continue; }
                    var prefabPath = AssetDatabase.GetAssetPath(prefab);
                    var mr = prefab.GetComponentInChildren<MeshRenderer>();
                    var mf = prefab.GetComponentInChildren<MeshFilter>();
                    int matCount = mr != null ? (mr.sharedMaterials?.Length ?? 0) : 0;
                    int nullMats = 0;
                    var matShaders = new List<string>();
                    if (mr != null && mr.sharedMaterials != null)
                    {
                        foreach (var m in mr.sharedMaterials)
                        {
                            if (m == null) { nullMats++; matShaders.Add("<null>"); }
                            else matShaders.Add(m.shader != null ? m.shader.name : "<null shader>");
                        }
                    }
                    bool meshOk = mf != null && mf.sharedMesh != null;
                    W($"    [{i}] {prefab.name}  ({prefabPath})");
                    W($"         mesh={(meshOk ? mf.sharedMesh.name : "<MISSING>")}  mats={matCount}  null={nullMats}");
                    W($"         shaders=[{string.Join(", ", matShaders)}]");
                }

                // DetailPrototypes (grass)
                var details = td.detailPrototypes ?? Array.Empty<DetailPrototype>();
                W($"  DetailPrototypes: {details.Length}, detailResolution={td.detailResolution}");
                int totalDetailDensity = 0;
                if (details.Length > 0 && td.detailResolution > 0)
                {
                    for (int layer = 0; layer < details.Length; layer++)
                    {
                        var map = td.GetDetailLayer(0, 0, td.detailResolution, td.detailResolution, layer);
                        long sum = 0;
                        for (int y = 0; y < map.GetLength(0); y++)
                            for (int x = 0; x < map.GetLength(1); x++)
                                sum += map[y, x];
                        totalDetailDensity += (int)Math.Min(sum, int.MaxValue);
                        var dp = details[layer];
                        var protoName = dp.prototype != null ? dp.prototype.name : (dp.prototypeTexture != null ? dp.prototypeTexture.name : "<no proto>");
                        W($"    [{layer}] {protoName}  density-sum={sum}");
                    }
                    W($"  total detail density across all layers: {totalDetailDensity}");
                }
            }

            W("");
            W("=== BiomePreset audit: Alpine Meadow ===");
            DumpBiomePreset("Alpine Meadow", W);

            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, sb.ToString());
            Debug.Log($"[GaiaAudit] wrote report to {ReportPath} ({sb.Length} chars)");
        }

        private static void DumpBiomePreset(string biomeNameHint, Action<string> W)
        {
            var guids = AssetDatabase.FindAssets("t:Gaia.BiomePreset");
            W($"  total BiomePreset assets in project: {guids.Length}");
            string targetPath = null;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains(biomeNameHint))
                {
                    targetPath = path;
                    break;
                }
            }
            if (targetPath == null)
            {
                W($"  (no BiomePreset matching '{biomeNameHint}' found — listing all)");
                foreach (var g in guids) W($"    {AssetDatabase.GUIDToAssetPath(g)}");
                return;
            }
            W($"  loading: {targetPath}");

            var so = AssetDatabase.LoadMainAssetAtPath(targetPath);
            if (so == null) { W("  load returned NULL"); return; }
            var serializedObj = new SerializedObject(so);

            var spawnList = serializedObj.FindProperty("m_spawnerPresetList");
            if (spawnList == null) { W("  m_spawnerPresetList property not found"); return; }
            W($"  m_spawnerPresetList: {spawnList.arraySize} entries");
            for (int i = 0; i < spawnList.arraySize; i++)
            {
                var entry = spawnList.GetArrayElementAtIndex(i);
                var settings = entry.FindPropertyRelative("m_spawnerSettings");
                var activeBiome = entry.FindPropertyRelative("m_isActiveInBiome");
                var activeStamper = entry.FindPropertyRelative("m_isActiveInStamper");
                var settingsRef = settings != null ? settings.objectReferenceValue : null;
                var settingsPath = settingsRef != null ? AssetDatabase.GetAssetPath(settingsRef) : "<null>";
                bool isBiomeActive = activeBiome != null && activeBiome.boolValue;
                bool isStamperActive = activeStamper != null && activeStamper.boolValue;
                W($"    [{i}] activeInBiome={isBiomeActive} activeInStamper={isStamperActive}");
                W($"         settings={settingsRef?.name ?? "<null>"} ({settingsPath})");
                if (settingsRef != null) DumpSpawnerSettings(settingsRef, "         ", W);
            }
        }

        private static void DumpSpawnerSettings(UnityEngine.Object spawnerSettings, string indent, Action<string> W)
        {
            var so = new SerializedObject(spawnerSettings);
            var resources = so.FindProperty("m_resources");
            var resRef = resources != null ? resources.objectReferenceValue : null;
            if (resRef == null) { W($"{indent}<no m_resources>"); return; }
            W($"{indent}resources: {resRef.name} ({AssetDatabase.GetAssetPath(resRef)})");

            var resSO = new SerializedObject(resRef);
            DumpProtoArray(resSO, "m_texturePrototypes", "texture",  indent, W);
            DumpProtoArray(resSO, "m_treePrototypes",    "tree",     indent, W);
            DumpProtoArray(resSO, "m_detailPrototypes",  "detail",   indent, W);
            DumpProtoArray(resSO, "m_gameObjectPrototypes", "go",    indent, W);
        }

        private static void DumpProtoArray(SerializedObject resSO, string propName, string label, string indent, Action<string> W)
        {
            var arr = resSO.FindProperty(propName);
            if (arr == null) { W($"{indent}{label} prototypes: <prop missing>"); return; }
            W($"{indent}{label} prototypes: {arr.arraySize}");
            int unresolved = 0;
            int nullMatCount = 0;
            for (int i = 0; i < arr.arraySize; i++)
            {
                var entry = arr.GetArrayElementAtIndex(i);
                var nameProp = entry.FindPropertyRelative("m_name");
                string entryName = nameProp != null ? nameProp.stringValue : "<no name>";

                // Try to find a prefab/texture reference inside the proto
                string firstAssetRef = "<no asset ref>";
                bool hasNullMat = false;
                var iter = entry.Copy();
                var end = iter.GetEndProperty();
                while (iter.NextVisible(true) && !SerializedProperty.EqualContents(iter, end))
                {
                    if (iter.propertyType == SerializedPropertyType.ObjectReference && iter.objectReferenceValue != null)
                    {
                        var p = AssetDatabase.GetAssetPath(iter.objectReferenceValue);
                        if (!string.IsNullOrEmpty(p) && firstAssetRef == "<no asset ref>")
                            firstAssetRef = $"{iter.objectReferenceValue.GetType().Name}:{p}";
                        if (iter.objectReferenceValue is GameObject go)
                        {
                            var mr = go.GetComponentInChildren<MeshRenderer>();
                            if (mr != null && mr.sharedMaterials != null)
                                foreach (var m in mr.sharedMaterials) if (m == null) { hasNullMat = true; break; }
                        }
                    }
                }
                if (firstAssetRef == "<no asset ref>") unresolved++;
                if (hasNullMat) nullMatCount++;
                W($"{indent}  [{i}] {entryName}  →  {Truncate(firstAssetRef, 90)}{(hasNullMat ? "  ⚠ NULL-MAT" : "")}");
            }
            W($"{indent}  ({label}: {unresolved} unresolved, {nullMatCount} with null materials)");
        }

        // Mirrors GaiaBakeBatch.FindLatestSessionTerrainScenes — additive load
        // of the per-tile scenes that Gaia writes alongside the parent scene.
        private static string[] FindLatestSessionTerrainScenes()
        {
            var sessionsRoot = "Assets/Gaia User Data/Sessions";
            if (!Directory.Exists(sessionsRoot)) return Array.Empty<string>();
            var dirs = Directory.GetDirectories(sessionsRoot);
            if (dirs.Length == 0) return Array.Empty<string>();
            Array.Sort(dirs, StringComparer.Ordinal);
            var latest = dirs[dirs.Length - 1];
            var terrainDir = Path.Combine(latest, "Terrain Scenes");
            if (!Directory.Exists(terrainDir)) return Array.Empty<string>();
            var scenes = Directory.GetFiles(terrainDir, "*.unity", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/'))
                .ToArray();
            return scenes;
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : "..." + s.Substring(s.Length - max + 3);
        }
    }
}
#endif
