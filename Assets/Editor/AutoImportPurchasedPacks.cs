using UnityEditor;
using UnityEngine;
using System.IO;

/// <summary>
/// One-shot auto-importer for the 7 purchased Unity Asset Store packs.
/// Runs once on script compilation via [InitializeOnLoadMethod], imports
/// all packages silently (no interactive dialog), then creates a marker
/// file so it never re-runs.
///
/// SAFE TO DELETE after all packages are imported — it's scaffolding,
/// not permanent infrastructure. The marker file at
/// Assets/Editor/.purchased_packs_imported prevents re-import even if
/// this script stays in the project.
///
/// Created autonomously while the user is at work. The packages were
/// already downloaded to the Unity Asset Store cache by Package Manager.
/// </summary>
public static class AutoImportPurchasedPacks
{
    private const string MarkerPath = "Assets/Editor/.purchased_packs_imported";

    private static readonly string[] PackagePaths = new[]
    {
        // Mana Station packs
        "C:/Users/bp303/AppData/Roaming/Unity/Asset Store-5.x/Mana Station/3D ModelsEnvironmentsDungeons/Multistory Dungeons.unitypackage",
        "C:/Users/bp303/AppData/Roaming/Unity/Asset Store-5.x/Mana Station/3D ModelsEnvironmentsFantasy/Eternal Temple.unitypackage",
        "C:/Users/bp303/AppData/Roaming/Unity/Asset Store-5.x/Mana Station/3D ModelsEnvironmentsFantasy/Lordenfel Castles Dungeons RPG pack.unitypackage",

        // Naked Singularity Studio
        "C:/Users/bp303/AppData/Roaming/Unity/Asset Store-5.x/Naked Singularity Studio/3D ModelsEnvironmentsDungeons/Dungeon Catacomb.unitypackage",

        // NatureManufacture
        "C:/Users/bp303/AppData/Roaming/Unity/Asset Store-5.x/NatureManufacture/3D ModelsVegetation/Forest Environment - Dynamic Nature.unitypackage",
        "C:/Users/bp303/AppData/Roaming/Unity/Asset Store-5.x/NatureManufacture/3D ModelsVegetation/Mountain Environment - Dynamic Nature.unitypackage",

        // Magic Pig Games
        "C:/Users/bp303/AppData/Roaming/Unity/Asset Store-5.x/Magic Pig Games Infinity PBR/3D ModelsEnvironmentsFantasy/Medieval Fantasy Town Village Environment for RPG FPS.unitypackage",
    };

    /// <summary>
    /// Call from batchmode via:
    ///   Unity.exe -batchmode -projectPath "..." -executeMethod AutoImportPurchasedPacks.ImportAll
    /// Does NOT use -quit — the method calls EditorApplication.Exit(0) after
    /// all imports + AssetDatabase.Refresh() have completed, ensuring the
    /// extraction finishes before Unity shuts down.
    /// </summary>
    public static void ImportAll()
    {
        OnScriptsReloaded();
        // Force a synchronous asset database refresh so all extracted
        // files are written to disk before we exit.
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("[AutoImportPurchasedPacks] Refresh complete. Exiting.");
        EditorApplication.Exit(0);
    }

    [InitializeOnLoadMethod]
    private static void OnScriptsReloaded()
    {
        // Only run once — check for the marker file
        if (File.Exists(MarkerPath))
        {
            return;
        }

        Debug.Log("[AutoImportPurchasedPacks] Starting import of 7 purchased packs...");

        int imported = 0;
        int skipped = 0;
        int failed = 0;

        foreach (var path in PackagePaths)
        {
            string name = Path.GetFileNameWithoutExtension(path);

            if (!File.Exists(path))
            {
                Debug.LogWarning($"[AutoImportPurchasedPacks] SKIP (not found): {name}\n  path: {path}");
                skipped++;
                continue;
            }

            try
            {
                Debug.Log($"[AutoImportPurchasedPacks] Importing: {name}...");
                // interactive=false → silent import, no dialog
                AssetDatabase.ImportPackage(path, false);
                imported++;
                Debug.Log($"[AutoImportPurchasedPacks] OK: {name}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AutoImportPurchasedPacks] FAIL: {name}: {e.Message}");
                failed++;
            }
        }

        Debug.Log($"[AutoImportPurchasedPacks] DONE: {imported} imported, {skipped} skipped, {failed} failed");

        // Create the marker file so we never re-run
        try
        {
            File.WriteAllText(MarkerPath, $"Imported {imported} packs on {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
            // Also refresh the asset database so Unity sees the marker
            AssetDatabase.Refresh();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[AutoImportPurchasedPacks] Failed to write marker: {e.Message}");
        }
    }
}
