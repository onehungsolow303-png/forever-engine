using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ForeverEngine.Procedural.Editor
{
    public enum PrefabCategory { Tree, Rock, Bush, Structure }

    public static class PackPrefabHarvester
    {
        public static PrefabCategory Classify(string prefabFileName)
        {
            var n = prefabFileName.ToLowerInvariant();
            if (n.Contains("tree") || n.Contains("pine") || n.Contains("birch") || n.Contains("oak") ||
                n.Contains("fir") || n.Contains("spruce") || n.Contains("palm"))
                return PrefabCategory.Tree;
            if (n.Contains("rock") || n.Contains("boulder") || n.Contains("stone") || n.Contains("cliff"))
                return PrefabCategory.Rock;
            if (n.Contains("bush") || n.Contains("shrub") || n.Contains("fern") || n.Contains("grass") ||
                n.Contains("plant"))
                return PrefabCategory.Bush;
            return PrefabCategory.Structure;
        }

        public static void Harvest(string packAbsPath, AssetPackBiomeEntry entry)
        {
            var trees   = new List<GameObject>();
            var rocks   = new List<GameObject>();
            var bushes  = new List<GameObject>();
            var structs = new List<GameObject>();
            var mats    = new List<Material>();
            var audio   = new List<AudioClip>();

            foreach (var prefabPath in Directory.GetFiles(packAbsPath, "*.prefab", SearchOption.AllDirectories))
            {
                var rel = "Assets" + prefabPath.Replace(Application.dataPath, "").Replace('\\', '/');
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(rel);
                if (go == null) continue;
                switch (Classify(Path.GetFileName(prefabPath)))
                {
                    case PrefabCategory.Tree: trees.Add(go); break;
                    case PrefabCategory.Rock: rocks.Add(go); break;
                    case PrefabCategory.Bush: bushes.Add(go); break;
                    case PrefabCategory.Structure: structs.Add(go); break;
                }
            }

            foreach (var matPath in Directory.GetFiles(packAbsPath, "*.mat", SearchOption.AllDirectories))
            {
                var rel = "Assets" + matPath.Replace(Application.dataPath, "").Replace('\\', '/');
                var m = AssetDatabase.LoadAssetAtPath<Material>(rel);
                if (m != null) mats.Add(m);
            }

            foreach (var wavPath in Directory.GetFiles(packAbsPath, "*.wav", SearchOption.AllDirectories))
            {
                var rel = "Assets" + wavPath.Replace(Application.dataPath, "").Replace('\\', '/');
                var a = AssetDatabase.LoadAssetAtPath<AudioClip>(rel);
                if (a != null) audio.Add(a);
            }

            entry.TreePrefabs      = trees.ToArray();
            entry.RockPrefabs      = rocks.ToArray();
            entry.BushPrefabs      = bushes.ToArray();
            entry.StructurePrefabs = structs.ToArray();
            entry.TerrainMaterials = mats.ToArray();
            entry.AmbientAudio     = audio.ToArray();
        }
    }
}
