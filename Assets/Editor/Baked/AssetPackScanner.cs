using System.Collections.Generic;
using System.IO;

namespace ForeverEngine.Procedural.Editor
{
    public readonly struct DiscoveredPack
    {
        public readonly string Name;
        public readonly string AbsolutePath;
        public readonly BiomeType[] SuggestedBiomes;

        public DiscoveredPack(string name, string path, BiomeType[] suggested)
        {
            Name = name; AbsolutePath = path; SuggestedBiomes = suggested;
        }
    }

    /// <summary>
    /// Discovers pack folders under a root directory. Skips Unity-internal
    /// folders (Plugins, Editor, Scripts, Tests, Resources) and anything
    /// starting with `_`. Each remaining top-level folder is treated as a pack.
    /// </summary>
    public static class AssetPackScanner
    {
        private static readonly HashSet<string> SkipFolders = new(System.StringComparer.OrdinalIgnoreCase)
        {
            "Plugins", "Editor", "Scripts", "Tests", "Resources",
            "StreamingAssets", "Settings", "TutorialInfo", "Packages"
        };

        public static DiscoveredPack[] ScanRoot(string rootDir)
        {
            if (!Directory.Exists(rootDir)) return System.Array.Empty<DiscoveredPack>();
            var packs = new List<DiscoveredPack>();
            foreach (var dir in Directory.GetDirectories(rootDir))
            {
                var name = Path.GetFileName(dir);
                if (SkipFolders.Contains(name)) continue;
                if (name.StartsWith("_")) continue;
                var suggested = PackBiomeHeuristics.SuggestBiomes(name);
                packs.Add(new DiscoveredPack(name, dir, suggested));
            }
            return packs.ToArray();
        }
    }
}
