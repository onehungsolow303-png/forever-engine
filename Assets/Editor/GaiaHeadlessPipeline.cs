#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gaia;
using ProceduralWorlds.GTS;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_2022_2_OR_NEWER
using UnityEditor.Rendering.Universal;
#endif

namespace ForeverEngine.Editor.Gaia
{
    /// <summary>
    /// End-to-end Gaia world creation in one batchmode-friendly entry point.
    /// No menu clicks required.
    ///
    /// Invocation:
    ///   Unity.exe -batchmode -projectPath "C:/Dev/Forever engine" \
    ///     -executeMethod ForeverEngine.Editor.Gaia.GaiaHeadlessPipeline.BuildConiferousMedium \
    ///     -logFile "C:/Dev/gaia-headless.log"
    ///
    /// Pipeline:
    ///   1. New empty scene, saved to Assets/Scenes/GaiaWorld_<Biome>_<Size>.unity
    ///   2. Clean any stray broken Terrain objects (null terrainData)
    ///   3. Load BiomePreset + build WorldCreationSettings, call GaiaAPI.CreateGaiaWorld
    ///   4. Poll m_worldCreationRunning via EditorApplication.update until done
    ///      (coroutine completes in the background — deliberately do not -quit
    ///      on entry, because the coroutine needs editor update ticks to run)
    ///   5. Once creation is done, convert Built-In → URP materials, run
    ///      NatureManufactureMatFixer, respawn all Gaia spawners, save, Exit(0)
    ///
    /// Failure modes that call EditorApplication.Exit(1):
    ///   - BiomePreset asset missing
    ///   - CreateGaiaWorld returns false (scene save rejected, settings null)
    ///   - Timeout (20 minutes) without completion
    ///   - Any exception during post-processing
    /// </summary>
    public static class GaiaHeadlessPipeline
    {
        private const string BiomesDir =
            "Assets/Procedural Worlds/Packages - Install/Gaia Pro Assets and Biomes/Biomes";
        private const double TimeoutSeconds = 20 * 60;

        // Captured at start-of-run so EditorApplication.update can read them.
        // One pipeline runs per Unity launch (EditorApplication.Exit terminates the process);
        // do not invoke two pipeline entry points in the same session — they share these fields.
        private static string _biomeName;
        private static string _sizeName;
        private static string _scenePath;
        private static DateTime _startedAt;

        // ── Entry points for -executeMethod ─────────────────────────────────

        public static void BuildConiferousSmall() =>
            RunAsync("Coniferous Forest", GaiaWorldBuilder.WorldSize.Small);

        public static void BuildConiferousMedium() =>
            RunAsync("Coniferous Forest", GaiaWorldBuilder.WorldSize.Medium);

        public static void BuildConiferousLarge() =>
            RunAsync("Coniferous Forest", GaiaWorldBuilder.WorldSize.Large);

        public static void BuildAlpineMedium() =>
            RunAsync("Alpine Meadow", GaiaWorldBuilder.WorldSize.Medium);

        public static void BuildGiantMedium() =>
            RunAsync("Giant Forest", GaiaWorldBuilder.WorldSize.Medium);

        // ── Test bake: Desert Beach Cave (no BiomePreset, hand-curated spawners) ───

        // Root-above-layers — MacroBakeTool.BakeTerrainsAsTiles appends `layer_{layerId}`
        // itself (line 98 of MacroBakeTool.cs), so this constant must NOT include layer_0.
        private const string TestBakeOutputRoot =
            "C:/Dev/.shared/baked/test/desert_beach_cave";

        public static void BuildDesertBeachCave() => RunDesertBeachCave();

        private static void RunDesertBeachCave()
        {
            try
            {
                _biomeName = "DesertBeachCave";
                _sizeName = "Test1x1";
                _startedAt = DateTime.UtcNow;

                Log("=== Step 1/9: new empty scene ===");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Directory.CreateDirectory("Assets/Scenes");
                _scenePath = "Assets/Scenes/GaiaWorld_DesertBeachCave.unity";
                EditorSceneManager.SaveScene(scene, _scenePath);
                Log($"  scene saved at {_scenePath}");

                Log("=== Step 1.5/9: setup basic lighting (sun + skybox + ambient) ===");
                SetupBasicLighting();

                Log("=== Step 2/9: clean any broken Terrains (null terrainData) ===");
                CleanBrokenTerrains();

                Log("=== Step 2.5/9: URP convert + matfixer (BEFORE spawn) ===");
                RunBuiltInToUrpConverter();
                RunNatureManufactureMatFixer();

                Log("=== Step 3/9: create 1x1 km terrain (no biome preset) ===");
                EnsureSceneViewExists();
                var settings = BuildDesertBeachCaveSettings();
                if (!GaiaSessionManager.CreateOrUpdateWorld(settings, executeNow: true, isUpdate: false))
                    throw new Exception("CreateOrUpdateWorld returned false");
                Log("  world-creation coroutine started. Driving it to completion...");
                DriveGaiaCoroutineToCompletion();

                ApplyDesertBeachCaveStamps();

                ApplyDesertBeachCaveSplats();

                SpawnDesertBeachCaveContent();

                PlaceDesertBeachCaveStructure();

                SetupCrestWater();

                Log("=== Step 8.5/9: post-processing (clean + culling settings) ===");
                CleanBrokenTerrains();
                ApplyTerrainCullingSettings();

                Log("=== Step 9/9: save scene ===");
                EditorSceneManager.SaveOpenScenes();

                Log($"=== DONE in {(DateTime.UtcNow - _startedAt).TotalSeconds:F1}s. Scene: {_scenePath} ===");
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GaiaHeadless] DesertBeachCave FAIL: {ex}");
                EditorApplication.Exit(1);
            }
        }

        private static WorldCreationSettings BuildDesertBeachCaveSettings()
        {
            var s = ScriptableObject.CreateInstance<WorldCreationSettings>();
            s.m_qualityPreset = GaiaConstants.EnvironmentTarget.Desktop;
            s.m_seaLevel = 50;                         // matches existing Coniferous convention
            s.m_autoSpawnBiome = false;                // we drive spawners manually
            s.m_centerOffset = Vector2.zero;
            s.m_dateTimeString = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            // Empty spawner list — Gaia.CreateOrUpdateWorld tolerates this (no biome
            // instantiation runs). Subsequent tasks construct spawners in code rather
            // than via a BiomePreset preset list.
            s.m_spawnerPresetList = new List<BiomeSpawnerListEntry>();

            s.m_targetSizePreset = GaiaConstants.EnvironmentSizePreset.Custom;
            s.m_xTiles = 1; s.m_zTiles = 1;
            s.m_tileSize = 1024; s.m_tileHeight = 800;
            s.m_createInScene = false; s.m_autoUnloadScenes = false;
            s.m_applyFloatingPointFix = false;

            return s;
        }

