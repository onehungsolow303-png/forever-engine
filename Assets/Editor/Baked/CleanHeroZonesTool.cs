using System.IO;
using UnityEditor;
using UnityEngine;

namespace ForeverEngine.Procedural.Editor
{
    /// <summary>
    /// Deletes every hero-zone override under .shared/baked/planet/layer_0/hero/
    /// and resets manifest.json to an empty array. Run this before baking a fresh
    /// Gaia-authored macro so leftover synthetic test zones don't punch holes
    /// through the new heightmap.
    /// </summary>
    public static class CleanHeroZonesTool
    {
        private const string HeroRoot = "C:/Dev/.shared/baked/planet/layer_0/hero";

        [MenuItem("Forever Engine/Bake/Clean Hero Zones")]
        public static void Clean()
        {
            if (!Directory.Exists(HeroRoot))
            {
                Debug.Log($"[CleanHeroZones] Nothing to clean — {HeroRoot} doesn't exist.");
                return;
            }

            int deleted = 0;
            foreach (var dir in Directory.GetDirectories(HeroRoot))
            {
                Directory.Delete(dir, recursive: true);
                deleted++;
            }

            File.WriteAllText(Path.Combine(HeroRoot, "manifest.json"), "[]");
            Debug.Log($"[CleanHeroZones] Removed {deleted} zone(s); manifest reset to [].");
            EditorUtility.DisplayDialog("Clean Hero Zones",
                $"Removed {deleted} hero zone(s) from\n{HeroRoot}", "OK");
        }
    }
}
