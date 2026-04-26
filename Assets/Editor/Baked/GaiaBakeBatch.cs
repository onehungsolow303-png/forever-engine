#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ForeverEngine.Procedural.Editor
{
    // Batchmode entry that opens a Gaia-authored scene and runs the macro bake
    // against it. Pairs with GaiaHeadlessPipeline (which writes the scene) and
    // PropSourceSelector (which routes prop placements through GaiaPlacementExtractor
    // when UseGaiaAuthored is true — the default).
    //
    // Invoke:
    //   Unity.exe -batchmode -nographics -projectPath "C:/Dev/Forever engine" \
    //     -executeMethod ForeverEngine.Procedural.Editor.GaiaBakeBatch.BakeConiferousMedium \
    //     -logFile "C:/Dev/gaia-bake.log"
    //
    // Status report written to C:/Dev/gaia-bake-results.txt regardless of outcome.
    public static class GaiaBakeBatch
    {
        private const string StatusPath = "C:/Dev/gaia-bake-results.txt";
        private const string LayerOutputDir = "C:/Dev/.shared/baked/planet/layer_0";

        public static void BakeConiferousMedium() =>
            BakeScene("Assets/Scenes/GaiaWorld_Coniferous_Forest_Medium.unity");

        public static void BakeAlpineMedium() =>
            BakeScene("Assets/Scenes/GaiaWorld_Alpine_Meadow_Medium.unity");

        private static void BakeScene(string scenePath)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var log = new List<string>();
            void L(string m) { Debug.Log($"[GaiaBakeBatch] {m}"); log.Add($"[{sw.Elapsed:mm\\:ss}] {m}"); }

            try
            {
                L($"Force prop source = Gaia-authored.");
                PropSourceSelector.UseGaiaAuthored = true;

                if (!File.Exists(scenePath))
                    throw new InvalidOperationException(
                        $"Scene not found at '{scenePath}'. Run GaiaHeadlessPipeline.BuildXxx first.");

                L($"Open parent scene: {scenePath}");
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                if (!scene.IsValid())
                    throw new InvalidOperationException($"Failed to open scene '{scenePath}'.");

                // Gaia builds with m_autoUnloadScenes = true, so the parent scene
                // saved to disk has no Terrain GameObjects — they live in
                // per-tile scene files under Assets/Gaia User Data/Sessions/<latest>/Terrain Scenes/*.unity.
                // Load every tile scene additively so FindObjectsByType<Terrain>
                // sees the full grid for the bake.
                var tileScenes = FindLatestSessionTerrainScenes();
                L($"Loading {tileScenes.Length} Gaia terrain scene(s) additively.");
                foreach (var ts in tileScenes)
                {
                    L($"  + {ts}");
                    EditorSceneManager.OpenScene(ts, OpenSceneMode.Additive);
                }

                var terrains = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
                L($"Found {terrains.Length} Terrain(s) after additive load.");
                if (terrains.Length == 0)
                    throw new InvalidOperationException("No terrains in opened scene + additives.");

                // Diagnostic — figure out whether Gaia spawned as GameObject children
                // or as TerrainData tree instances (or both). The extractor handles
                // both, but the counts here tell us where to look first if a bake
                // turns up empty.
                int totalChildren = 0;
                int totalTreeInstances = 0;
                int totalDetailPatches = 0;
                foreach (var t in terrains)
                {
                    totalChildren += CountDescendants(t.transform);
                    if (t.terrainData != null)
                    {
                        totalTreeInstances += t.terrainData.treeInstanceCount;
                        var dl = t.terrainData.detailPrototypes;
                        if (dl != null) totalDetailPatches += dl.Length;
                    }
                }
                L($"Terrain content summary: {totalChildren} GO descendants, " +
                  $"{totalTreeInstances} tree instances, {totalDetailPatches} detail prototypes.");

                L("Run MacroBakeTool.BakeAllTilesInSceneOrThrow() — Gaia path via PropSourceSelector.");
                MacroBakeTool.BakeAllTilesInSceneOrThrow();

                var indexPath = Path.Combine(LayerOutputDir, "index.json");
                if (!File.Exists(indexPath))
                    throw new InvalidOperationException($"Bake produced no index.json at {indexPath}");
                L($"index.json: {new FileInfo(indexPath).Length} bytes");

                long totalPropsBytes = 0;
                int tilesWithProps = 0;
                foreach (var p in Directory.GetFiles(LayerOutputDir, "props.bin", SearchOption.AllDirectories))
                {
                    var len = new FileInfo(p).Length;
                    totalPropsBytes += len;
                    if (len > 0) tilesWithProps++;
                    L($"  {p.Replace('\\','/'),-90} {len,10}B");
                }
                L($"Tiles with props.bin: {tilesWithProps}, total props.bin bytes: {totalPropsBytes}");
                if (totalPropsBytes == 0)
                    throw new InvalidOperationException(
                        "All props.bin files are empty. Either Gaia produced no spawned children, " +
                        "or the extractor failed to resolve prefab GUIDs. Inspect the log.");

                sw.Stop();
                L($"SUCCESS in {sw.Elapsed:mm\\:ss}.");
                WriteStatus(true, log);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                sw.Stop();
                log.Add($"[{sw.Elapsed:mm\\:ss}] FAIL: {ex.GetType().Name}: {ex.Message}");
                log.Add(ex.StackTrace ?? "(no stack)");
                WriteStatus(false, log);
                Debug.LogError($"[GaiaBakeBatch] {ex}");
                EditorApplication.Exit(1);
            }
        }

        private static int CountDescendants(Transform t)
        {
            int n = 0;
            foreach (Transform child in t) { n++; n += CountDescendants(child); }
            return n;
        }

        // Finds the most recent Gaia session's Terrain Scenes directory (Gaia
        // names sessions GS-yyyyMMdd - HHmmss).
        //
        // A failed / aborted Gaia run can leave an empty session husk (the
        // GS-* dir + Terrain Scenes/ subdir get created up front, but the
        // tile scenes only get written if creation actually completes). The
        // newest dir on disk is therefore not always the one that holds
        // tile scenes — walk backwards until we find one that does.
        private static string[] FindLatestSessionTerrainScenes()
        {
            const string sessionsRoot = "Assets/Gaia User Data/Sessions";
            if (!Directory.Exists(sessionsRoot)) return System.Array.Empty<string>();

            var sessions = Directory.GetDirectories(sessionsRoot);
            if (sessions.Length == 0) return System.Array.Empty<string>();

            System.Array.Sort(sessions); // GS-yyyymmdd-HHmmss sorts chronologically
            for (int i = sessions.Length - 1; i >= 0; i--)
            {
                var terrainScenesDir = Path.Combine(sessions[i], "Terrain Scenes");
                if (!Directory.Exists(terrainScenesDir)) continue;
                var scenes = Directory.GetFiles(terrainScenesDir, "*.unity");
                if (scenes.Length == 0) continue;

                Debug.Log($"[GaiaBakeBatch] Picked session: {Path.GetFileName(sessions[i])} ({scenes.Length} tile scenes)");
                // Normalize to forward slashes so EditorSceneManager.OpenScene accepts them.
                for (int j = 0; j < scenes.Length; j++)
                    scenes[j] = scenes[j].Replace('\\', '/');
                return scenes;
            }
            return System.Array.Empty<string>();
        }

        private static void WriteStatus(bool success, List<string> lines)
        {
            try
            {
                var status = new List<string>
                {
                    $"Status: {(success ? "SUCCESS" : "FAILED")}",
                    $"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
                    "",
                    "Log:",
                };
                status.AddRange(lines);
                File.WriteAllLines(StatusPath, status);
            }
            catch (Exception ex) { Debug.LogError($"[GaiaBakeBatch] status write failed: {ex.Message}"); }
        }
    }
}
#endif
