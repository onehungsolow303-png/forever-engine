// Assets/Editor/BuildRealBeach.cs
// Real ocean-front beach using NM Coast Environment's pre-authored terrain.
// DO NOT use Gaia stamps. DO NOT use 3DForge prefabs.
// Invocation (batchmode, NO -quit, NO -nographics):
//   Unity.exe -batchmode -projectPath "C:/Dev/Forever engine"
//     -executeMethod ForeverEngine.Editor.BuildRealBeach.Build
//     -logFile /c/tmp/build-real-beach.log
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_2022_2_OR_NEWER
using UnityEditor.Rendering.Universal;
#endif

namespace ForeverEngine.Editor
{
    public static class BuildRealBeach
    {
        private const string LogPrefix = "[BuildRealBeach]";
        private const string ScenePath = "Assets/Scenes/GaiaWorld_DesertBeachCave.unity";

        // ── Asset paths (verified from disk this session) ────────────────────

        private const string CoastTerrainPath =
            "Assets/NatureManufacture Assets/Coast Environment/Demo Scene/Coast Demo Terrain.asset";

        private const string LayerDir =
            "Assets/NatureManufacture Assets/Coast Environment/Ground/Ground Layers";

        private const string CliffDir =
            "Assets/NatureManufacture Assets/Coast Environment/Cliffs/Prefabs Sand 1";

        private const string CavePrefabPath =
            "Assets/Realistic Natural Cave 2/Prefabs/CavePart_1.prefab";

        private const string PalmPrefabPath =
            "Assets/TFP/2_Prefabs/Trees/CoconutPalmTree01_LOD.prefab";

        private const string PineDirA =
            "Assets/NatureManufacture Assets/Coast Environment/Pine Trees/Prefabs/Prefab_Pine_beach_01_A.prefab";

        private const string PineDirShort =
            "Assets/NatureManufacture Assets/Coast Environment/Pine Trees/Prefabs/Prefab_Pine_beach_short_01.prefab";

        private const string DetailDir =
            "Assets/NatureManufacture Assets/Coast Environment/Details/Prefabs";

        private const string GrassDir =
            "Assets/NatureManufacture Assets/Coast Environment/Foliage and Grass/Prefabs";

        // ── Entry point ──────────────────────────────────────────────────────