        // StamperSettings semantics (verified against Gaia source 2026-04-29):
        //   m_x/m_y/m_z (double) — world position; ALSO sync transform.position because
        //     Stamper.Stamp() reads transform.position at line 870 (not m_settings.m_x).
        //   m_width (float) — XZ footprint as PERCENTAGE of terrain (0-100). Stamps are
        //     square in XZ; 100 = full terrain XZ coverage. Bounds.size.x = m_width *
        //     terrainData.size.x / 100 per Stamper.cs:870.
        //   m_height (float) — visualization only (gizmo localScale Y). Does NOT govern
        //     stamp Y amplitude — that comes from the stamp's grayscale data + m_baseLevel.
        //   m_baseLevel (float) — floor Y the stamp lifts from.
        //   m_operation (GaiaConstants.FeatureOperation) — Raise / Add / Blend / etc.

        private static void ApplyStamp(
            string stampQuery,
            double worldX, double worldY, double worldZ,
            float widthPercent,
            float baseLevelY,
            GaiaConstants.FeatureOperation op,
            string label)
        {
            Log($"  applying stamp '{label}' at ({worldX},{worldY},{worldZ}) width={widthPercent}% baseY={baseLevelY} op={op}");

            var guids = AssetDatabase.FindAssets($"{stampQuery} t:Texture2D");
            var candidates = new List<string>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("Packages - Install/Stamps"))
                    candidates.Add(path);
            }
            if (candidates.Count > 1)
                Debug.LogWarning($"[GaiaHeadless] ApplyStamp: {candidates.Count} candidates for '{stampQuery}'; using first: {candidates[0]}");
            Texture2D stampTex = candidates.Count > 0
                ? AssetDatabase.LoadAssetAtPath<Texture2D>(candidates[0])
                : null;
            if (stampTex == null)
                throw new Exception($"Stamp not found by query '{stampQuery}'. Verify filename in Procedural Worlds/Packages - Install/Stamps/.");
            Log($"    resolved to {candidates[0]}");

            var go = new GameObject($"Stamper_{label}");
            var stamper = go.AddComponent<Stamper>();
            var settings = ScriptableObject.CreateInstance<StamperSettings>();
            stamper.m_settings = settings;
            stamper.m_stampImage = stampTex;
            settings.m_x = worldX; settings.m_y = worldY; settings.m_z = worldZ;
            settings.m_width = widthPercent;
            settings.m_height = 5f;
            settings.m_baseLevel = baseLevelY;
            settings.m_rotation = 0f;
            settings.m_operation = op;
            go.transform.position = new Vector3((float)worldX, (float)worldY, (float)worldZ);

            try
            {
                stamper.Stamp();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
            Log($"    stamp '{label}' applied");
        }

        private static void ApplyDesertBeachCaveStamps()
        {
            Log("=== Step 4/9: apply 3 stamps (Plains, Mountains, Hills) ===");

            ApplyStamp("Plains", 512, 45, 512, widthPercent: 100, baseLevelY: 45,
                       GaiaConstants.FeatureOperation.BlendHeight, "Plains_Baseline");

            ApplyStamp("Mountain", 900, 50, 512, widthPercent: 50, baseLevelY: 50,
                       GaiaConstants.FeatureOperation.RaiseHeight, "Mountain_EastCliff");

            ApplyStamp("Hills", 575, 53, 512, widthPercent: 40, baseLevelY: 53,
                       GaiaConstants.FeatureOperation.AddHeight, "Hills_Dunes");
        }

        private static TerrainLayer LoadOrCreateLayer(string name, string textureGuidOrName)
        {
            // Try to find an existing TerrainLayer with this name
            var layerGuids = AssetDatabase.FindAssets($"{name} t:TerrainLayer");
            foreach (var guid in layerGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var existing = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                if (existing != null) return existing;
            }
            // Otherwise create one referencing the texture
            var texGuids = AssetDatabase.FindAssets(textureGuidOrName + " t:Texture2D");
            Texture2D albedo = null;
            foreach (var guid in texGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (albedo != null)
                {
                    Log($"  LoadOrCreateLayer '{name}': resolved texture at {path}");
                    break;
                }
            }
            if (albedo == null)
                throw new Exception($"Could not resolve texture for layer '{name}' (query='{textureGuidOrName}')");

            var layer = new TerrainLayer
            {
                diffuseTexture = albedo,
                tileSize = new Vector2(8, 8),
                name = name,
            };
            Directory.CreateDirectory("Assets/Procedural Worlds/_GeneratedLayers");
            var layerPath = $"Assets/Procedural Worlds/_GeneratedLayers/{name}.terrainlayer";
            AssetDatabase.CreateAsset(layer, layerPath);
            AssetDatabase.SaveAssets();
            Log($"  LoadOrCreateLayer '{name}': created new TerrainLayer at {layerPath}");
            return layer;
        }

        private static void ApplyDesertBeachCaveSplats()
        {
            Log("=== Step 5/9: paint sand + rock splats via direct alphamap write ===");

            var sand = LoadOrCreateLayer("DBC_Sand", "sand");
            var rock = LoadOrCreateLayer("DBC_Rock", "rock");

            var terrains = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            if (terrains.Length == 0) throw new Exception("No terrains in scene");

            foreach (var t in terrains)
            {
                t.terrainData.terrainLayers = new[] { sand, rock };
                var data = t.terrainData;
                int aw = data.alphamapWidth, ah = data.alphamapHeight;
                var alpha = new float[ah, aw, 2];      // 2 layers — note Unity convention is [y, x, layer]

                int hmRes = data.heightmapResolution;
                // Sample slope + height at each alphamap cell
                for (int y = 0; y < ah; y++)
                for (int x = 0; x < aw; x++)
                {
                    float nx = (float)x / (aw - 1);
                    float nz = (float)y / (ah - 1);
                    // Clamp the heightmap index to avoid off-by-one at the boundary
                    int hx = Mathf.Clamp((int)(nx * (hmRes - 1)), 0, hmRes - 1);
                    int hz = Mathf.Clamp((int)(nz * (hmRes - 1)), 0, hmRes - 1);
                    float worldY = data.GetHeight(hx, hz);
                    float slopeDeg = data.GetSteepness(nx, nz);

                    float sandWeight = (slopeDeg < 25f && worldY >= 45 && worldY <= 65) ? 1f : 0f;
                    float rockWeight = (slopeDeg > 30f || worldY > 80) ? 1f : 0f;
                    // If both 0, default to sand (low-elevation flat fallback).
                    if (sandWeight + rockWeight <= 0f) sandWeight = 1f;
                    float total = sandWeight + rockWeight;
                    alpha[y, x, 0] = sandWeight / total;
                    alpha[y, x, 1] = rockWeight / total;
                }

                data.SetAlphamaps(0, 0, alpha);
                Log($"  splatted terrain '{t.name}' ({aw}x{ah} alphamap)");
            }
        }

        // ── Pipeline ────────────────────────────────────────────────────────

        private static void RunAsync(string biomeName, GaiaWorldBuilder.WorldSize size)
        {
            try
            {
                _biomeName = biomeName;
                _sizeName = size.ToString();
                _startedAt = DateTime.UtcNow;

                Log($"=== Step 1/5: new empty scene for biome='{biomeName}' size={size} ===");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Directory.CreateDirectory("Assets/Scenes");
                _scenePath = $"Assets/Scenes/GaiaWorld_{biomeName.Replace(' ', '_')}_{size}.unity";
                EditorSceneManager.SaveScene(scene, _scenePath);
                Log($"  scene saved at {_scenePath}");

                Log("=== Step 1.5/7: setup basic lighting (sun + skybox + ambient) ===");
                SetupBasicLighting();

                Log("=== Step 2/7: clean any broken Terrains (null terrainData) ===");
                CleanBrokenTerrains();

                // URP convert + NatureManufacture matfixer must run BEFORE
                // Gaia tries to instance trees. Otherwise the world-creation
                // coroutine's stamper-active spawners hit "The tree X
                // couldn't be instanced because one of the materials is
                // missing" (Built-in / NM shaders not loaded in this URP
                // project) and produce 0 tree instances. The converter
                // operates on the project, not on Gaia output, so it's safe
                // to run before any Gaia work.
                Log("=== Step 2.5/7: URP convert + matfixer (BEFORE spawn) ===");
                RunBuiltInToUrpConverter();
                RunNatureManufactureMatFixer();

                Log("=== Step 3/7: load biome + kick off GaiaAPI.CreateGaiaWorld ===");
                var biome = AssetDatabase.LoadAssetAtPath<BiomePreset>($"{BiomesDir}/{biomeName}.asset");
                if (biome == null)
                    throw new Exception($"Missing BiomePreset {biomeName} under {BiomesDir}");

                // In batchmode there is no SceneView; Gaia's ExecuteCreateWorld
                // calls SceneView.lastActiveSceneView.camera to position the
                // editor view on the new world and NREs when null, killing the
                // coroutine BEFORE it saves the scene / flips
                // m_worldCreationRunning. Spawning a hidden SceneView here
                // satisfies that call site.
                EnsureSceneViewExists();

                var settings = BuildSettings(biome, size);
                if (!GaiaSessionManager.CreateOrUpdateWorld(settings, executeNow: true, isUpdate: false))
                    throw new Exception("GaiaSessionManager.CreateOrUpdateWorld returned false");

                // Gaia drives its coroutine via EditorApplication.update —
                // unreliable in batchmode (ticks are sparse / non-existent
                // between -executeMethod calls). Instead, synchronously drive
                // m_updateOperationCoroutine.MoveNext() ourselves in a blocking
                // loop until Gaia clears the coroutine reference. This matches
                // what Gaia's own EditorUpdate does, just without the frame
                // dependency.
                Log("  world-creation coroutine started. Driving it to completion...");
                DriveGaiaCoroutineToCompletion();

                // Step 3.5: apply procedural heightmaps. Gaia's CreateGaiaWorld
                // makes flat terrains by default (no stamps in WorldCreationSettings).
                // Without topography, biome spawn rules with slope/height image-masks
                // find no valid positions → 0 trees, 0 rocks placed (diagnosed
                // 2026-04-25: spawners exit after 9 MoveNext = "nothing to do").
                // Multi-octave Perlin gives natural-looking hills + valleys that
                // the spawn rules can populate against.
                Log("=== Step 3.5/7: apply procedural heightmaps (multi-octave Perlin) ===");
                ApplyProceduralHeightmaps();

                // Step 3 only creates the terrain tiles. The full biome
                // (textures, trees, rocks, grass) needs BiomePreset.CreateBiome
                // which instantiates all BiomeController child Spawner objects;
                // then Spawner.Spawn paints each onto the now-stamped terrain.
                Log("=== Step 4/6: instantiate biome spawners + spawn all ===");
                InstantiateAndSpawnBiome(biome);

                Log("=== Step 5/7: post-processing (clean + culling settings) ===");
                CleanBrokenTerrains();
                ApplyTerrainCullingSettings();

                Log("=== Step 5.5/7: apply PWSky scene infrastructure ===");
                ApplyPWSky();

                // The macro bake must run BEFORE saving + exit. Gaia is configured
                // with m_autoUnloadScenes=true (so the parent scene stays small)
                // which means terrain GOs only live in memory during this run.
                // Reading them after a fresh editor open requires loading per-tile
                // session scenes additively — fragile. Baking now skips that.
                Log("=== Step 6/7: macro bake (Gaia path via PropSourceSelector) ===");
                RunMacroBake();

                Log("=== Step 6.5/7: GTS post-bake setup ===");
                ApplyGTSToBakedTerrains();

                Log("=== Step 7/7: save scene ===");
                EditorSceneManager.SaveOpenScenes();

                Log($"=== DONE in {(DateTime.UtcNow - _startedAt).TotalSeconds:F1}s. Scene: {_scenePath} ===");
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GaiaHeadless] FAIL: {ex}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Drives GaiaSessionManager.m_updateOperationCoroutine to completion
        /// synchronously. In batchmode Gaia's own EditorUpdate tick rate is
        /// too slow / irregular to progress the coroutine, so we MoveNext()
        /// ourselves. Matches Gaia's internal EditorUpdate behavior at
        /// GaiaSessionManager.cs:908–914 minus the 100-tick debounce.
        /// </summary>
        private static void DriveGaiaCoroutineToCompletion()
        {
            var sm = GaiaSessionManager.GetSessionManager(
                pickupExistingTerrain: false, createSession: false);
            if (sm == null)
                throw new Exception("No GaiaSessionManager in scene after CreateOrUpdateWorld.");

            var maxSteps = 10_000_000;   // safety limit; a world takes ~1e5 ticks
            int steps = 0;
            int lastLogPct = -1;
            while (sm.m_updateOperationCoroutine != null)
            {
                steps++;
                if ((DateTime.UtcNow - _startedAt).TotalSeconds > TimeoutSeconds)
                    throw new Exception($"TIMEOUT after {TimeoutSeconds}s.");

                try
                {
                    if (!sm.m_updateOperationCoroutine.MoveNext())
                    {
                        // Coroutine finished.
                        sm.m_updateOperationCoroutine = null;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception(
                        $"Gaia coroutine threw at step {steps}: {ex.Message}", ex);
                }

                // Progress log every ~1% of max to show liveness.
                int pct = (int)(100L * steps / maxSteps);
                if (pct != lastLogPct && pct % 1 == 0 && steps % 50_000 == 0)
                {
                    lastLogPct = pct;
                    Log($"  ... driving coroutine step {steps:N0} (busyFlag={sm.m_worldCreationRunning})");
                }

                if (steps > maxSteps)
                    throw new Exception($"Coroutine exceeded {maxSteps:N0} MoveNext() calls without completing.");
            }

            var elapsed = (DateTime.UtcNow - _startedAt).TotalSeconds;
            Log($"  coroutine complete in {elapsed:F1}s after {steps:N0} MoveNext calls.");
        }

        /// <summary>
        /// Drives a Spawner's coroutines to completion. Unlike world creation,
        /// spawning stores the active coroutine on the Spawner itself
        /// (m_updateCoroutine + m_updateCoroutine2 at Spawner.cs:578/582), not
        /// on GaiaSessionManager. Mirrors Spawner.EditorUpdate (line 1011) minus
        /// the frame-rate throttling.
        /// </summary>
        private static void DriveSpawnerToCompletion(Spawner spawner)
        {
            if (spawner == null) return;

            var maxSteps = 10_000_000;
            int steps = 0;
            var spawnerStart = DateTime.UtcNow;

            while (spawner.m_updateCoroutine != null)
            {
                steps++;
                if ((DateTime.UtcNow - _startedAt).TotalSeconds > TimeoutSeconds)
                    throw new Exception($"TIMEOUT after {TimeoutSeconds}s driving '{spawner.name}'.");

                try
                {
                    if (spawner.m_updateCoroutine2 != null)
                        spawner.m_updateCoroutine2.MoveNext();

                    if (!spawner.m_updateCoroutine.MoveNext())
                    {
                        spawner.m_updateCoroutine = null;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    // Mirror Spawner.EditorUpdate: it swallows exceptions too.
                    // Null the coroutine so we exit cleanly rather than spin.
                    spawner.m_updateCoroutine = null;
                    throw new Exception(
                        $"Spawner '{spawner.name}' threw at step {steps}: {ex.Message}", ex);
                }

                if (steps > maxSteps)
                    throw new Exception($"Spawner '{spawner.name}' exceeded {maxSteps:N0} MoveNext() calls.");
            }

            var spawnerElapsed = (DateTime.UtcNow - spawnerStart).TotalSeconds;
            Log($"      '{spawner.name}' finished in {spawnerElapsed:F1}s after {steps:N0} MoveNext calls.");
        }

        // ── Helpers (duplicated intentionally so this script has no runtime
        //    dependency on the interactive GaiaFixup menu) ─────────────────

        private static WorldCreationSettings BuildSettings(BiomePreset biome, GaiaWorldBuilder.WorldSize size)
        {
            var s = ScriptableObject.CreateInstance<WorldCreationSettings>();
            s.m_qualityPreset = GaiaConstants.EnvironmentTarget.Desktop;
            s.m_seaLevel = 50;
            // m_autoSpawnBiome=false so the stamper-active spawners (texture
            // splats etc.) DON'T run during world creation while terrains are
            // still flat. We apply procedural heightmaps in Step 3.5, then
            // InstantiateAndSpawnBiome (Step 4) runs all spawners together
            // against the now-varied topography. Without this, splat rules
            // see uniform slope=0 everywhere and paint a single layer.
            s.m_autoSpawnBiome = false;
            s.m_centerOffset = Vector2.zero;
            s.m_dateTimeString = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            s.m_spawnerPresetList = biome.m_spawnerPresetList;

            switch (size)
            {
                case GaiaWorldBuilder.WorldSize.Small:
                    s.m_targetSizePreset = GaiaConstants.EnvironmentSizePreset.Medium;
                    s.m_xTiles = 1; s.m_zTiles = 1;
                    s.m_tileSize = 1024; s.m_tileHeight = 600;
                    s.m_createInScene = false; s.m_autoUnloadScenes = false;
                    s.m_applyFloatingPointFix = false;
                    break;
                case GaiaWorldBuilder.WorldSize.Medium:
                    s.m_targetSizePreset = GaiaConstants.EnvironmentSizePreset.Custom;
                    s.m_xTiles = 2; s.m_zTiles = 2;
                    s.m_tileSize = 1024; s.m_tileHeight = 800;
                    // m_createInScene=false keeps the 4 terrains as GameObjects
                    // in the main saved scene rather than splitting them into
                    // per-tile scene files under Assets/Gaia User Data/Sessions/.
                    // The standalone build only ships the main scene, so per-tile
                    // scenes would be absent at runtime → empty world. This is
                    // the right choice for the L (preview) path AND for the
                    // current build — for very large worlds we'd want true +
                    // a runtime additive loader (future work, after S works).
                    s.m_createInScene = false; s.m_autoUnloadScenes = false;
                    s.m_applyFloatingPointFix = true;
                    break;
                case GaiaWorldBuilder.WorldSize.Large:
                    s.m_targetSizePreset = GaiaConstants.EnvironmentSizePreset.Custom;
                    s.m_xTiles = 3; s.m_zTiles = 3;
                    s.m_tileSize = 1024; s.m_tileHeight = 1000;
                    s.m_createInScene = true; s.m_autoUnloadScenes = true;
                    s.m_applyFloatingPointFix = true; s.m_addLoadingScreen = true;
                    break;
            }
            return s;
        }

        // EmptyScene template has no light, no skybox, ambient=black, and no
        // Main Camera. The built world is invisible at runtime without all
        // three. Gaia's UI workflow pairs world creation with a Lighting
        // System component (PWS sun, post-FX volume, weather), but we want
        // only basics here so visuals never depend on the broken PW Sky /
        // weather install. Always overwrite — guards previously skipped the
        // setup when a stale Light or skybox was already present.
        private static void SetupBasicLighting()
        {
            // 1. Sun (directional light) — always (re)create.
            var existingSun = GameObject.Find("Sun");
            if (existingSun != null) UnityEngine.Object.DestroyImmediate(existingSun);
            var sunGO = new GameObject("Sun");
            var light = sunGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.color = new Color(1.0f, 0.957f, 0.839f); // warm sunlight
            light.shadows = LightShadows.Soft;
            sunGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Log("  added Directional Light 'Sun'.");

            // 2. Procedural skybox material — always (re)create. Built-in
            //    shader "Skybox/Procedural" works in URP without any
            //    pipeline-specific dependency.
            var sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunSize", 0.04f);
            sky.SetFloat("_AtmosphereThickness", 1.0f);
            sky.SetColor("_SkyTint", new Color(0.5f, 0.5f, 0.5f));
            sky.SetColor("_GroundColor", new Color(0.369f, 0.349f, 0.341f));
            sky.SetFloat("_Exposure", 1.3f);
            Directory.CreateDirectory("Assets/Settings/Lighting");
            var skyPath = "Assets/Settings/Lighting/HeadlessSkybox.mat";
            // Overwrite if exists.
            var existing = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (existing != null) AssetDatabase.DeleteAsset(skyPath);
            AssetDatabase.CreateAsset(sky, skyPath);
            RenderSettings.skybox = sky;
            Log($"  created procedural skybox at {skyPath}.");

            // 3. Skybox-driven ambient — without this, the terrain renders
            //    almost black even with sun + skybox in place.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.0f;
            DynamicGI.UpdateEnvironment();
            Log("  ambient = Skybox, environment updated.");

            // 4. Main Camera — Gaia doesn't add one; without it Unity has
            //    nothing to render and the build shows the editor's clear
            //    color (dark gray) instead of the world. Place above origin,
            //    pitched down so the camera sees the 2x2 terrain grid centered
            //    around (0,0). Tile size 1024 + applyFloatingPointFix means
            //    terrains span roughly (-1024, -1024)..(1024, 1024).
            var existingCam = GameObject.Find("Main Camera");
            if (existingCam != null) UnityEngine.Object.DestroyImmediate(existingCam);
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 5000f;        // see all 4 tiles + horizon
            camGO.AddComponent<AudioListener>();
            // Position: 400m above the world center, pitched down 30deg looking
            // toward +Z. With tile_height=800 and seaLevel=50, terrain peaks
            // are around 100-300m, so 400m above gives a clean overview.
            camGO.transform.position = new Vector3(0f, 400f, -800f);
            camGO.transform.rotation = Quaternion.Euler(30f, 0f, 0f);
            Log("  added Main Camera (overview vantage at y=400, looking forward).");
        }

        // Multi-octave Perlin heightmap. Gives a mix of broad mountains
        // (low-frequency octave) and finer ridges + ground roughness
        // (higher-frequency octaves). Output range is normalized [0..1] of
        // terrainData.size.y, then biased into [0.08, 0.55] so terrains sit
        // above the sea level (set to 50m on a 800m-tall tile = 0.0625) and
        // peaks reach ~440m. Per-tile world-coord lookup ensures adjacent
        // tiles align seamlessly at their shared edge.
        private static void ApplyProceduralHeightmaps()
        {
            var terrains = UnityEngine.Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (terrains.Length == 0)
            {
                Log("  no terrains to stamp — skipping.");
                return;
            }

            // Octave config: each successive octave doubles frequency and
            // halves amplitude (standard fBm). 4 octaves gives smooth terrain
            // with detail; 6+ starts adding noise that doesn't read as
            // landform.
            const int Octaves = 4;
            const float BaseFrequency = 0.0008f; // wavelength ~= 1250m
            const float MinHeight = 0.08f;       // ~64m on a 800m tile
            const float HeightRange = 0.47f;     // up to 0.55 = ~440m peaks

            // Pre-compute amplitude normalization (sum of geometric series).
            float ampNorm = 0f;
            for (int o = 0; o < Octaves; o++) ampNorm += Mathf.Pow(0.5f, o);

            foreach (var t in terrains)
            {
                if (t.terrainData == null) continue;
                var data = t.terrainData;
                int res = data.heightmapResolution;
                var heights = new float[res, res];
                var origin = t.transform.position;
                var size = data.size;

                for (int y = 0; y < res; y++)
                {
                    // World Z at this heightmap row.
                    float wz = origin.z + ((float)y / (res - 1)) * size.z;
                    for (int x = 0; x < res; x++)
                    {
                        float wx = origin.x + ((float)x / (res - 1)) * size.x;

                        // Multi-octave Perlin (fBm).
                        float n = 0f;
                        float amp = 1f;
                        float freq = BaseFrequency;
                        for (int o = 0; o < Octaves; o++)
                        {
                            // +1024 offsets keep the noise away from Perlin's
                            // origin (which has known directional bias at
                            // exact integer coords).
                            n += Mathf.PerlinNoise((wx + 1024f) * freq, (wz + 1024f) * freq) * amp;
                            amp *= 0.5f;
                            freq *= 2f;
                        }
                        n /= ampNorm;          // normalize to [0..1]
                        heights[y, x] = MinHeight + n * HeightRange;
                    }
                }

                data.SetHeights(0, 0, heights);
                Log($"    stamped '{t.name}' (res={res}x{res}, range=[{MinHeight * size.y:F0}m, {(MinHeight + HeightRange) * size.y:F0}m])");
            }

            Log($"  stamped {terrains.Length} terrain(s).");
        }

        private static void CleanBrokenTerrains()
        {
            var terrains = UnityEngine.Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var toDestroy = new List<GameObject>();
            foreach (var t in terrains)
                if (t != null && t.terrainData == null) toDestroy.Add(t.gameObject);
            foreach (var go in toDestroy)
                UnityEngine.Object.DestroyImmediate(go);
            if (toDestroy.Count > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Log($"  removed {toDestroy.Count} broken Terrain(s).");
            }
        }

        // Per gaia-architecture skill Bug #12: Unity 6 changed terrain culling.
        // Gaia's 2021-era defaults (PixelError=5, basemapDistance=1024) cause
        // terrain to disappear when camera is within ~1000-1500 units. Our
        // 4000 km static planet plan would see literal floor-of-the-world holes
        // at every step. PixelError 10 = safer mip-LOD threshold for distant
        // terrain. basemapDistance 4096 = 4 tile-widths; splat fidelity holds
        // for the entire 2x2 grid even when standing at a corner.
        private const int CullingPixelError = 10;
        private const float CullingBasemapDistance = 4096f;

        private static void ApplyTerrainCullingSettings()
        {
            var terrains = UnityEngine.Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            int n = 0;
            foreach (var t in terrains)
            {
                if (t == null || t.terrainData == null) continue;
                t.heightmapPixelError = CullingPixelError;
                t.basemapDistance = CullingBasemapDistance;
                n++;
            }
            if (n > 0)
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Log($"  applied culling overrides to {n} terrain(s) (PixelError={CullingPixelError}, BasemapDistance={CullingBasemapDistance}).");
        }

        // Step 5.5 — wire the Procedural Worlds Sky into the scene so runtime
        // GaiaAPI.SetTimeOfDay* / SetWeather* calls have a PWSkyStandalone.Instance
        // to drive. Without this step PWSky stays installed-on-disk but unwired,
        // and every atmospherics call silently no-ops (gaia-architecture Bug #9).
        // GRC_PWSky.AddToScene() guards against HDRP internally; on URP it
        // disables existing dir-lights, instantiates the PW Sky prefab tree,
        // assigns the HDRI skybox material, and sets RenderSettings.fog=true.
        // Failure is non-fatal — bake proceeds without atmospherics if PW Sky
        // assets are missing.
        private static void ApplyPWSky()
        {
            try
            {
                var grcType = Type.GetType("Gaia.GRC_PWSky, Assembly-CSharp")
                              ?? AppDomain.CurrentDomain.GetAssemblies()
                                  .Select(a => a.GetType("Gaia.GRC_PWSky"))
                                  .FirstOrDefault(t => t != null);
                if (grcType == null)
                {
                    Log("  GRC_PWSky type not found — Procedural Worlds Sky sub-package not installed. Skipping (atmospherics will silent-noop).");
                    return;
                }

                var grc = ScriptableObject.CreateInstance(grcType);
                if (grc == null)
                {
                    Log("  ScriptableObject.CreateInstance(GRC_PWSky) returned null. Skipping.");
                    return;
                }

                // Initialize() seeds m_orderNumber + help links. AddToScene() does the work.
                grcType.GetMethod("Initialize")?.Invoke(grc, null);
                grcType.GetMethod("AddToScene")?.Invoke(grc, null);

                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Log("  PWSky scene infrastructure applied (PW Sky.prefab + Lighting + weather + skybox + fog=true).");
            }
            catch (Exception ex)
            {
                Log($"  PWSky setup failed (non-fatal): {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void RunBuiltInToUrpConverter()
        {
#if UNITY_2022_2_OR_NEWER
            try
            {
                var ids = new List<ConverterId>
                {
                    ConverterId.Material,
                    ConverterId.ReadonlyMaterial,
                };
                Converters.RunInBatchMode(
                    ConverterContainerId.BuiltInToURP,
                    ids,
                    ConverterFilter.Inclusive);
                Log("  Built-In → URP converter finished.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GaiaHeadless] URP converter threw (continuing): {ex.Message}");
            }
#endif
        }

        private static void RunNatureManufactureMatFixer()
        {
            try
            {
                ForeverEngine.Editor.NatureManufactureMatFixer.Run();
                Log("  NatureManufactureMatFixer finished.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GaiaHeadless] MatFixer threw (continuing): {ex.Message}");
            }
        }

        private static int CountAllDescendants(Transform t)
        {
            int n = 0;
            foreach (Transform child in t) { n++; n += CountAllDescendants(child); }
            return n;
        }

        private static void RunMacroBake()
        {
            // Force Gaia-authored prop source for this bake. PropSourceSelector
            // already defaults to Gaia, but if a developer toggled to Synthetic
            // for a CI run, we don't want to silently produce synthetic props
            // from the Gaia world.
            ForeverEngine.Procedural.Editor.PropSourceSelector.UseGaiaAuthored = true;

            var terrains = UnityEngine.Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int totalTreeInstances = 0;
            foreach (var t in terrains)
            {
                if (t.terrainData != null) totalTreeInstances += t.terrainData.treeInstanceCount;
                int childCount = CountAllDescendants(t.transform);
                int protoCount = t.terrainData != null && t.terrainData.treePrototypes != null
                    ? t.terrainData.treePrototypes.Length : 0;
                int tdInstanceId = t.terrainData != null ? t.terrainData.GetInstanceID() : 0;
                string tdName = t.terrainData != null ? t.terrainData.name : "<null>";
                string tdAssetPath = t.terrainData != null
                    ? UnityEditor.AssetDatabase.GetAssetPath(t.terrainData) : "<null>";
                int hmRes = t.terrainData != null ? t.terrainData.heightmapResolution : 0;
                int detailCount = t.terrainData != null && t.terrainData.detailPrototypes != null
                    ? t.terrainData.detailPrototypes.Length : 0;
                Log($"    terrain '{t.name}' pos={t.transform.position} dataName='{tdName}' " +
                    $"hmRes={hmRes} treeProtos={protoCount} treeInstances={t.terrainData?.treeInstanceCount ?? 0} " +
                    $"detailProtos={detailCount} GOdescendants={childCount}");
                Log($"      dataPath={tdAssetPath}");
            }
            Log($"  bake input total: {terrains.Length} terrains, {totalTreeInstances} tree instances total.");

            if (terrains.Length == 0)
            {
                Debug.LogWarning("[GaiaHeadless] No terrains in scene at bake time — skipping bake.");
                return;
            }

            try
            {
                ForeverEngine.Procedural.Editor.MacroBakeTool.BakeAllTilesInSceneOrThrow();
                Log("  macro bake complete.");
            }
            catch (Exception ex)
            {
                // Bake failure is loud but shouldn't lose the world. Save the
                // scene anyway so the world can be re-baked separately.
                Debug.LogError($"[GaiaHeadless] Macro bake threw — scene will still save: {ex.Message}");
            }
        }

        /// <summary>
        /// Attaches GTS to each baked Terrain so they render with PW's GTS shader
        /// instead of URP/Terrain/Lit. One GTSProfile per (biome, size); subsequent
        /// bakes reuse the same asset so manual tuning persists across re-bakes.
        /// </summary>
        private static void ApplyGTSToBakedTerrains()
        {
            var terrains = Terrain.activeTerrains;
            if (terrains.Length == 0)
            {
                Log("  no terrains; skipping GTS");
                return;
            }

            // Profile + texture arrays land under Assets/Resources/GTSProfiles/
            // so the runtime BakedTerrainTileRenderer can Resources.Load them
            // by name (matches BakedAssetRegistry / PrefabRegistry precedent;
            // project uses Resources, not Addressables).
            const string profilesDir = "Assets/Resources/GTSProfiles";
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(profilesDir))
                AssetDatabase.CreateFolder("Assets/Resources", "GTSProfiles");
            var profilePath = $"{profilesDir}/{_biomeName.Replace(' ', '_')}_{_sizeName}_GTS.asset";

            var profile = AssetDatabase.LoadAssetAtPath<GTSProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<GTSProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            AssetDatabase.SaveAssets();

            // Pass 1: attach GTSTerrain components first. RefreshTerrainLayers(Terrain[])
            // skips any terrain that doesn't already have one (GTSProfile.cs:722), so
            // calling it before AddGTSToTerrain produces a profile with 0 layers and
            // null _AlbedoArray on every generated material.
            foreach (var t in terrains)
                profile.AddGTSToTerrain(t);

            profile.RefreshTerrainLayers(terrains);
            profile.CreateTextureArrays();
            profile.SetRuntimeData();

            // Pass 2: generate material + bake per-terrain textures.
            foreach (var t in terrains)
            {
                var gts = t.GetComponent<GTSTerrain>();
                gts.ApplyProfile();
                // Persist material to disk before SaveAllTextures() resolves AssetDatabase.GetAssetPath(material).
                AssetDatabase.SaveAssets();
                gts.UpdateAllTextures();
                gts.SaveAllTextures();
            }
            profile.UpdateProfile();
            AssetDatabase.SaveAssets();
            Log($"  GTS applied to {terrains.Length} terrain(s); profile={profilePath}");

            // Copy each terrain's GTS material into Resources/ so the runtime
            // BakedTerrainTileRenderer can Resources.Load it by tile coord.
            // Without this the .mat lives at Assets/GTS User Data/Scenes/...
            // which is OUTSIDE Resources/ → standalone builds render default
            // Unity grass instead of GTS (community-reported, see gaia skill
            // Bug #25). The runtime SO from SetRuntimeData() is already
            // Resources-loadable because it lives next to the profile.
            const string matsDir = "Assets/Resources/GTSMaterials";
            if (!AssetDatabase.IsValidFolder(matsDir))
                AssetDatabase.CreateFolder("Assets/Resources", "GTSMaterials");
            string biomeKey = $"{_biomeName.Replace(' ', '_')}_{_sizeName}";
            // Tile coord derivation: parse Gaia's "Terrain_<x>_<z>-<timestamp>"
            // naming convention. Server-side baked tile_X_Y dirs use the same
            // 0-based positive scheme — perfect alignment with what the runtime
            // BakedTerrainTileRenderer's RetainTile(tileX, tileZ) caller passes.
            // Position-based rounding would fail because Gaia centers tiles
            // around world origin → negative coords for half the tiles.
            var tilePattern = new System.Text.RegularExpressions.Regex(@"^Terrain_(\d+)_(\d+)");
            int copied = 0;
            foreach (var t in terrains)
            {
                var match = tilePattern.Match(t.name);
                if (!match.Success)
                {
                    Log($"    skip mat copy for '{t.name}' — name doesn't match Terrain_X_Y");
                    continue;
                }
                int tx = int.Parse(match.Groups[1].Value);
                int tz = int.Parse(match.Groups[2].Value);
                var srcPath = AssetDatabase.GetAssetPath(t.materialTemplate);
                if (string.IsNullOrEmpty(srcPath))
                {
                    Log($"    skip mat copy for '{t.name}' — materialTemplate has no asset path");
                    continue;
                }
                var dstPath = $"{matsDir}/{biomeKey}_tile_{tx}_{tz}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(dstPath) != null)
                    AssetDatabase.DeleteAsset(dstPath);
                if (AssetDatabase.CopyAsset(srcPath, dstPath))
                    copied++;
                else
                    Log($"    CopyAsset failed: {srcPath} -> {dstPath}");
            }
            AssetDatabase.SaveAssets();
            Log($"  GTS materials copied to Resources: {copied}/{terrains.Length}");
        }

        /// <summary>
        /// Instantiates the full biome (BiomeController + every Spawner child)
        /// and triggers each active spawner to paint its prefabs onto the
        /// terrain. Each Spawn.Spawn() call kicks off its own coroutine which
        /// we drive to completion before moving to the next.
        /// </summary>
        private static void InstantiateAndSpawnBiome(BiomePreset biome)
        {
            // Build the biome GO tree (<Biome> Biome + child Spawner GameObjects).
            var biomeController = biome.CreateBiome(autoAssignPrototypes: true);
            if (biomeController == null)
                throw new Exception("BiomePreset.CreateBiome returned null.");
            Log($"  instantiated biome '{biomeController.name}' with {biomeController.m_autoSpawners?.Count ?? 0} auto-spawners.");

            if (biomeController.m_autoSpawners == null || biomeController.m_autoSpawners.Count == 0)
            {
                Log("  (no auto-spawners on this biome — skipping spawn step.)");
                return;
            }

            int ok = 0, skipped = 0, fail = 0;
            for (int i = 0; i < biomeController.m_autoSpawners.Count; i++)
            {
                var auto = biomeController.m_autoSpawners[i];
                if (auto == null || auto.spawner == null) { skipped++; continue; }
                if (!auto.isActive) { skipped++; continue; }

                var name = auto.spawner.name;
                // Skip spawners with known broken assets (PW Spruce trees crash
                // TreeDatabase::ValidateTrees in Unity 6). Option A attempted
                // 2026-04-26 PM (replaced empty stubs with full URP/Lit content
                // copied from PW_Tree_Spruce_01_Needles template) — DID NOT FIX
                // the crash. The validator failure is deeper than material
                // content, likely in the FBX geometry or LOD chain. See
                // project_pw_spruce_deferred.md for repair history; needs
                // a different angle (re-import FBXs, replace with Asset Store
                // spruces, or skip indefinitely).
                if (name != null && name.Contains("Tree Spruce"))
                {
                    Log($"    [{i + 1}/{biomeController.m_autoSpawners.Count}] SKIPPING '{name}' (TreeDatabase::ValidateTrees crash; Option A failed)");
                    skipped++;
                    continue;
                }
                Log($"    [{i + 1}/{biomeController.m_autoSpawners.Count}] spawning '{name}'...");
                try
                {
                    auto.spawner.Spawn(allTerrains: true);
                    DriveSpawnerToCompletion(auto.spawner);
                    ok++;
                }
                catch (Exception ex)
                {
                    fail++;
                    Debug.LogWarning($"[GaiaHeadless]    '{name}' failed: {ex.Message}");
                }
            }
            Log($"  biome spawn complete. ok={ok} skipped={skipped} fail={fail}");
        }

        // ── DesertBeachCave content spawners ─────────────────────────────────

        private static GameObject FindFirstPrefab(string queryFolder, string nameContains)
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { queryFolder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains(nameContains, StringComparison.OrdinalIgnoreCase)) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) return prefab;
            }
            return null;
        }

        private static void AddTreePrototype(Terrain t, GameObject prefab)
        {
            if (prefab == null) return;
            var data = t.terrainData;
            var existing = data.treePrototypes;
            var arr = new TreePrototype[existing.Length + 1];
            Array.Copy(existing, arr, existing.Length);
            arr[existing.Length] = new TreePrototype { prefab = prefab };
            data.treePrototypes = arr;
        }

        private static void ScatterTreesByMask(
            Terrain t, int prototypeIdx, int count,
            float minY, float maxY, float maxSlopeDeg, int seed)
        {
            var data = t.terrainData;
            var rng = new System.Random(seed);
            var instances = new List<TreeInstance>(data.treeInstances);
            int placed = 0, attempts = 0, maxAttempts = count * 20;
            while (placed < count && attempts++ < maxAttempts)
            {
                float nx = (float)rng.NextDouble();
                float nz = (float)rng.NextDouble();
                float worldY = data.GetInterpolatedHeight(nx, nz);
                float slope = data.GetSteepness(nx, nz);
                if (worldY < minY || worldY > maxY || slope > maxSlopeDeg) continue;

                instances.Add(new TreeInstance
                {
                    prototypeIndex = prototypeIdx,
                    position = new Vector3(nx, worldY / data.size.y, nz),
                    heightScale = 1f, widthScale = 1f,
                    rotation = (float)(rng.NextDouble() * Math.PI * 2),
                    color = Color.white, lightmapColor = Color.white,
                });
                placed++;
            }
            data.treeInstances = instances.ToArray();
            Log($"  scattered {placed} trees (proto={prototypeIdx})");
        }

        private static void ScatterPrefabsByMask(
            Terrain t, GameObject[] prefabs, int count,
            float minY, float maxY, float maxSlopeDeg, int seed,
            Transform parent)
        {
            if (prefabs == null || prefabs.Length == 0) { Log($"  no prefabs to scatter"); return; }
            var data = t.terrainData;
            var rng = new System.Random(seed);
            int placed = 0, attempts = 0, maxAttempts = count * 20;
            while (placed < count && attempts++ < maxAttempts)
            {
                float nx = (float)rng.NextDouble();
                float nz = (float)rng.NextDouble();
                float worldY = data.GetInterpolatedHeight(nx, nz);
                float slope = data.GetSteepness(nx, nz);
                if (worldY < minY || worldY > maxY || slope > maxSlopeDeg) continue;

                var prefab = prefabs[rng.Next(prefabs.Length)];
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.transform.position = new Vector3(
                    t.transform.position.x + nx * data.size.x,
                    worldY,
                    t.transform.position.z + nz * data.size.z);
                go.transform.rotation = Quaternion.Euler(0, (float)(rng.NextDouble() * 360), 0);
                placed++;
            }
            Log($"  scattered {placed} prefabs from {prefabs.Length} prototypes");
        }

        private static void PlaceDesertBeachCaveStructure()
        {
            Log("=== Step 7/9: place 3DForge cave entrance at cliff base ===");

            GameObject cavePrefab = FindFirstPrefab(
                "Assets/3DForge/Cave Adventure kit", "Entrance");
            if (cavePrefab == null) cavePrefab = FindFirstPrefab(
                "Assets/3DForge/Cave Adventure kit", "Tunnel");
            if (cavePrefab == null) cavePrefab = FindFirstPrefab(
                "Assets/3DForge/Cave Adventure kit", "Wall");

            if (cavePrefab == null)
            {
                Log("  WARN: no 3DForge Cave Adventure kit prefab found — skipping cave placement");
                return;
            }

            var terrains = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            if (terrains.Length == 0) return;
            var terrain = terrains[0];

            // Sample Y at world (850, ?, 512) — base of east cliff
            var data = terrain.terrainData;
            if (data == null)
            {
                Log("  WARN: terrain has null terrainData — skipping cave placement");
                return;
            }
            float nx = (850f - terrain.transform.position.x) / data.size.x;
            float nz = (512f - terrain.transform.position.z) / data.size.z;
            float baseY = data.GetInterpolatedHeight(nx, nz);

            var caveRoot = new GameObject("DesertBeachCave_Cave");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(cavePrefab, caveRoot.transform);
            go.transform.position = new Vector3(850f, baseY, 512f);
            go.transform.rotation = Quaternion.Euler(0, 270f, 0);  // entrance faces west (toward ocean)
            Log($"  placed cave prefab '{cavePrefab.name}' at (850, {baseY:F1}, 512)");
        }

        private static void SpawnDesertBeachCaveContent()
        {
            Log("=== Step 6/9: spawn palms + rocks ===");

            var terrains = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            var contentRoot = new GameObject("DesertBeachCave_Content");

            foreach (var t in terrains)
            {
                if (t.terrainData == null)
                {
                    Log($"  WARN: terrain {t.name} has null terrainData — skipping");
                    continue;
                }

                // Palms (terrainTrees — efficient, batched)
                var palm = FindFirstPrefab("Assets/TFP/2_Prefabs/Trees", "Palm");
                if (palm == null) palm = FindFirstPrefab("Assets/TFP/2_Prefabs/Trees", "Tree");
                if (palm != null)
                {
                    int idx = t.terrainData.treePrototypes.Length;
                    AddTreePrototype(t, palm);
                    ScatterTreesByMask(t, idx, count: 30, minY: 50f, maxY: 65f, maxSlopeDeg: 15f, seed: 1337);
                }
                else { Log("  WARN: no palm prefab found in Assets/TFP/2_Prefabs/Trees — skipping"); }

                // Rocks (GameObject prefabs — placed under contentRoot)
                var rockGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Hivemind", "Assets/_SwampBundle" });
                var rockPrefabs = rockGuids
                    .Select(g => AssetDatabase.GUIDToAssetPath(g))
                    .Where(p => p.Contains("Rock", StringComparison.OrdinalIgnoreCase))
                    .Take(8)
                    .Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p))
                    .Where(p => p != null)
                    .ToArray();
                ScatterPrefabsByMask(t, rockPrefabs, count: 15, minY: 55f, maxY: 80f,
                                     maxSlopeDeg: 35f, seed: 2024, parent: contentRoot.transform);
            }
        }

        private static void SetupCrestWater()
        {
            Log("=== Step 8/9: setup Crest Water 5 ocean ===");

            // Try prefab path first; fall back to AddComponent if no prefab ships
            var waterPrefabGuids = AssetDatabase.FindAssets("WaterRenderer t:Prefab");
            GameObject waterPrefab = null;
            foreach (var g in waterPrefabGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                if (!path.Contains("waveharmonic.crest")) continue;
                waterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (waterPrefab != null) { Log($"  loaded Crest prefab: {path}"); break; }
            }

            GameObject waterGo;
            if (waterPrefab != null)
            {
                waterGo = (GameObject)PrefabUtility.InstantiatePrefab(waterPrefab);
                waterGo.transform.position = new Vector3(0, 50f, 0);
            }
            else
            {
                Log("  no Crest prefab found — attaching WaterRenderer component manually");
                waterGo = new GameObject("Crest WaterRenderer");
                waterGo.transform.position = new Vector3(0, 50f, 0);
                // Reflective AddComponent — Crest's WaterRenderer type is in the WaveHarmonic.Crest.Scripting assembly.
                var crestAsm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name.Contains("Crest", StringComparison.OrdinalIgnoreCase));
                if (crestAsm == null)
                {
                    Log("  WARN: Crest assembly not found at runtime — skipping water setup");
                    UnityEngine.Object.DestroyImmediate(waterGo);
                    return;
                }
                var waterRendererType = crestAsm.GetType("WaveHarmonic.Crest.WaterRenderer");
                if (waterRendererType != null)
                    waterGo.AddComponent(waterRendererType);
                else
                    Log("  WARN: WaterRenderer type not found in Crest assembly — water inert");
            }

            // Attach WaterCamera to main camera if one exists
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                var crestAsm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name.Contains("Crest", StringComparison.OrdinalIgnoreCase));
                var waterCamType = crestAsm?.GetType("WaveHarmonic.Crest.WaterCamera");
                if (waterCamType != null && mainCam.GetComponent(waterCamType) == null)
                    mainCam.gameObject.AddComponent(waterCamType);
            }

            Log("  Crest water configured at sea level Y=50");
        }

        private static void Log(string msg) => Debug.Log($"[GaiaHeadless] {msg}");

        /// <summary>
        /// Batchmode has no SceneView by default, and Gaia's ExecuteCreateWorld
        /// dereferences <c>SceneView.lastActiveSceneView.camera</c>. Create a
        /// hidden one so the NRE doesn't kill the coroutine.
        /// </summary>
        private static void EnsureSceneViewExists()
        {
            if (SceneView.lastActiveSceneView != null) return;

            // GetWindow constructs a SceneView without showing it in batchmode.
            // The window object is enough to make `lastActiveSceneView` non-null.
            try
            {
                var sv = EditorWindow.GetWindow<SceneView>(utility: false, title: "Scene", focus: false);
                if (sv != null)
                {
                    Log("  spawned hidden SceneView for batchmode coroutine.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GaiaHeadless] Could not spawn SceneView: {ex.Message}");
            }
        }
    }
}
#endif
