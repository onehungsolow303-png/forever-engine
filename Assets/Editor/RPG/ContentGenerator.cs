using UnityEditor;
using UnityEngine;

namespace ForeverEngine.Editor.RPG
{
    public static class ContentGenerator
    {
        private const string ClassDir = "Assets/Resources/RPG/Content/Classes";
        private const string SpeciesDir = "Assets/Resources/RPG/Content/Species";
        private const string SpellDir = "Assets/Resources/RPG/Content/Spells";
        private const string WeaponDir = "Assets/Resources/RPG/Content/Weapons";
        private const string ArmorDir = "Assets/Resources/RPG/Content/Armor";

        [MenuItem("Forever Engine/RPG/Generate All Content")]
        public static void GenerateAll()
        {
            Debug.Log("[ContentGenerator] Starting full content generation...");

            EnsureDirectories();

            SpellGenerator.GenerateAll();
            ClassGenerator.GenerateAll();
            SpeciesGenerator.GenerateAll();
            EquipmentGenerator.GenerateAll();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Validate();

            Debug.Log("[ContentGenerator] Content generation complete.");
        }

        private static void EnsureDirectories()
        {
            EnsureFolder("Assets/Resources", "RPG");
            EnsureFolder("Assets/Resources/RPG", "Content");
            EnsureFolder("Assets/Resources/RPG/Content", "Classes");
            EnsureFolder("Assets/Resources/RPG/Content", "Species");
            EnsureFolder("Assets/Resources/RPG/Content", "Spells");
            EnsureFolder("Assets/Resources/RPG/Content", "Weapons");
            EnsureFolder("Assets/Resources/RPG/Content", "Armor");
        }

        private static void EnsureFolder(string parent, string name)
        {
            string full = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(full))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static void Validate()
        {
            int errors = 0;

            var classes = AssetDatabase.FindAssets("t:ClassData", new[] { ClassDir });
            var species = AssetDatabase.FindAssets("t:SpeciesData", new[] { SpeciesDir });
            var spells = AssetDatabase.FindAssets("t:SpellData", new[] { SpellDir });
            // Use file counting for equipment — FindAssets can miss newly-created types in batch mode
            int weaponCount = System.IO.Directory.Exists(WeaponDir)
                ? System.IO.Directory.GetFiles(WeaponDir, "*.asset").Length : 0;
            int armorCount = System.IO.Directory.Exists(ArmorDir)
                ? System.IO.Directory.GetFiles(ArmorDir, "*.asset").Length : 0;

            Debug.Log($"[ContentGenerator] Generated {classes.Length} ClassData assets");
            Debug.Log($"[ContentGenerator] Generated {species.Length} SpeciesData assets");
            Debug.Log($"[ContentGenerator] Generated {spells.Length} SpellData assets");
            Debug.Log($"[ContentGenerator] Generated {weaponCount} WeaponData assets");
            Debug.Log($"[ContentGenerator] Generated {armorCount} ArmorData assets");

            if (classes.Length != 12)
            {
                Debug.LogError($"[ContentGenerator] Expected 12 classes, found {classes.Length}");
                errors++;
            }
            if (species.Length != 15)
            {
                Debug.LogError($"[ContentGenerator] Expected 15 species, found {species.Length}");
                errors++;
            }
            if (spells.Length != 205)
            {
                Debug.LogError($"[ContentGenerator] Expected 205 spells, found {spells.Length}");
                errors++;
            }
            if (weaponCount < 100)
            {
                Debug.LogError($"[ContentGenerator] Expected 100+ weapons, found {weaponCount}");
                errors++;
            }
            if (armorCount < 50)
            {
                Debug.LogError($"[ContentGenerator] Expected 50+ armor, found {armorCount}");
                errors++;
            }

            // Validate class progressions
            foreach (string guid in classes)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var cd = AssetDatabase.LoadAssetAtPath<ForeverEngine.RPG.Character.ClassData>(path);
                if (cd == null) { Debug.LogError($"[ContentGenerator] Null class at {path}"); errors++; continue; }
                if (cd.Progression == null || cd.Progression.Length != 20)
                {
                    Debug.LogError($"[ContentGenerator] {cd.Name} has {cd.Progression?.Length ?? 0} progression entries (expected 20)");
                    errors++;
                }
            }

            // Validate species innate spell references
            foreach (string guid in species)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var sd = AssetDatabase.LoadAssetAtPath<ForeverEngine.RPG.Character.SpeciesData>(path);
                if (sd == null) { Debug.LogError($"[ContentGenerator] Null species at {path}"); errors++; continue; }
                if (sd.InnateSpells != null)
                {
                    for (int i = 0; i < sd.InnateSpells.Length; i++)
                    {
                        if (sd.InnateSpells[i] == null)
                        {
                            Debug.LogError($"[ContentGenerator] {sd.Name} has null innate spell at index {i}");
                            errors++;
                        }
                    }
                }
            }

            int total = classes.Length + species.Length + spells.Length + weaponCount + armorCount;
            Debug.Log($"[ContentGenerator] Validation: {total} total assets, {errors} errors");
        }
    }
}
