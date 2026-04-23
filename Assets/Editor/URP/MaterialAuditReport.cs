#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using ForeverEngine.Procedural;

namespace ForeverEngine.Editor.URP
{
    /// <summary>
    /// Menu: Forever Engine → URP → Material Audit Report.
    /// Walks every material referenced by AssetPackBiomeCatalog (via prefabs in
    /// Tree/Rock/Bush/Structure arrays) plus PrefabRegistry, buckets by shader
    /// name, writes Assets/Editor/URP/MaterialAuditReport.md.
    ///
    /// Used as pre/post-conversion diagnostic for Phase B URP migration.
    /// Batchmode: Unity.exe -batchmode -nographics -quit
    ///   -executeMethod ForeverEngine.Editor.URP.MaterialAuditReport.Run -logFile -
    /// </summary>
    public static class MaterialAuditReport
    {
        private const string ReportPath = "Assets/Editor/URP/MaterialAuditReport.md";
        private const string CatalogPath = "Assets/Resources/AssetPackBiomeCatalog.asset";
        private const string BiomePropCatalogPath = "Assets/Resources/BiomePropCatalog.asset";

        private struct MatRef
        {
            public Material Mat;
            public string PrefabPath;
            public string PackName;
        }

        [MenuItem("Forever Engine/URP/Material Audit Report")]
        public static void Run()
        {
            var refs = CollectMaterials();
            if (refs.Count == 0)
            {
                Debug.LogError("[MaterialAuditReport] No materials found. Catalog or registry may be empty.");
                return;
            }

            var byShader = new Dictionary<string, List<MatRef>>();
            foreach (var r in refs)
            {
                var shader = r.Mat != null && r.Mat.shader != null ? r.Mat.shader.name : "<null>";
                if (!byShader.TryGetValue(shader, out var list))
                {
                    list = new List<MatRef>();
                    byShader[shader] = list;
                }
                list.Add(r);
            }

            WriteReport(byShader, refs.Count);
            Debug.Log($"[MaterialAuditReport] Wrote {ReportPath} ({refs.Count} mat-refs, {byShader.Count} distinct shaders).");
        }

