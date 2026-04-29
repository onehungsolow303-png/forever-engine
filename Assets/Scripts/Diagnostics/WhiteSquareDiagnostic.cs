using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ForeverEngine.Diagnostics
{
    /// <summary>
    /// Runtime diagnostic that finds the actual source of white squares.
    /// Auto-spawns on scene load, presses F8 to dump full renderer inventory,
    /// presses F9 to raycast from screen center and identify what's there.
    ///
    /// Output: %APPDATA%/../LocalLow/DefaultCompany/Forever engin/whitesquare-diag.txt
    /// Removes itself / disables when done — non-invasive.
    /// </summary>
    public sealed class WhiteSquareDiagnostic : UnityEngine.MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("WhiteSquareDiagnostic");
            DontDestroyOnLoad(go);
            go.AddComponent<WhiteSquareDiagnostic>();
            Debug.Log("[WS-DIAG] Bootstrapped. Press F8 to dump full renderer inventory; F9 to raycast forward.");
        }

        private string _outPath;
        private float _autoDumpAt;

        private void Awake()
        {
            _outPath = Path.Combine(Application.persistentDataPath, "whitesquare-diag.txt");
            _autoDumpAt = Time.time + 30f;
            Debug.Log($"[WS-DIAG] Output path: {_outPath}");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8) || (Time.time >= _autoDumpAt && _autoDumpAt > 0f))
            {
                _autoDumpAt = -1f;
                DumpAllRenderers();
            }
            if (Input.GetKeyDown(KeyCode.F9))
            {
                RaycastInspect();
            }
        }

        private void DumpAllRenderers()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== WhiteSquareDiagnostic dump @ {System.DateTime.Now:HH:mm:ss} ===");
            sb.AppendLine($"Player position: {(Camera.main != null ? Camera.main.transform.position.ToString("F1") : "no camera")}");
            sb.AppendLine();

            var allRenderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            sb.AppendLine($"Total active Renderers in scene: {allRenderers.Length}");
            sb.AppendLine();

            // Bucket by suspicion: white-square fingerprint
            var whiteCandidates = new List<(Renderer r, string reason)>();
            var particleSystems = new List<Renderer>();
            var terrainDetails = new List<string>();

            foreach (var r in allRenderers)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;

                if (r is ParticleSystemRenderer)
                {
                    particleSystems.Add(r);
                }

                var mat = r.sharedMaterial;
                if (mat == null) continue;

                // White-square fingerprint:
                // - opaque (or null surface)
                // - white-ish base color
                // - null/missing main texture
                bool isOpaque = !mat.HasFloat("_Surface") || mat.GetFloat("_Surface") == 0f;
                bool baseMapNull = false;
                if (mat.HasTexture("_BaseMap")) baseMapNull = mat.GetTexture("_BaseMap") == null;
                else if (mat.HasTexture("_MainTex")) baseMapNull = mat.GetTexture("_MainTex") == null;

                Color baseColor = Color.white;
                if (mat.HasColor("_BaseColor")) baseColor = mat.GetColor("_BaseColor");
                else if (mat.HasColor("_Color")) baseColor = mat.GetColor("_Color");
                bool isWhiteish = baseColor.r > 0.8f && baseColor.g > 0.8f && baseColor.b > 0.8f;

                if (baseMapNull && isWhiteish && (isOpaque || baseColor.a > 0.05f))
                {
                    whiteCandidates.Add((r, $"baseMapNull={baseMapNull} whiteish={isWhiteish} opaque={isOpaque} alpha={baseColor.a:F2}"));
                }
            }

            sb.AppendLine($"White-square candidates ({whiteCandidates.Count}):");
            foreach (var (r, reason) in whiteCandidates)
            {
                var go = r.gameObject;
                var path = GetPath(go.transform);
                var pos = r.bounds.center;
                var size = r.bounds.size;
                var matName = r.sharedMaterial.name;
                var shaderName = r.sharedMaterial.shader != null ? r.sharedMaterial.shader.name : "null";
                sb.AppendLine($"  [{r.GetType().Name}] {path}");
                sb.AppendLine($"    pos=({pos.x:F1},{pos.y:F1},{pos.z:F1}) size=({size.x:F2},{size.y:F2},{size.z:F2})");
                sb.AppendLine($"    mat='{matName}' shader='{shaderName}'");
                sb.AppendLine($"    {reason}");
                sb.AppendLine();
            }

            // Active terrains and their detail prototypes
            sb.AppendLine();
            sb.AppendLine($"=== Active Terrains ({Terrain.activeTerrains.Length}) ===");
            foreach (var t in Terrain.activeTerrains)
            {
                if (t == null || t.terrainData == null) continue;
                sb.AppendLine($"Terrain '{t.name}' at {t.transform.position}");
                var td = t.terrainData;
                sb.AppendLine($"  detailPrototypes: {td.detailPrototypes.Length}");
                for (int i = 0; i < td.detailPrototypes.Length; i++)
                {
                    var dp = td.detailPrototypes[i];
                    var protoMat = dp.prototype != null ? "prefab=" + dp.prototype.name : (dp.prototypeTexture != null ? "tex=" + dp.prototypeTexture.name : "<null>");
                    sb.AppendLine($"    [{i}] render={dp.renderMode} {protoMat} healthyColor={dp.healthyColor} dryColor={dp.dryColor}");
                }
                sb.AppendLine($"  treePrototypes: {td.treePrototypes.Length}");
                for (int i = 0; i < td.treePrototypes.Length; i++)
                {
                    var tp = td.treePrototypes[i];
                    var name = tp.prefab != null ? tp.prefab.name : "<null>";
                    sb.AppendLine($"    [{i}] prefab={name}");
                }
            }

            sb.AppendLine();
            sb.AppendLine($"=== ParticleSystemRenderers ({particleSystems.Count}) ===");
            foreach (var r in particleSystems)
            {
                var go = r.gameObject;
                var path = GetPath(go.transform);
                var matName = r.sharedMaterial != null ? r.sharedMaterial.name : "null";
                var shaderName = r.sharedMaterial != null && r.sharedMaterial.shader != null ? r.sharedMaterial.shader.name : "null";
                sb.AppendLine($"  {path}  mat='{matName}' shader='{shaderName}'");
            }

            File.WriteAllText(_outPath, sb.ToString());
            Debug.Log($"[WS-DIAG] Dumped {whiteCandidates.Count} white-square candidates to {_outPath}");
            Debug.Log($"[WS-DIAG] {allRenderers.Length} total renderers, {Terrain.activeTerrains.Length} terrains, {particleSystems.Count} particle systems.");
        }

        private void RaycastInspect()
        {
            var cam = Camera.main;
            if (cam == null) { Debug.LogWarning("[WS-DIAG] No main camera."); return; }
            var ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
            if (Physics.Raycast(ray, out var hit, 5000f))
            {
                var path = GetPath(hit.collider.transform);
                Debug.Log($"[WS-DIAG] Crosshair hit: {path} at {hit.point}");
                var r = hit.collider.GetComponent<Renderer>();
                if (r != null && r.sharedMaterial != null)
                {
                    Debug.Log($"[WS-DIAG]   mat='{r.sharedMaterial.name}' shader='{r.sharedMaterial.shader?.name}'");
                }
            }
            else
            {
                Debug.Log("[WS-DIAG] Crosshair raycast missed.");
            }
        }

        private static string GetPath(Transform t)
        {
            var sb = new StringBuilder(t.name);
            while (t.parent != null)
            {
                t = t.parent;
                sb.Insert(0, t.name + "/");
            }
            return sb.ToString();
        }
    }
}