        [MenuItem("Forever Engine/Build Real Beach Scene")]
        public static void Build()
        {
            var startedAt = DateTime.UtcNow;
            try
            {
                // ── Step 1: empty scene ──────────────────────────────────────
                Log("=== Step 1: new empty scene ===");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Directory.CreateDirectory("Assets/Scenes");
                EditorSceneManager.SaveScene(scene, ScenePath);
                Log($"  saved at {ScenePath}");

                // ── Step 2: lighting + skybox + ambient ──────────────────────
                Log("=== Step 2: basic lighting (sun + skybox + ambient) ===");
                SetupLighting();

                // ── Step 3: place NM Coast Demo Terrain ─────────────────────
                Log("=== Step 3: create Terrain from NM Coast Demo Terrain.asset ===");
                var terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(CoastTerrainPath);
                if (terrainData == null)
                    throw new Exception($"Coast Demo Terrain not found at: {CoastTerrainPath}");
                Log($"  loaded TerrainData '{terrainData.name}'  size={terrainData.size}  hmRes={terrainData.heightmapResolution}");

                var terrainGo = Terrain.CreateTerrainGameObject(terrainData);
                terrainGo.name = "NM_CoastTerrain";

                // Center terrain at world origin
                var data = terrainGo.GetComponent<Terrain>().terrainData;
                terrainGo.transform.position = new Vector3(
                    -data.size.x / 2f, 0f, -data.size.z / 2f);
                Log($"  terrain centered at {terrainGo.transform.position}  (size={data.size})");

                var terrain = terrainGo.GetComponent<Terrain>();
                terrain.heightmapPixelError = 5f;
                terrain.basemapDistance = 2000f;
                terrain.treeDistance = 2000f;
                terrain.treeBillboardDistance = 600f;

                // ── Step 4: apply terrain layers ────────────────────────────
                Log("=== Step 4: apply NM Sand terrain layers ===");
                ApplyTerrainLayers(terrain);

                // ── Step 5: NM MatFixer ─────────────────────────────────────
                Log("=== Step 5: NatureManufactureMatFixer ===");
                RunNmMatFixer();

                // ── Step 6: compute height stats ────────────────────────────
                Log("=== Step 6: sample heightmap for high-ground ridge + sea level ===");
                var (minTerrainY, maxTerrainY, ridgeWorldX, ridgeWorldZ, terrainOrigin) =
                    AnalyseHeightmap(terrain);

                float range = maxTerrainY - minTerrainY;
                float seaLevel = minTerrainY + 1.5f;
                Log($"  terrain Y range: min={minTerrainY:F2} max={maxTerrainY:F2} range={range:F2}");
                Log($"  sea level Y = {seaLevel:F2}");
                Log($"  ridge world X = {ridgeWorldX:F2}  ridge Z mid = {ridgeWorldZ:F2}");

                // ── Step 7: place beach cliff chain along ridge ──────────────
                Log("=== Step 7: place 5 NM sand beach cliff prefabs along ridge ===");
                Vector3 cliffMidpoint = PlaceCliffChain(terrain, ridgeWorldX, ridgeWorldZ, seaLevel, range);
                Log($"  cliff midpoint at {cliffMidpoint}");

                // ── Step 8: embed cave at cliff midpoint ─────────────────────
                Log("=== Step 8: embed CavePart_1 at cliff midpoint ===");
                PlaceCave(cliffMidpoint, ridgeWorldX);

                // ── Step 9: scatter trees (terrain treeInstances) ─────────────
                Log("=== Step 9: scatter ~80 trees on beach band ===");
                int treesPlaced = ScatterTrees(terrain, minTerrainY, range, seaLevel);
                Log($"  trees scattered: {treesPlaced}");

                // ── Step 10: scatter detail prefabs ─────────────────────────
                Log("=== Step 10: scatter ~60 detail prefabs (driftwood/shells/grass) ===");
                int detailsPlaced = ScatterDetailPrefabs(terrain, minTerrainY, range, seaLevel, terrainOrigin);
                Log($"  details scattered: {detailsPlaced}");

                // ── Step 11: Crest water at sea level ───────────────────────
                Log($"=== Step 11: Crest WaterRenderer at sea level Y={seaLevel:F2} ===");
                SetupCrestWater(seaLevel);

                // ── Step 12: Main Camera ─────────────────────────────────────
                Log("=== Step 12: ensure Main Camera ===");
                EnsureCamera(seaLevel);

                // ── Step 13: save + exit ─────────────────────────────────────
                Log("=== Step 13: save scene + verify ===");
                var activeScene = EditorSceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(activeScene);
                bool saved = EditorSceneManager.SaveScene(activeScene, ScenePath);
                if (!saved) throw new Exception($"SaveScene returned false for {ScenePath}");

                // Post-save assertion: scene file must be substantial (terrain + content adds ≥100 KB)
                AssetDatabase.Refresh();
                var sceneInfo = new FileInfo(ScenePath);
                if (!sceneInfo.Exists) throw new Exception($"Scene file missing post-save: {ScenePath}");
                if (sceneInfo.Length < 100_000)
                    throw new Exception($"Scene file is suspiciously small ({sceneInfo.Length} bytes) — content didn't make it to disk. Expected >= 100 KB for terrain + cliffs + cave + trees + details + water. Check for scene-reload mid-build.");
                Log($"  scene saved: {sceneInfo.Length:N0} bytes");

                double elapsed = (DateTime.UtcNow - startedAt).TotalSeconds;
                Log($"=== DONE in {elapsed:F1}s. Scene: {ScenePath}  trees={treesPlaced}  details={detailsPlaced}  seaLevel={seaLevel:F2} ===");
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogPrefix} FAIL: {ex}");
                EditorApplication.Exit(1);
            }
        }

