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
            var weapons = AssetDatabase.FindAssets("t:WeaponData", new[] { WeaponDir });
            var armor = AssetDatabase.FindAssets("t:ArmorData", new[] { ArmorDir });

            Debug.Log($"[ContentGenerator] Generated {classes.Length} ClassData assets");
            Debug.Log($"[ContentGenerator] Generated {species.Length} SpeciesData assets");
            Debug.Log($"[ContentGenerator] Generated {spells.Length} SpellData assets");
            Debug.Log($"[ContentGenerator] Generated {weapons.Length} WeaponData assets");
            Debug.Log($"[ContentGenerator] Generated {armor.Length} ArmorData assets");

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
            if (weapons.Length < 100)
            {
                Debug.LogError($"[ContentGenerator] Expected 100+ weapons, found {weapons.Length}");
                errors++;
            }
            if (armor.Length < 50)
            {
                Debug.LogError($"[ContentGenerator] Expected 50+ armor, found {armor.Length}");
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

            int total = classes.Length + species.Length + spells.Length + weapons.Length + armor.Length;
            Debug.Log($"[ContentGenerator] Validation: {total} total assets, {errors} errors");
        }
    }
}
