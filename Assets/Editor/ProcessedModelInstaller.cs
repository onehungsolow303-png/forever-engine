#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Replaces raw GLBs in Resources/Models/ with Blender-processed versions from
/// GeneratedModels/. The processed models have proper PBR materials and semantic
/// parts. The originals in GeneratedModels/ are never modified — the upgrade is
/// always reversible by re-importing the pack assets.
///
/// Menu: Forever Engine → Install Processed Models
/// </summary>
public static class ProcessedModelInstaller
{
    [MenuItem("Forever Engine/Install Processed Models")]
    public static void Install()
    {
        int upgraded = 0;

        upgraded += InstallDir(
            "Assets/GeneratedModels/Monsters",
            "Assets/Resources/Models/Monsters");

        upgraded += InstallDir(
            "Assets/GeneratedModels/NPCs",
            "Assets/Resources/Models/NPCs");

        AssetDatabase.Refresh();
        Debug.Log($"[ProcessedModelInstaller] Upgraded {upgraded} model(s) with Blender-processed versions.");
    }

    /// <summary>
    /// Scans <paramref name="sourceBase"/> for processed GLBs (both inside
    /// named subdirectories and loose at the root) and copies each one over
    /// the matching file in <paramref name="targetBase"/>, preserving the
    /// existing .meta file so Unity keeps its import settings and GUIDs.
    /// </summary>
    private static int InstallDir(string sourceBase, string targetBase)
    {
        int count = 0;

        if (!Directory.Exists(sourceBase))
        {
            Debug.LogWarning($"[ProcessedModelInstaller] Source directory not found: {sourceBase}");
            return 0;
        }

        if (!Directory.Exists(targetBase))
        {
            Debug.LogWarning($"[ProcessedModelInstaller] Target directory not found: {targetBase}");
            return 0;
        }

        // ── 1. Subdirectory layout: GeneratedModels/Monsters/Giant Rat/Giant Rat.glb ──
        foreach (string modelDir in Directory.GetDirectories(sourceBase))
        {
            string modelName = Path.GetFileName(modelDir);
            string[] glbs = Directory.GetFiles(modelDir, "*.glb");
            if (glbs.Length == 0) continue;

            // Prefer the GLB whose stem matches the directory name; fall back to first.
            string processedGlb = glbs[0];
            foreach (string g in glbs)
            {
                if (Path.GetFileNameWithoutExtension(g) == modelName)
                {
                    processedGlb = g;
                    break;
                }
            }

            count += TryCopyGlb(processedGlb, targetBase);
        }

        // ── 2. Loose GLB layout: GeneratedModels/NPCs/Dwarf male cleric.glb ──
        foreach (string looseGlb in Directory.GetFiles(sourceBase, "*.glb"))
        {
            count += TryCopyGlb(looseGlb, targetBase);
        }

        return count;
    }

    /// <summary>
    /// Copies <paramref name="sourceGlb"/> into <paramref name="targetDir"/>,
    /// overwriting the existing file. The .meta file is left untouched so Unity
    /// retains the original GUID and import settings.
    /// Returns 1 if a file was upgraded, 0 if no matching target existed.
    /// </summary>
    private static int TryCopyGlb(string sourceGlb, string targetDir)
    {
        string fileName = Path.GetFileName(sourceGlb);
        string targetGlb = Path.Combine(targetDir, fileName);

        if (!File.Exists(targetGlb))
        {
            // No raw GLB to upgrade — skip silently.
            return 0;
        }

        File.Copy(sourceGlb, targetGlb, overwrite: true);
        Debug.Log($"[ProcessedModelInstaller] Upgraded: {targetGlb}");
        return 1;
    }
}
#endif
