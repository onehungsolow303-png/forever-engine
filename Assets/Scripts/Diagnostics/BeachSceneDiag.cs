using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ForeverEngine.Diagnostics
{
    /// <summary>
    /// Dumps the loaded scene's contents to %APPDATA%/../LocalLow/.../beach-scene-diag.txt
    /// 3 seconds after scene load. No keyboard input required — runs automatically in builds.
    /// </summary>
    public static class BeachSceneDiag
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("BeachSceneDiag");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<BeachSceneDiagBehaviour>();
        }
    }

    public class BeachSceneDiagBehaviour : UnityEngine.MonoBehaviour
    {
        private float _t;
        private bool _dumped;

        private void Update()
        {
            if (_dumped) return;
            _t += Time.unscaledDeltaTime;
            if (_t < 3f) return;
            _dumped = true;
            Dump();
        }

        private void Dump()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== BeachSceneDiag at t={Time.realtimeSinceStartup:F1}s ===");
            sb.AppendLine($"Active scene: {SceneManager.GetActiveScene().name} (path={SceneManager.GetActiveScene().path}, loaded={SceneManager.GetActiveScene().isLoaded}, GOs={SceneManager.GetActiveScene().rootCount})");
            sb.AppendLine($"Sea level (RenderSettings.skybox? {RenderSettings.skybox != null})");

            // Cameras
            var cams = Camera.allCameras;
            sb.AppendLine($"\n--- Cameras ({cams.Length}) ---");
            foreach (var c in cams)
                sb.AppendLine($"  {c.name}: pos={c.transform.position}  rot={c.transform.rotation.eulerAngles}  fov={c.fieldOfView}  clearFlags={c.clearFlags}  bg={c.backgroundColor}");

            // Terrains
            var terrains = Terrain.activeTerrains;
            sb.AppendLine($"\n--- Terrains ({terrains.Length}) ---");
            foreach (var t in terrains)
            {
                sb.AppendLine($"  {t.name}: pos={t.transform.position}  size={t.terrainData?.size}  hmRes={t.terrainData?.heightmapResolution}  layers={t.terrainData?.terrainLayers?.Length}  treeProtos={t.terrainData?.treePrototypes?.Length}  treeInstances={t.terrainData?.treeInstances?.Length}");
            }

            // Top-level GOs in active scene
            sb.AppendLine($"\n--- Active scene root GameObjects ---");
            var root = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var g in root)
            {
                sb.AppendLine($"  {g.name}: active={g.activeInHierarchy}  pos={g.transform.position}  children={g.transform.childCount}");
            }

            // Renderers count + bounds
            var rends = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            sb.AppendLine($"\n--- Renderers ({rends.Length}) ---");
            int visibleCount = 0;
            Bounds combined = rends.Length > 0 ? rends[0].bounds : default;
            foreach (var r in rends)
            {
                if (r.enabled && r.gameObject.activeInHierarchy) { visibleCount++; combined.Encapsulate(r.bounds); }
            }
            sb.AppendLine($"  visible: {visibleCount}");
            sb.AppendLine($"  combined bounds: center={combined.center}  size={combined.size}");

            // Lights
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            sb.AppendLine($"\n--- Lights ({lights.Length}) ---");
            foreach (var l in lights)
                sb.AppendLine($"  {l.name}: type={l.type}  intensity={l.intensity}  color={l.color}");

            string outDir = Path.Combine(Application.persistentDataPath);
            string outPath = Path.Combine(outDir, "beach-scene-diag.txt");
            try
            {
                Directory.CreateDirectory(outDir);
                File.WriteAllText(outPath, sb.ToString());
                Debug.Log($"[BeachSceneDiag] Dumped {rends.Length} renderers / {root.Length} root GOs / {terrains.Length} terrains to {outPath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BeachSceneDiag] Write failed: {ex.Message}");
            }
        }
    }
}
