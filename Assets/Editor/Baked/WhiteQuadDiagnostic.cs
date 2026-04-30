#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ForeverEngine.Procedural.Editor
{
    /// <summary>
    /// Walks every Renderer + every Terrain.treePrototypes/detailPrototypes in the
    /// GaiaWorld scene and dumps their material/shader/texture state. Used to
    /// identify which materials are responsible for the "white quad" rendering
    /// that persisted after Bug #52/#53 fixes.
    ///
    /// Output: C:/tmp/white-quad-diag.txt — sorted by suspect score.
    /// </summary>
    public static class WhiteQuadDiagnostic
    {
        private const string ScenePath = "Assets/Scenes/GaiaWorld_Coniferous_Forest_Medium.unity";
        private const string OutputPath = "C:/tmp/white-quad-diag.txt";

        // Camera position from CaptureAssetVarietyScreenshots.cs shot 01 (front-eye-level)
        private static readonly Vector3 CamPos = new Vector3(0, 264, -52);

        public static void Run()
        {
            Debug.Log("[WhiteQuadDiag] === starting ===");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid()) throw new System.Exception($"Couldn't open {ScenePath}");

            var lines = new List<string>();
            lines.Add($"WhiteQuadDiagnostic — {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            lines.Add($"Scene: {ScenePath}");
            lines.Add("");

            // ── Section 1: scene-GO Renderers ─────────────────────────────────
            lines.Add("=== Section 1: scene Renderers (sorted by suspect score) ===");
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var rendererSuspects = new List<(int score, string line)>();
            int totalRenderers = 0, suspectsCount = 0;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                totalRenderers++;
                foreach (var mat in r.sharedMaterials ?? new Material[0])
                {
                    if (mat == null)
                    {
                        rendererSuspects.Add((100, $"  [NULL_MAT]      {Path(r.gameObject)}"));
                        suspectsCount++;
                        continue;
                    }
                    var diag = MaterialDiag(mat);
                    if (diag.score == 0) continue;
                    rendererSuspects.Add((diag.score, $"  [{diag.score:D3}]            {Path(r.gameObject)}  pos={r.transform.position}  bounds={r.bounds.size}\n           mat={mat.name}  shader={mat.shader?.name ?? "NULL"}  {diag.summary}"));
                    suspectsCount++;
                }
            }
            lines.Add($"Total Renderers: {totalRenderers}, suspects: {suspectsCount}");
            lines.Add("");
            foreach (var (_, l) in rendererSuspects.OrderByDescending(t => t.score).Take(80))
                lines.Add(l);

            // ── Section 2: terrain treePrototypes ─────────────────────────────
            lines.Add("");
            lines.Add("=== Section 2: Terrain.treePrototypes ===");
            int totalTrees = 0, treeSuspects = 0;
            foreach (var t in Terrain.activeTerrains)
            {
                if (t == null || t.terrainData == null) continue;
                lines.Add($"Terrain: {t.name}, treeInstanceCount={t.terrainData.treeInstanceCount}");
                var prototypes = t.terrainData.treePrototypes;
                for (int i = 0; i < prototypes.Length; i++)
                {
                    var p = prototypes[i];
                    if (p?.prefab == null)
                    {
                        lines.Add($"  [{i}] NULL prefab — likely renders as white billboard");
                        treeSuspects++;
                        continue;
                    }
                    totalTrees++;
                    var renderersInPrefab = p.prefab.GetComponentsInChildren<Renderer>(true);
                    foreach (var rr in renderersInPrefab)
                    {
                        foreach (var mm in rr.sharedMaterials ?? new Material[0])
                        {
                            if (mm == null)
                            {
                                lines.Add($"  [{i}] {p.prefab.name} — NULL material in {rr.gameObject.name}");
                                treeSuspects++;
                                continue;
                            }
                            var d = MaterialDiag(mm);
                            if (d.score == 0) continue;
                            lines.Add($"  [{i}] {p.prefab.name} — {rr.gameObject.name}.{mm.name}  shader={mm.shader?.name}  {d.summary}");
                            treeSuspects++;
                        }
                    }
                }
            }
            lines.Add($"Tree prototype suspects: {treeSuspects}");

            File.WriteAllLines(OutputPath, lines);
            Debug.Log($"[WhiteQuadDiag] wrote {lines.Count} lines to {OutputPath}");
            Debug.Log($"[WhiteQuadDiag] scene Renderer suspects: {suspectsCount}/{totalRenderers}");
            Debug.Log($"[WhiteQuadDiag] tree prototype suspects: {treeSuspects}");
        }

        private static string Path(GameObject go)
        {
            var stack = new List<string>();
            var t = go.transform;
            while (t != null) { stack.Add(t.name); t = t.parent; }
            stack.Reverse();
            return string.Join("/", stack);
        }

        private static (int score, string summary) MaterialDiag(Material mat)
        {
            if (mat == null) return (0, "");
            int score = 0;
            var summary = new List<string>();

            // Bug #52 fingerprint: Surface=Opaque + null _BaseMap
            bool isOpaque = false;
            try { isOpaque = mat.HasProperty("_Surface") && Mathf.Approximately(mat.GetFloat("_Surface"), 0f); } catch { }
            bool hasBaseMap = mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != null;
            bool hasMainTex = mat.HasProperty("_MainTex") && mat.GetTexture("_MainTex") != null;
            Color baseColor = (mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor")
                              : mat.HasProperty("_Color") ? mat.GetColor("_Color")
                              : Color.black);

            if (!hasBaseMap && !hasMainTex)
            {
                summary.Add("NO_TEX");
                score += 50;
            }
            if (isOpaque && !hasBaseMap && !hasMainTex)
            {
                summary.Add("BUG52_FINGERPRINT");
                score += 50;
            }
            if (baseColor.r > 0.9f && baseColor.g > 0.9f && baseColor.b > 0.9f)
            {
                summary.Add($"WHITE_TINT(r={baseColor.r:F2},g={baseColor.g:F2},b={baseColor.b:F2})");
                score += 30;
            }
            if (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
            {
                summary.Add("MAGENTA_SHADER");
                score += 100;
            }

            return (score, string.Join(" ", summary));
        }
    }
}
#endif
