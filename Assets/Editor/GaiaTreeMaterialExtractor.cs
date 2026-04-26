#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ForeverEngine.Editor.Gaia
{
    /// <summary>
    /// Batchmode entry that fixes the Coniferous Forest "tree couldn't be
    /// instanced because one of the materials is missing" cascade.
    ///
    /// Root cause (diagnosed 2026-04-25): the PW_Tree_Pine and PW_Tree_Spruce
    /// FBX files have `recycleNameMap` entries pointing to Material assets
    /// (e.g. PW_Tree_Hero_Pine_01_Ending.mat with GUID 3eae9beb...) that don't
    /// exist on disk. The FBX importer was set to "Use External Materials"
    /// but those external .mat files were never produced by the import. The
    /// tree prefab references the GUID, the GUID resolves to nothing, Unity
    /// rejects the tree at instance time → 0 tree instances on every Gaia
    /// terrain.
    ///
    /// Fix: AssetDatabase.ExtractMaterials walks the FBX's embedded materials
    /// and writes them out as standalone .mat assets next to the FBX. The
    /// extraction reuses existing materials in the same folder by name when
    /// found, so subsequent runs are no-ops.
    ///
    /// Invoke:
    ///   Unity.exe -batchmode -projectPath "C:/Dev/Forever engine" \
    ///     -executeMethod ForeverEngine.Editor.Gaia.GaiaTreeMaterialExtractor.ExtractAllConiferousFbx \
    ///     -quit -logFile "C:/Dev/extract-mats.log"
    /// </summary>
    public static class GaiaTreeMaterialExtractor
    {
        // The 8 FBX files the Coniferous Forest biome's tree spawners reference.
        // Each one has multiple internal materials that the importer is supposed
        // to extract. Confirmed via grep of `couldn't be instanced` errors in
        // the gaia-headless.log from 2026-04-25.
        private static readonly string[] TreeFbxPaths = new[]
        {
            "Assets/Procedural Worlds/Packages - Install/Gaia Pro Assets and Biomes/Content Resources/Trees/PW_Tree_Pine_Hero/PW_Tree_Pine_01 Hero.fbx",
            "Assets/Procedural Worlds/Packages - Install/Gaia Pro Assets and Biomes/Content Resources/Trees/PW_Tree_Pine_Hero/PW_Tree_Pine_02_Hero.fbx",
            "Assets/Procedural Worlds/Packages - Install/Gaia Pro Assets and Biomes/Content Resources/Trees/PW_Tree_Pine_Hero/PW_Tree_Pine_03 Hero.fbx",
            "Assets/Procedural Worlds/Packages - Install/Gaia Pro Assets and Biomes/Content Resources/Trees/PW_Tree_Spruce/PW_Tree_Spruce_01_Hero.fbx",
            "Assets/Procedural Worlds/Packages - Install/Gaia Pro Assets and Biomes/Content Resources/Trees/PW_Tree_Spruce/PW_Tree_Spruce_02 Hero.fbx",
            "Assets/Procedural Worlds/Packages - Install/Gaia Pro Assets and Biomes/Content Resources/Trees/PW_Tree_Spruce/PW_Tree_Spruce_04 Hero.fbx",
            "Assets/Procedural Worlds/Packages - Install/Gaia Pro Assets and Biomes/Content Resources/Trees/PW_Tree_Spruce/PW_Tree_Spruce_06 Hero.fbx",
            "Assets/Procedural Worlds/Packages - Install/Gaia Pro Assets and Biomes/Content Resources/Trees/PW_Tree_Spruce/PW_Tree_Spruce_07_Hero.fbx",
        };

        public static void ExtractAllConiferousFbx()
        {
            try
            {
                int totalExtracted = 0;
                int totalFbx = 0;
                int totalErrors = 0;

                foreach (var fbxPath in TreeFbxPaths)
                {
                    if (!File.Exists(fbxPath))
                    {
                        Debug.LogWarning($"[ExtractMats] FBX missing: {fbxPath}");
                        continue;
                    }

                    totalFbx++;
                    var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
                    if (importer == null)
                    {
                        Debug.LogWarning($"[ExtractMats] Not a ModelImporter: {fbxPath}");
                        totalErrors++;
                        continue;
                    }

                    // Force "Use External Materials (Legacy)" so the importer
                    // expects standalone .mat files. Then ExtractMaterials
                    // creates them.
                    if (importer.materialImportMode != ModelImporterMaterialImportMode.ImportStandard)
                    {
                        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                        importer.materialLocation = ModelImporterMaterialLocation.External;
                        importer.SaveAndReimport();
                    }

                    var folder = Path.GetDirectoryName(fbxPath).Replace('\\', '/');
                    int beforeCount = CountMatsIn(folder);

                    // ExtractMaterials returns a list of error strings; empty = success.
                    var errors = ExtractMaterialsFromFbx(fbxPath, folder);
                    if (errors.Count > 0)
                    {
                        Debug.LogWarning($"[ExtractMats] {Path.GetFileName(fbxPath)} extraction warnings:");
                        foreach (var err in errors) Debug.LogWarning($"    {err}");
                    }

                    AssetDatabase.Refresh();
                    int afterCount = CountMatsIn(folder);
                    int newMats = afterCount - beforeCount;
                    totalExtracted += newMats;
                    Debug.Log($"[ExtractMats] {Path.GetFileName(fbxPath)}: +{newMats} new materials (folder now has {afterCount})");
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"[ExtractMats] DONE — {totalFbx} FBX processed, {totalExtracted} new materials extracted, {totalErrors} errors.");
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ExtractMats] FAIL: {ex}");
                EditorApplication.Exit(1);
            }
        }

        // Mirrors what Unity's "Materials > Extract Materials..." button does:
        // for each Material sub-asset of the FBX, if no asset of the same name
        // exists in the destination folder, write one. Then re-link the
        // FBX importer's external_objects map to point at it.
        private static List<string> ExtractMaterialsFromFbx(string fbxPath, string destFolder)
        {
            var errors = new List<string>();
            var importer = AssetImporter.GetAtPath(fbxPath);
            if (importer == null)
            {
                errors.Add("AssetImporter null");
                return errors;
            }

            var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (var asset in assets)
            {
                if (asset is Material mat)
                {
                    string matPath = $"{destFolder}/{mat.name}.mat";
                    // Skip if the .mat already exists at destination.
                    if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null)
                        continue;

                    // ExtractAsset moves the embedded material to its own .mat file.
                    string extractError = AssetDatabase.ExtractAsset(mat, matPath);
                    if (!string.IsNullOrEmpty(extractError))
                        errors.Add($"{mat.name}: {extractError}");
                }
            }

            // Re-import so the FBX picks up the new external materials.
            AssetDatabase.WriteImportSettingsIfDirty(fbxPath);
            AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate);
            return errors;
        }

        private static int CountMatsIn(string folder)
        {
            if (!Directory.Exists(folder)) return 0;
            return Directory.GetFiles(folder, "*.mat").Length;
        }
    }
}
#endif