        // ── Step 2 ───────────────────────────────────────────────────────────

        private static void SetupLighting()
        {
            // Directional sun
            var existing = GameObject.Find("Sun");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
            var sunGo = new GameObject("Sun");
            var light = sunGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.color = new Color(1.0f, 0.957f, 0.839f);
            light.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Procedural skybox
            var skyMat = new Material(Shader.Find("Skybox/Procedural"));
            skyMat.SetFloat("_SunSize", 0.04f);
            skyMat.SetFloat("_AtmosphereThickness", 1.0f);
            skyMat.SetColor("_SkyTint", new Color(0.5f, 0.6f, 0.8f));
            skyMat.SetColor("_GroundColor", new Color(0.8f, 0.75f, 0.65f));
            skyMat.SetFloat("_Exposure", 1.4f);
            Directory.CreateDirectory("Assets/Settings/Lighting");
            const string skyPath = "Assets/Settings/Lighting/RealBeachSkybox.mat";
            var existingSky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (existingSky != null) AssetDatabase.DeleteAsset(skyPath);
            AssetDatabase.CreateAsset(skyMat, skyPath);
            RenderSettings.skybox = skyMat;

            // Skybox-driven ambient
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.0f;
            DynamicGI.UpdateEnvironment();
            Log("  sun + skybox + ambient applied.");
        }

        // ── Step 4 ───────────────────────────────────────────────────────────

        private static void ApplyTerrainLayers(Terrain terrain)
        {
            // Load the three NM layers by exact path
            var sand01 = LoadTerrainLayerRequired($"{LayerDir}/Terrain_Layer_Sand_01.terrainlayer");
            var sand02 = LoadTerrainLayerRequired($"{LayerDir}/Terrain_Layer_Sand_02.terrainlayer");
            var cliffSand01 = LoadTerrainLayerRequired($"{LayerDir}/Terrain_Layer_Cliff_Sand_01.terrainlayer");

            terrain.terrainData.terrainLayers = new[] { sand01, sand02, cliffSand01 };
            // Don't repaint the alphamap — NM's terrain already has authored splats;
            // leaving them intact preserves the beach/cliff/dune look.
            Log($"  applied 3 NM sand layers to '{terrain.name}'. Existing alphamap preserved.");
        }