        private static List<MatRef> CollectMaterials()
        {
            var result = new List<MatRef>();
            var seen = new HashSet<int>();

            var catalog = AssetDatabase.LoadAssetAtPath<AssetPackBiomeCatalog>(CatalogPath);
            if (catalog != null && catalog.Entries != null)
            {
                foreach (var entry in catalog.Entries)
                {
                    if (entry == null) continue;
                    CollectFromPrefabs(result, seen, entry.TreePrefabs, entry.PackName);
                    CollectFromPrefabs(result, seen, entry.RockPrefabs, entry.PackName);
                    CollectFromPrefabs(result, seen, entry.BushPrefabs, entry.PackName);
                    CollectFromPrefabs(result, seen, entry.StructurePrefabs, entry.PackName);
                    if (entry.TerrainMaterials != null)
                    {
                        foreach (var mat in entry.TerrainMaterials)
                        {
                            if (mat == null) continue;
                            if (!seen.Add(mat.GetInstanceID())) continue;
                            result.Add(new MatRef { Mat = mat, PrefabPath = "(terrain)", PackName = entry.PackName });
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[MaterialAuditReport] AssetPackBiomeCatalog not found at {CatalogPath}.");
            }

            var bpc = AssetDatabase.LoadAssetAtPath<BiomePropCatalog>(BiomePropCatalogPath);
            if (bpc != null)
            {
                foreach (var rule in bpc.GetAllRules())
                {
                    if (rule.Prefabs == null) continue;
                    foreach (var p in rule.Prefabs)
                    {
                        if (p == null) continue;
                        CollectFromPrefab(result, seen, p, "(BiomePropCatalog)");
                    }
                }
            }

            return result;
        }

        private static void CollectFromPrefabs(List<MatRef> result, HashSet<int> seen, GameObject[] prefabs, string packName)
        {
            if (prefabs == null) return;
            foreach (var p in prefabs)
            {
                if (p == null) continue;
                CollectFromPrefab(result, seen, p, packName);
            }
        }

        private static void CollectFromPrefab(List<MatRef> result, HashSet<int> seen, GameObject prefab, string packName)
        {
            var path = AssetDatabase.GetAssetPath(prefab);
            foreach (var mr in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr == null || mr.sharedMaterials == null) continue;
                foreach (var mat in mr.sharedMaterials)
                {
                    if (mat == null) continue;
                    if (!seen.Add(mat.GetInstanceID())) continue;
                    result.Add(new MatRef { Mat = mat, PrefabPath = path, PackName = packName });
                }
            }
            foreach (var smr in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == null || smr.sharedMaterials == null) continue;
                foreach (var mat in smr.sharedMaterials)
                {
                    if (mat == null) continue;
                    if (!seen.Add(mat.GetInstanceID())) continue;
                    result.Add(new MatRef { Mat = mat, PrefabPath = path, PackName = packName });
                }
            }
        }

        private static void WriteReport(Dictionary<string, List<MatRef>> byShader, int total)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Material Audit Report");
            sb.AppendLine();
            sb.AppendLine($"**Generated:** {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"**Total unique materials:** {total}");
            sb.AppendLine($"**Distinct shaders:** {byShader.Count}");
            sb.AppendLine();

            sb.AppendLine("## Bucketed summary");
            sb.AppendLine();
            int urpCount = 0, standardCount = 0, hdrpCount = 0, customCount = 0, otherCount = 0;
            foreach (var kv in byShader)
            {
                var s = kv.Key;
                int n = kv.Value.Count;
                if (s.StartsWith("Universal Render Pipeline/")) urpCount += n;
                else if (s.StartsWith("Shader Graphs/")) urpCount += n;
                else if (s == "Standard" || s == "Standard (Specular setup)" || s.StartsWith("Legacy Shaders/") || s.StartsWith("Mobile/") || s.StartsWith("Particles/") || s.StartsWith("Nature/") || s.StartsWith("Unlit/")) standardCount += n;
                else if (s.StartsWith("HDRP/") || s.StartsWith("HDRenderPipeline/")) hdrpCount += n;
                else if (s.StartsWith("Custom/") || s.StartsWith("NatureManufacture/") || s.StartsWith("CDT/")) customCount += n;
                else otherCount += n;
            }
            sb.AppendLine($"- **URP / ShaderGraphs (healthy):** {urpCount}");
            sb.AppendLine($"- **Built-in Standard/Legacy (needs conversion):** {standardCount}");
            sb.AppendLine($"- **HDRP (needs conversion):** {hdrpCount}");
            sb.AppendLine($"- **Custom (leave alone — see 486-shader incident):** {customCount}");
            sb.AppendLine($"- **Other/unknown:** {otherCount}");
            sb.AppendLine();

            sb.AppendLine("## Per-shader detail");
            sb.AppendLine();
            foreach (var kv in byShader.OrderByDescending(p => p.Value.Count))
            {
                sb.AppendLine($"### `{kv.Key}` — {kv.Value.Count} material(s)");
                sb.AppendLine();
                int examples = 0;
                var byPack = kv.Value.GroupBy(r => string.IsNullOrEmpty(r.PackName) ? "(unknown)" : r.PackName).OrderByDescending(g => g.Count());
                foreach (var g in byPack)
                {
                    sb.AppendLine($"- **{g.Key}** — {g.Count()} mats");
                    foreach (var r in g.Take(3))
                    {
                        var matPath = AssetDatabase.GetAssetPath(r.Mat);
                        sb.AppendLine($"  - `{matPath}` (in `{r.PrefabPath}`)");
                        examples++;
                        if (examples >= 15) break;
                    }
                    if (examples >= 15) break;
                }
                sb.AppendLine();
            }

            var dir = Path.GetDirectoryName(ReportPath);
            if (!AssetDatabase.IsValidFolder(dir))
            {
                var parent = Path.GetDirectoryName(dir);
                var leaf = Path.GetFileName(dir);
                if (!AssetDatabase.IsValidFolder(parent))
                    AssetDatabase.CreateFolder(Path.GetDirectoryName(parent), Path.GetFileName(parent));
                AssetDatabase.CreateFolder(parent, leaf);
            }

            File.WriteAllText(ReportPath, sb.ToString());
            AssetDatabase.ImportAsset(ReportPath);
        }
    }
}
#endif