        private static TerrainLayer LoadTerrainLayerRequired(string path)
        {
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null) throw new Exception($"TerrainLayer not found at: {path}");
            return layer;
        }

        // ── Step 5 ───────────────────────────────────────────────────────────

        private static void RunUrpConverter()
        {
#if UNITY_2022_2_OR_NEWER
            try
            {
                var ids = new List<ConverterId> { ConverterId.Material, ConverterId.ReadonlyMaterial };
                Converters.RunInBatchMode(ConverterContainerId.BuiltInToURP, ids, ConverterFilter.Inclusive);
                Log("  URP converter done.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogPrefix} URP converter threw (non-fatal): {ex.Message}");
            }
#endif
        }

        private static void RunNmMatFixer()
        {
            try
            {
                ForeverEngine.Editor.NatureManufactureMatFixer.Run();
                Log("  NatureManufactureMatFixer done.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogPrefix} MatFixer threw (non-fatal): {ex.Message}");
            }
        }

        // ── Step 6: heightmap analysis ───────────────────────────────────────

        /// <summary>
        /// Returns (minY, maxY, ridgeWorldX, ridgeWorldZ, terrainOrigin).
        /// ridgeWorldX = world X of the heightmap column with the highest average Y (inland high-ground).
        /// ridgeWorldZ = midpoint of the Z extent of that ridge column.
        /// </summary>
        private static (float minY, float maxY, float ridgeX, float ridgeZ, Vector3 origin)
            AnalyseHeightmap(Terrain terrain)
        {
            var tData = terrain.terrainData;
            int res = tData.heightmapResolution;
            var size = tData.size;
            var origin = terrain.transform.position;
            var heights = tData.GetHeights(0, 0, res, res); // [z,x]

            float globalMin = float.MaxValue, globalMax = float.MinValue;
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                {
                    float wy = heights[z, x] * size.y;
                    if (wy < globalMin) globalMin = wy;
                    if (wy > globalMax) globalMax = wy;
                }

            // Find the X column with highest average world Y — that's the natural ridge
            float bestColAvg = float.MinValue;
            int bestColX = 0;
            for (int x = 0; x < res; x++)
            {
                float sum = 0f;
                for (int z = 0; z < res; z++) sum += heights[z, x] * size.y;
                float avg = sum / res;
                if (avg > bestColAvg) { bestColAvg = avg; bestColX = x; }
            }

            float ridgeWorldX = origin.x + (float)bestColX / (res - 1) * size.x;
            // Z spread 30%-70% → midpoint is at 50% = centre of Z extent
            float ridgeWorldZ = origin.z + 0.5f * size.z;

            return (globalMin, globalMax, ridgeWorldX, ridgeWorldZ, origin);
        }

        // ── Step 7: cliff chain ───────────────────────────────────────────────

        private static Vector3 PlaceCliffChain(
            Terrain terrain, float ridgeWorldX, float ridgeWorldZMid,
            float seaLevel, float range)
        {
            // Required prefabs — all 5 must exist or we throw
            var cliffEndL = LoadPrefabRequired(
                $"{CliffDir}/Prefab_beach_cliff_01_ending_left_Sand_1.prefab", "cliff ending_left");
            var cliffA = LoadPrefabRequired(
                $"{CliffDir}/Prefab_beach_cliff_01_A_Sand_1.prefab", "cliff A");
            var cliffB = LoadPrefabRequired(
                $"{CliffDir}/Prefab_beach_cliff_01_B_Sand_1.prefab", "cliff B");
            var cliffC = LoadPrefabRequired(
                $"{CliffDir}/Prefab_beach_cliff_01_C_Sand_1.prefab", "cliff C");
            var cliffEndR = LoadPrefabRequired(
                $"{CliffDir}/Prefab_beach_cliff_01_ending_right_Sand_1.prefab", "cliff ending_right");

            var root = new GameObject("BeachCliffs");

            // Terrain Z extent; cliffs span 30%-70% of terrain Z
            var tData = terrain.terrainData;
            float terrainOriginZ = terrain.transform.position.z;
            float zSpanStart = terrainOriginZ + 0.30f * tData.size.z;
            float zSpanEnd   = terrainOriginZ + 0.70f * tData.size.z;
            float zTotal = zSpanEnd - zSpanStart;
            float spacing = zTotal / 4f; // 5 pieces at 0, 1, 2, 3, 4 * spacing

            // X position: ridge + slight inland pull so cliffs read as a wall behind the beach.
            // Face the beach: rotation Y=270 means +X faces toward -Z in Unity — we want
            // the cliff face to look toward the ocean (toward lower terrain Y).
            // Rotation 270 = face -X (west if ridge is on east side of terrain).
            float cliffX = ridgeWorldX;

            var sequence = new[]
            {
                (cliffEndL, 0f, "EndL"),
                (cliffA,    1f, "A"),
                (cliffB,    2f, "B"),
                (cliffC,    3f, "C"),
                (cliffEndR, 4f, "EndR"),
            };

            int midIdx = 0;
            Vector3 midpoint = Vector3.zero;

            foreach (var (prefab, step, label) in sequence)
            {
                float zPos = zSpanStart + step * spacing;
                float worldY = terrain.SampleHeight(new Vector3(cliffX, 0, zPos));
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                go.transform.position = new Vector3(cliffX, worldY, zPos);
                go.transform.rotation = Quaternion.Euler(0f, 270f, 0f);
                Log($"  cliff[{label}] at ({cliffX:F1}, {worldY:F1}, {zPos:F1})");

                if (label == "B") // midpoint between B and C
                {
                    float nextZ = zSpanStart + (step + 1f) * spacing;
                    midpoint = new Vector3(cliffX, worldY, (zPos + nextZ) * 0.5f);
                }
            }

            Log($"  cliff midpoint = {midpoint}");
            return midpoint;
        }

        private static GameObject LoadPrefabRequired(string path, string label)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new Exception($"Required prefab '{label}' not found at: {path}");
            return prefab;
        }

        // ── Step 8: cave ─────────────────────────────────────────────────────

        private static void PlaceCave(Vector3 cliffMidpoint, float ridgeWorldX)
        {
            var cavePrefab = LoadPrefabRequired(CavePrefabPath, "CavePart_1");

            // Pull cave 30m forward toward the beach (toward lower-X side, west of ridge)
            float caveX = ridgeWorldX - 30f;
            var caveGo = (GameObject)PrefabUtility.InstantiatePrefab(cavePrefab);
            caveGo.name = "CaveEntrance_NaturalCave2";
            caveGo.transform.position = new Vector3(caveX, cliffMidpoint.y, cliffMidpoint.z);
            caveGo.transform.rotation = Quaternion.Euler(0f, 270f, 0f);
            // Realistic Natural Cave 2's CavePart_1 is ~5-10m at scale 1; scale 5x reads as a 30m cave entrance against the 95m cliff.
            caveGo.transform.localScale = new Vector3(5f, 5f, 5f);
            Log($"  cave at ({caveX:F1}, {cliffMidpoint.y:F1}, {cliffMidpoint.z:F1}) scale=5x");
        }

        // ── Step 9: scatter trees ─────────────────────────────────────────────

        private static int ScatterTrees(
            Terrain terrain, float minTerrainY, float range, float seaLevel)
        {
            var tData = terrain.terrainData;

            // Load palm — non-throwing; log warn if missing
            var palm = AssetDatabase.LoadAssetAtPath<GameObject>(PalmPrefabPath);
            var pineA = AssetDatabase.LoadAssetAtPath<GameObject>(PineDirA);
            var pineShort = AssetDatabase.LoadAssetAtPath<GameObject>(PineDirShort);

            if (palm == null)
                Debug.LogWarning($"{LogPrefix} Palm prefab not found at {PalmPrefabPath} — skipping palms");
            if (pineA == null)
                Debug.LogWarning($"{LogPrefix} Beach pine A not found at {PineDirA} — skipping pineA");
            if (pineShort == null)
                Debug.LogWarning($"{LogPrefix} Beach pine short not found at {PineDirShort} — skipping pineShort");

            // Register prototypes
            var protos = new List<TreePrototype>(tData.treePrototypes);
            int palmIdx = -1, pineAIdx = -1, pineShortIdx = -1;

            if (palm != null)       { palmIdx = protos.Count;      protos.Add(new TreePrototype { prefab = palm }); }
            if (pineA != null)      { pineAIdx = protos.Count;     protos.Add(new TreePrototype { prefab = pineA }); }
            if (pineShort != null)  { pineShortIdx = protos.Count; protos.Add(new TreePrototype { prefab = pineShort }); }

            tData.treePrototypes = protos.ToArray();

            // Beach band: minTerrainY .. minTerrainY + 0.4*range, slope < 12°
            float maxBeachY = minTerrainY + 0.4f * range;
            float maxSlopeDeg = 12f;

            var instances = new List<TreeInstance>(tData.treeInstances);
            var rng = new System.Random(42);
            int placed = 0;

            // 80 trees total: ~35 palms, ~25 pineA, ~20 pineShort (or fewer if missing)
            int palmCount  = palm != null ? 35 : 0;
            int pineACount = pineA != null ? 25 : 0;
            int pineShortCount = pineShort != null ? 20 : 0;

            placed += ScatterTreeInstances(instances, tData, palmIdx, palmCount, seaLevel, maxBeachY, maxSlopeDeg, rng);
            placed += ScatterTreeInstances(instances, tData, pineAIdx, pineACount, seaLevel, maxBeachY, maxSlopeDeg, new System.Random(1001));
            placed += ScatterTreeInstances(instances, tData, pineShortIdx, pineShortCount, seaLevel, maxBeachY, maxSlopeDeg, new System.Random(2002));

            tData.treeInstances = instances.ToArray();
            return placed;
        }

        private static int ScatterTreeInstances(
            List<TreeInstance> list, TerrainData tData,
            int protoIdx, int count,
            float minY, float maxY, float maxSlopeDeg,
            System.Random rng)
        {
            if (protoIdx < 0 || count <= 0) return 0;
            int placed = 0, attempts = 0, maxAttempts = count * 30;
            while (placed < count && attempts++ < maxAttempts)
            {
                float nx = (float)rng.NextDouble();
                float nz = (float)rng.NextDouble();
                float worldY = tData.GetInterpolatedHeight(nx, nz);
                float slope  = tData.GetSteepness(nx, nz);
                if (worldY < minY || worldY > maxY || slope > maxSlopeDeg) continue;

                float scale = 0.8f + (float)rng.NextDouble() * 0.4f;
                list.Add(new TreeInstance
                {
                    prototypeIndex  = protoIdx,
                    position        = new Vector3(nx, worldY / tData.size.y, nz),
                    heightScale     = scale,
                    widthScale      = scale,
                    rotation        = (float)(rng.NextDouble() * Math.PI * 2),
                    color           = Color.white,
                    lightmapColor   = Color.white,
                });
                placed++;
            }
            return placed;
        }

        // ── Step 10: scatter detail prefabs ──────────────────────────────────

        private static int ScatterDetailPrefabs(
            Terrain terrain, float minTerrainY, float range, float seaLevel, Vector3 terrainOrigin)
        {
            var tData = terrain.terrainData;
            float maxBeachY = minTerrainY + 0.4f * range;
            float maxSlopeDeg = 15f;

            var detailRoot = new GameObject("BeachDetails");
            int placed = 0;

            // Build the pool: driftwood 01-09, shells A+B, beach grass 01/02/03 _1
            var detailPaths = new List<string>();

            // Driftwood: try each, skip gracefully if missing
            for (int i = 1; i <= 9; i++)
            {
                string p = $"{DetailDir}/Prefab_beach_driftwood_{i:D2}.prefab";
                if (File.Exists($"C:/Dev/Forever engine/{p}") ||
                    AssetDatabase.LoadAssetAtPath<GameObject>(p) != null)
                    detailPaths.Add(p);
            }
            // Shells
            foreach (var suf in new[] { "A", "B" })
            {
                string p = $"{DetailDir}/Prefab_shells_01_{suf}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(p) != null) detailPaths.Add(p);
            }
            // Beach grass
            foreach (var g in new[] {
                $"{GrassDir}/Prefab_beach_grass_01_1.prefab",
                $"{GrassDir}/Prefab_beach_grass_02_1.prefab",
                $"{GrassDir}/Prefab_beach_grass_03_1.prefab" })
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(g) != null) detailPaths.Add(g);
            }

            if (detailPaths.Count == 0)
            {
                Log("  WARN: no detail prefabs resolved — skipping detail scatter.");
                return 0;
            }

            // Load prefabs
            var prefabs = detailPaths
                .Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p))
                .Where(g => g != null)
                .ToArray();

            Log($"  detail prefab pool: {prefabs.Length} items from {detailPaths.Count} paths");

            var rng = new System.Random(777);
            int count = 60;
            int attempts = 0, maxAttempts = count * 30;
            while (placed < count && attempts++ < maxAttempts)
            {
                float nx = (float)rng.NextDouble();
                float nz = (float)rng.NextDouble();
                float worldY = tData.GetInterpolatedHeight(nx, nz);
                float slope  = tData.GetSteepness(nx, nz);
                if (worldY < seaLevel || worldY > maxBeachY || slope > maxSlopeDeg) continue;

                var prefab = prefabs[rng.Next(prefabs.Length)];
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, detailRoot.transform);
                go.transform.position = new Vector3(
                    terrainOrigin.x + nx * tData.size.x,
                    worldY,
                    terrainOrigin.z + nz * tData.size.z);
                go.transform.rotation = Quaternion.Euler(0, (float)(rng.NextDouble() * 360), 0);
                placed++;
            }

            Log($"  placed {placed} detail prefabs.");
            return placed;
        }

        // ── Step 11: Crest water ─────────────────────────────────────────────

        private static void SetupCrestWater(float seaLevel)
        {
            // Try prefab first
            GameObject waterGo = null;
            var prefabGuids = AssetDatabase.FindAssets("WaterRenderer t:Prefab");
            foreach (var g in prefabGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                if (!path.Contains("waveharmonic.crest", StringComparison.OrdinalIgnoreCase)) continue;
                var wPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (wPrefab != null)
                {
                    waterGo = (GameObject)PrefabUtility.InstantiatePrefab(wPrefab);
                    waterGo.transform.position = new Vector3(0f, seaLevel, 0f);
                    Log($"  Crest prefab instantiated from {path}");
                    break;
                }
            }

            if (waterGo == null)
            {
                Log("  no Crest prefab found — using reflective AddComponent");
                waterGo = new GameObject("Crest WaterRenderer");
                waterGo.transform.position = new Vector3(0f, seaLevel, 0f);
                var crestAsm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name.Contains("Crest", StringComparison.OrdinalIgnoreCase));
                if (crestAsm == null)
                {
                    Log("  WARN: Crest assembly not found — skipping water");
                    UnityEngine.Object.DestroyImmediate(waterGo);
                    waterGo = null;
                }
                else
                {
                    var wrType = crestAsm.GetType("WaveHarmonic.Crest.WaterRenderer");
                    if (wrType != null) waterGo.AddComponent(wrType);
                    else Log("  WARN: WaterRenderer type not in Crest assembly — water inert");
                }
            }

            if (waterGo != null)
            {
                // Attach WaterCamera to main camera
                var mainCam = Camera.main;
                if (mainCam != null)
                {
                    var crestAsm = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name.Contains("Crest", StringComparison.OrdinalIgnoreCase));
                    var wcType = crestAsm?.GetType("WaveHarmonic.Crest.WaterCamera");
                    if (wcType != null && mainCam.GetComponent(wcType) == null)
                        mainCam.gameObject.AddComponent(wcType);
                }
                Log($"  Crest water at sea level Y={seaLevel:F2}");
            }
        }

        // ── Step 12: main camera ─────────────────────────────────────────────

        private static void EnsureCamera(float seaLevel)
        {
            var existingCam = GameObject.Find("Main Camera");
            if (existingCam != null) UnityEngine.Object.DestroyImmediate(existingCam);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 5000f;
            camGo.AddComponent<AudioListener>();

            // Ground-level beach view — camera 150m WEST of the cliff at moderate elevation,
            // looking east directly at the cliff face where the cave is embedded. Cliff is at
            // world X=491, cave at (461, 87, 50). Position at (300, 30, 50) → cliff is 191m
            // east in the camera's forward direction, cave entrance ~57m above camera.
            // Pitch -15° looks up to frame the cave; no yaw drift.
            camGo.transform.position = new Vector3(300f, 30f, 50f);
            camGo.transform.rotation = Quaternion.Euler(-15f, 90f, 0f);
            Log("  Main Camera at (300, 30, 50) rot=(-15,90,0) — beach-level looking east at cliff/cave");
        }

        // ── Util ─────────────────────────────────────────────────────────────

        private static void Log(string msg) => Debug.Log($"{LogPrefix} {msg}");
    }
}
#endif
