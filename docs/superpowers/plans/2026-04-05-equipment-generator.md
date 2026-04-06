# Equipment Generator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Generate ~189 weapon and armor ScriptableObject assets (base D&D 5e SRD + magic variants + named magic items) following the established generator pattern.

**Architecture:** Single `EquipmentGenerator.cs` in `Assets/Editor/RPG/` with factory helpers for each weapon/armor category. Integrates into the existing `ContentGenerator` orchestrator. Assets output to `Assets/Resources/RPG/Content/Weapons/` and `Assets/Resources/RPG/Content/Armor/`.

**Tech Stack:** Unity Editor scripting (AssetDatabase), ScriptableObject (WeaponData, ArmorData), existing RPG enum types (DieType, DamageType, WeaponProperty, ArmorType, Rarity).

**Spec:** `docs/superpowers/specs/2026-04-05-equipment-generator-design.md`

---

## File Structure

| Action | File | Responsibility |
|--------|------|----------------|
| Create | `Assets/Editor/RPG/EquipmentGenerator.cs` | All weapon + armor asset generation |
| Modify | `Assets/Editor/RPG/ContentGenerator.cs` | Add equipment call + directories + validation |
| Modify | `Assets/Scripts/Demo/RPGBridge.cs` | Remove TODO comments, assets now exist |

---

### Task 1: EquipmentGenerator Scaffold + Simple Melee Weapons

**Files:**
- Create: `Assets/Editor/RPG/EquipmentGenerator.cs`

- [ ] **Step 1: Create EquipmentGenerator.cs with scaffold and weapon helpers**

```csharp
using UnityEditor;
using UnityEngine;
using ForeverEngine.RPG.Enums;
using ForeverEngine.RPG.Items;

namespace ForeverEngine.Editor.RPG
{
    public static class EquipmentGenerator
    {
        private const string WeaponDir = "Assets/Resources/RPG/Content/Weapons";
        private const string ArmorDir = "Assets/Resources/RPG/Content/Armor";

        // Shorthand aliases
        private const WeaponProperty Fin = WeaponProperty.Finesse;
        private const WeaponProperty Hvy = WeaponProperty.Heavy;
        private const WeaponProperty Lgt = WeaponProperty.Light;
        private const WeaponProperty Thr = WeaponProperty.Thrown;
        private const WeaponProperty Two = WeaponProperty.TwoHanded;
        private const WeaponProperty Ver = WeaponProperty.Versatile;
        private const WeaponProperty Rch = WeaponProperty.Reach;
        private const WeaponProperty Lod = WeaponProperty.Loading;
        private const WeaponProperty Amm = WeaponProperty.Ammunition;

        [MenuItem("Forever Engine/RPG/Generate Equipment")]
        public static void GenerateAll()
        {
            Debug.Log("[EquipmentGenerator] Generating weapons and armor...");
            EnsureDirectories();
            DeleteExisting();

            int wCount = 0;
            int aCount = 0;

            GenerateBaseWeapons(ref wCount);
            GenerateMagicWeaponVariants(ref wCount);
            GenerateNamedMagicWeapons(ref wCount);

            GenerateBaseArmor(ref aCount);
            GenerateMagicArmorVariants(ref aCount);
            GenerateNamedMagicArmor(ref aCount);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[EquipmentGenerator] Generated {wCount} weapons, {aCount} armor ({wCount + aCount} total)");
        }

        private static void EnsureDirectories()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources/RPG/Content/Weapons"))
                AssetDatabase.CreateFolder("Assets/Resources/RPG/Content", "Weapons");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/RPG/Content/Armor"))
                AssetDatabase.CreateFolder("Assets/Resources/RPG/Content", "Armor");
        }

        private static void DeleteExisting()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:WeaponData", new[] { WeaponDir }))
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
            foreach (string guid in AssetDatabase.FindAssets("t:ArmorData", new[] { ArmorDir }))
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
        }

        // =====================================================================
        // WEAPON FACTORY HELPERS
        // =====================================================================

        private static WeaponData Melee(string id, string name, int diceCount, DieType die,
            DamageType dmgType, WeaponProperty props, string group)
        {
            var w = ScriptableObject.CreateInstance<WeaponData>();
            w.Id = id;
            w.Name = name;
            w.DamageDiceCount = diceCount;
            w.DamageDie = die;
            w.DamageBonus = 0;
            w.Type = dmgType;
            w.Properties = props;
            w.ProficiencyGroup = group;
            w.NormalRange = 0;
            w.LongRange = 0;
            w.VersatileDiceCount = 0;
            w.VersatileDie = DieType.D4;
            w.MagicBonus = 0;
            w.Rarity = Rarity.Common;
            return w;
        }

        private static WeaponData Ranged(string id, string name, int diceCount, DieType die,
            DamageType dmgType, int normalRange, int longRange, WeaponProperty props, string group)
        {
            var w = Melee(id, name, diceCount, die, dmgType, props, group);
            w.NormalRange = normalRange;
            w.LongRange = longRange;
            return w;
        }

        private static WeaponData Versatile(string id, string name, int diceCount, DieType die,
            int versDiceCount, DieType versDie, DamageType dmgType, WeaponProperty props, string group)
        {
            var w = Melee(id, name, diceCount, die, dmgType, props | Ver, group);
            w.VersatileDiceCount = versDiceCount;
            w.VersatileDie = versDie;
            return w;
        }

        private static WeaponData MagicWeapon(WeaponData baseW, int bonus)
        {
            var w = ScriptableObject.CreateInstance<WeaponData>();
            w.Id = baseW.Id + "_plus_" + bonus;
            w.Name = baseW.Name + " +" + bonus;
            w.DamageDiceCount = baseW.DamageDiceCount;
            w.DamageDie = baseW.DamageDie;
            w.DamageBonus = 0;
            w.Type = baseW.Type;
            w.Properties = baseW.Properties;
            w.ProficiencyGroup = baseW.ProficiencyGroup;
            w.NormalRange = baseW.NormalRange;
            w.LongRange = baseW.LongRange;
            w.VersatileDiceCount = baseW.VersatileDiceCount;
            w.VersatileDie = baseW.VersatileDie;
            w.MagicBonus = bonus;
            w.Rarity = bonus == 1 ? Rarity.Uncommon : bonus == 2 ? Rarity.Rare : Rarity.VeryRare;
            return w;
        }

        private static void SaveW(ref int count, WeaponData w)
        {
            AssetDatabase.CreateAsset(w, $"{WeaponDir}/{w.Id}.asset");
            count++;
        }

        // =====================================================================
        // ARMOR FACTORY HELPERS
        // =====================================================================

        private static ArmorData MakeArmor(string id, string name, int baseAC, ArmorType type,
            bool stealthDisadv, int strReq)
        {
            var a = ScriptableObject.CreateInstance<ArmorData>();
            a.Id = id;
            a.Name = name;
            a.BaseAC = baseAC;
            a.Type = type;
            a.StealthDisadvantage = stealthDisadv;
            a.StrengthRequirement = strReq;
            a.MagicBonus = 0;
            a.Rarity = Rarity.Common;
            return a;
        }

        private static ArmorData MagicArmor(ArmorData baseA, int bonus)
        {
            var a = ScriptableObject.CreateInstance<ArmorData>();
            a.Id = baseA.Id + "_plus_" + bonus;
            a.Name = baseA.Name + " +" + bonus;
            a.BaseAC = baseA.BaseAC;
            a.Type = baseA.Type;
            a.StealthDisadvantage = baseA.StealthDisadvantage;
            a.StrengthRequirement = baseA.StrengthRequirement;
            a.MagicBonus = bonus;
            a.Rarity = bonus == 1 ? Rarity.Uncommon : bonus == 2 ? Rarity.Rare : Rarity.VeryRare;
            return a;
        }

        private static void SaveA(ref int count, ArmorData a)
        {
            AssetDatabase.CreateAsset(a, $"{ArmorDir}/{a.Id}.asset");
            count++;
        }

        // =====================================================================
        // BASE WEAPONS
        // =====================================================================

        private static void GenerateBaseWeapons(ref int count)
        {
            // --- Simple Melee (8) ---
            SaveW(ref count, Melee("club", "Club", 1, DieType.D4, DamageType.Bludgeoning, Lgt, "Simple"));
            SaveW(ref count, Melee("dagger", "Dagger", 1, DieType.D4, DamageType.Piercing, Fin | Lgt | Thr, "Simple"));
            SaveW(ref count, Melee("greatclub", "Greatclub", 1, DieType.D8, DamageType.Bludgeoning, Two, "Simple"));
            SaveW(ref count, Melee("handaxe", "Handaxe", 1, DieType.D6, DamageType.Slashing, Lgt | Thr, "Simple"));
            SaveW(ref count, Melee("javelin", "Javelin", 1, DieType.D6, DamageType.Piercing, Thr, "Simple"));
            SaveW(ref count, Melee("mace", "Mace", 1, DieType.D6, DamageType.Bludgeoning, WeaponProperty.None, "Simple"));
            SaveW(ref count, Versatile("quarterstaff", "Quarterstaff", 1, DieType.D6, 1, DieType.D8, DamageType.Bludgeoning, WeaponProperty.None, "Simple"));
            SaveW(ref count, Melee("sickle", "Sickle", 1, DieType.D4, DamageType.Slashing, Lgt, "Simple"));

            // --- Simple Ranged (4) ---
            SaveW(ref count, Ranged("light_crossbow", "Light Crossbow", 1, DieType.D8, DamageType.Piercing, 80, 320, Amm | Lod | Two, "Simple"));
            SaveW(ref count, Ranged("dart", "Dart", 1, DieType.D4, DamageType.Piercing, 20, 60, Fin | Thr, "Simple"));
            SaveW(ref count, Ranged("shortbow", "Shortbow", 1, DieType.D6, DamageType.Piercing, 80, 320, Amm | Two, "Simple"));
            SaveW(ref count, Ranged("sling", "Sling", 1, DieType.D4, DamageType.Bludgeoning, 30, 120, Amm, "Simple"));

            // --- Martial Melee (14) ---
            SaveW(ref count, Versatile("battleaxe", "Battleaxe", 1, DieType.D8, 1, DieType.D10, DamageType.Slashing, WeaponProperty.None, "Martial"));
            SaveW(ref count, Melee("flail", "Flail", 1, DieType.D8, DamageType.Bludgeoning, WeaponProperty.None, "Martial"));
            SaveW(ref count, Melee("glaive", "Glaive", 1, DieType.D10, DamageType.Slashing, Hvy | Rch | Two, "Martial"));
            SaveW(ref count, Melee("greataxe", "Greataxe", 1, DieType.D12, DamageType.Slashing, Hvy | Two, "Martial"));
            SaveW(ref count, Melee("greatsword", "Greatsword", 2, DieType.D6, DamageType.Slashing, Hvy | Two, "Martial"));
            SaveW(ref count, Melee("halberd", "Halberd", 1, DieType.D10, DamageType.Slashing, Hvy | Rch | Two, "Martial"));
            SaveW(ref count, Melee("lance", "Lance", 1, DieType.D12, DamageType.Piercing, Rch, "Martial"));
            SaveW(ref count, Versatile("longsword", "Longsword", 1, DieType.D8, 1, DieType.D10, DamageType.Slashing, WeaponProperty.None, "Martial"));
            SaveW(ref count, Melee("morningstar", "Morningstar", 1, DieType.D8, DamageType.Piercing, WeaponProperty.None, "Martial"));
            SaveW(ref count, Melee("rapier", "Rapier", 1, DieType.D8, DamageType.Piercing, Fin, "Martial"));
            SaveW(ref count, Melee("scimitar", "Scimitar", 1, DieType.D6, DamageType.Slashing, Fin | Lgt, "Martial"));
            SaveW(ref count, Melee("shortsword", "Shortsword", 1, DieType.D6, DamageType.Piercing, Fin | Lgt, "Martial"));
            SaveW(ref count, Versatile("trident", "Trident", 1, DieType.D6, 1, DieType.D8, DamageType.Piercing, Thr, "Martial"));
            SaveW(ref count, Versatile("warhammer", "Warhammer", 1, DieType.D8, 1, DieType.D10, DamageType.Bludgeoning, WeaponProperty.None, "Martial"));

            // --- Martial Ranged (5) ---
            SaveW(ref count, Ranged("hand_crossbow", "Hand Crossbow", 1, DieType.D6, DamageType.Piercing, 30, 120, Amm | Lgt | Lod, "Martial"));
            SaveW(ref count, Ranged("heavy_crossbow", "Heavy Crossbow", 1, DieType.D10, DamageType.Piercing, 100, 400, Amm | Hvy | Lod | Two, "Martial"));
            SaveW(ref count, Ranged("longbow", "Longbow", 1, DieType.D8, DamageType.Piercing, 150, 600, Amm | Hvy | Two, "Martial"));
            SaveW(ref count, Ranged("blowgun", "Blowgun", 1, DieType.D4, DamageType.Piercing, 25, 100, Amm | Lod, "Martial"));
            SaveW(ref count, Ranged("net", "Net", 0, DieType.D4, DamageType.Bludgeoning, 5, 15, Thr, "Martial"));
        }

        // =====================================================================
        // MAGIC WEAPON VARIANTS (+1/+2/+3)
        // =====================================================================

        private static void GenerateMagicWeaponVariants(ref int count)
        {
            // Load all base weapons and create +1/+2/+3 of each
            var baseGuids = AssetDatabase.FindAssets("t:WeaponData", new[] { WeaponDir });
            foreach (string guid in baseGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var baseW = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
                if (baseW == null || baseW.MagicBonus > 0) continue; // Skip if already magic
                for (int b = 1; b <= 3; b++)
                    SaveW(ref count, MagicWeapon(baseW, b));
            }
        }

        // =====================================================================
        // NAMED MAGIC WEAPONS
        // =====================================================================

        private static void GenerateNamedMagicWeapons(ref int count)
        {
            // Flame Tongue (Longsword) — Rare, +2d6 fire
            var w = Versatile("flame_tongue_longsword", "Flame Tongue Longsword", 1, DieType.D8, 1, DieType.D10, DamageType.Slashing | DamageType.Fire, WeaponProperty.None, "Martial");
            w.MagicBonus = 0; w.Rarity = Rarity.Rare; w.DamageBonus = 7; // avg 2d6 as flat for simplicity
            SaveW(ref count, w);

            // Flame Tongue (Greatsword) — Rare
            w = Melee("flame_tongue_greatsword", "Flame Tongue Greatsword", 2, DieType.D6, DamageType.Slashing | DamageType.Fire, Hvy | Two, "Martial");
            w.MagicBonus = 0; w.Rarity = Rarity.Rare; w.DamageBonus = 7;
            SaveW(ref count, w);

            // Frost Brand (Longsword) — VeryRare, +1d6 cold, magic +3
            w = Versatile("frost_brand_longsword", "Frost Brand Longsword", 1, DieType.D8, 1, DieType.D10, DamageType.Slashing | DamageType.Cold, WeaponProperty.None, "Martial");
            w.MagicBonus = 3; w.Rarity = Rarity.VeryRare; w.DamageBonus = 3;
            SaveW(ref count, w);

            // Frost Brand (Greatsword) — VeryRare
            w = Melee("frost_brand_greatsword", "Frost Brand Greatsword", 2, DieType.D6, DamageType.Slashing | DamageType.Cold, Hvy | Two, "Martial");
            w.MagicBonus = 3; w.Rarity = Rarity.VeryRare; w.DamageBonus = 3;
            SaveW(ref count, w);

            // Vorpal Sword — Legendary, greatsword +3
            w = Melee("vorpal_sword", "Vorpal Sword", 2, DieType.D6, DamageType.Slashing, Hvy | Two, "Martial");
            w.MagicBonus = 3; w.Rarity = Rarity.Legendary;
            SaveW(ref count, w);

            // Dragon Slayer (Longsword) — Rare, +1
            w = Versatile("dragon_slayer_longsword", "Dragon Slayer Longsword", 1, DieType.D8, 1, DieType.D10, DamageType.Slashing, WeaponProperty.None, "Martial");
            w.MagicBonus = 1; w.Rarity = Rarity.Rare;
            SaveW(ref count, w);

            // Sun Blade — Rare, radiant damage, +2
            w = Versatile("sun_blade", "Sun Blade", 1, DieType.D8, 1, DieType.D10, DamageType.Radiant, Fin, "Martial");
            w.MagicBonus = 2; w.Rarity = Rarity.Rare;
            SaveW(ref count, w);

            // Vicious Rapier — Rare, +2d6 on nat 20
            w = Melee("vicious_rapier", "Vicious Rapier", 1, DieType.D8, DamageType.Piercing, Fin, "Martial");
            w.MagicBonus = 0; w.Rarity = Rarity.Rare; w.DamageBonus = 7;
            SaveW(ref count, w);

            // Javelin of Lightning — Uncommon, lightning, +1
            w = Melee("javelin_of_lightning", "Javelin of Lightning", 1, DieType.D6, DamageType.Piercing | DamageType.Lightning, Thr, "Simple");
            w.MagicBonus = 1; w.Rarity = Rarity.Uncommon;
            SaveW(ref count, w);

            // Dagger of Venom — Rare, poison, +1
            w = Melee("dagger_of_venom", "Dagger of Venom", 1, DieType.D4, DamageType.Piercing | DamageType.Poison, Fin | Lgt | Thr, "Simple");
            w.MagicBonus = 1; w.Rarity = Rarity.Rare;
            SaveW(ref count, w);

            // Mace of Disruption — Rare, radiant, +2
            w = Melee("mace_of_disruption", "Mace of Disruption", 1, DieType.D6, DamageType.Bludgeoning | DamageType.Radiant, WeaponProperty.None, "Simple");
            w.MagicBonus = 2; w.Rarity = Rarity.Rare; w.DamageBonus = 7;
            SaveW(ref count, w);

            // Mace of Smiting — Rare, +1
            w = Melee("mace_of_smiting", "Mace of Smiting", 1, DieType.D6, DamageType.Bludgeoning, WeaponProperty.None, "Simple");
            w.MagicBonus = 1; w.Rarity = Rarity.Rare;
            SaveW(ref count, w);

            // Mace of Terror — Rare, +2
            w = Melee("mace_of_terror", "Mace of Terror", 1, DieType.D6, DamageType.Bludgeoning, WeaponProperty.None, "Simple");
            w.MagicBonus = 2; w.Rarity = Rarity.Rare;
            SaveW(ref count, w);

            // Holy Avenger — Legendary, radiant, +3
            w = Versatile("holy_avenger", "Holy Avenger", 1, DieType.D8, 1, DieType.D10, DamageType.Slashing | DamageType.Radiant, WeaponProperty.None, "Martial");
            w.MagicBonus = 3; w.Rarity = Rarity.Legendary; w.DamageBonus = 7;
            SaveW(ref count, w);

            // Nine Lives Stealer — VeryRare, necrotic, +2
            w = Versatile("nine_lives_stealer", "Nine Lives Stealer", 1, DieType.D8, 1, DieType.D10, DamageType.Slashing | DamageType.Necrotic, WeaponProperty.None, "Martial");
            w.MagicBonus = 2; w.Rarity = Rarity.VeryRare;
            SaveW(ref count, w);

            // Dancing Sword — VeryRare, longsword +1
            w = Versatile("dancing_sword", "Dancing Sword", 1, DieType.D8, 1, DieType.D10, DamageType.Slashing, WeaponProperty.None, "Martial");
            w.MagicBonus = 1; w.Rarity = Rarity.VeryRare;
            SaveW(ref count, w);

            // Defender — Legendary, longsword +3
            w = Versatile("defender_longsword", "Defender Longsword", 1, DieType.D8, 1, DieType.D10, DamageType.Slashing, WeaponProperty.None, "Martial");
            w.MagicBonus = 3; w.Rarity = Rarity.Legendary;
            SaveW(ref count, w);

            // Oathbow — VeryRare, longbow +3
            w = Ranged("oathbow", "Oathbow", 1, DieType.D8, DamageType.Piercing, 150, 600, Amm | Hvy | Two, "Martial");
            w.MagicBonus = 3; w.Rarity = Rarity.VeryRare;
            SaveW(ref count, w);

            // Berserker Greataxe — Rare, +1
            w = Melee("berserker_greataxe", "Berserker Greataxe", 1, DieType.D12, DamageType.Slashing, Hvy | Two, "Martial");
            w.MagicBonus = 1; w.Rarity = Rarity.Rare;
            SaveW(ref count, w);

            // Giant Slayer Greataxe — Rare, +1
            w = Melee("giant_slayer_greataxe", "Giant Slayer Greataxe", 1, DieType.D12, DamageType.Slashing, Hvy | Two, "Martial");
            w.MagicBonus = 1; w.Rarity = Rarity.Rare;
            SaveW(ref count, w);

            // Luck Blade — Legendary, longsword +1
            w = Versatile("luck_blade", "Luck Blade", 1, DieType.D8, 1, DieType.D10, DamageType.Slashing, Fin, "Martial");
            w.MagicBonus = 1; w.Rarity = Rarity.Legendary;
            SaveW(ref count, w);

            // Scimitar of Speed — VeryRare, +2
            w = Melee("scimitar_of_speed", "Scimitar of Speed", 1, DieType.D6, DamageType.Slashing, Fin | Lgt, "Martial");
            w.MagicBonus = 2; w.Rarity = Rarity.VeryRare;
            SaveW(ref count, w);
        }

        // =====================================================================
        // BASE ARMOR
        // =====================================================================

        private static void GenerateBaseArmor(ref int count)
        {
            // --- Light Armor (3) ---
            SaveA(ref count, MakeArmor("padded", "Padded", 11, ArmorType.Light, true, 0));
            SaveA(ref count, MakeArmor("leather", "Leather", 11, ArmorType.Light, false, 0));
            SaveA(ref count, MakeArmor("studded_leather", "Studded Leather", 12, ArmorType.Light, false, 0));

            // --- Medium Armor (4) ---
            SaveA(ref count, MakeArmor("hide", "Hide", 12, ArmorType.Medium, false, 0));
            SaveA(ref count, MakeArmor("chain_shirt", "Chain Shirt", 13, ArmorType.Medium, false, 0));
            SaveA(ref count, MakeArmor("scale_mail", "Scale Mail", 14, ArmorType.Medium, true, 0));
            SaveA(ref count, MakeArmor("breastplate", "Breastplate", 14, ArmorType.Medium, false, 0));

            // --- Heavy Armor (4) ---
            SaveA(ref count, MakeArmor("ring_mail", "Ring Mail", 14, ArmorType.Heavy, true, 0));
            SaveA(ref count, MakeArmor("chain_mail", "Chain Mail", 16, ArmorType.Heavy, true, 13));
            SaveA(ref count, MakeArmor("splint", "Splint", 17, ArmorType.Heavy, true, 15));
            SaveA(ref count, MakeArmor("plate", "Plate", 18, ArmorType.Heavy, true, 15));

            // --- Shield (1) ---
            SaveA(ref count, MakeArmor("shield", "Shield", 2, ArmorType.Shield, false, 0));
        }

        // =====================================================================
        // MAGIC ARMOR VARIANTS (+1/+2/+3)
        // =====================================================================

        private static void GenerateMagicArmorVariants(ref int count)
        {
            var baseGuids = AssetDatabase.FindAssets("t:ArmorData", new[] { ArmorDir });
            foreach (string guid in baseGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var baseA = AssetDatabase.LoadAssetAtPath<ArmorData>(path);
                if (baseA == null || baseA.MagicBonus > 0) continue;
                for (int b = 1; b <= 3; b++)
                    SaveA(ref count, MagicArmor(baseA, b));
            }
        }

        // =====================================================================
        // NAMED MAGIC ARMOR
        // =====================================================================

        private static void GenerateNamedMagicArmor(ref int count)
        {
            // Mithral variants — no stealth disadvantage, no STR req
            var a = MakeArmor("mithral_chain_shirt", "Mithral Chain Shirt", 13, ArmorType.Medium, false, 0);
            a.Rarity = Rarity.Uncommon;
            SaveA(ref count, a);

            a = MakeArmor("mithral_chain_mail", "Mithral Chain Mail", 16, ArmorType.Heavy, false, 0);
            a.Rarity = Rarity.Uncommon;
            SaveA(ref count, a);

            a = MakeArmor("mithral_breastplate", "Mithral Breastplate", 14, ArmorType.Medium, false, 0);
            a.Rarity = Rarity.Uncommon;
            SaveA(ref count, a);

            a = MakeArmor("mithral_splint", "Mithral Splint", 17, ArmorType.Heavy, false, 0);
            a.Rarity = Rarity.Uncommon;
            SaveA(ref count, a);

            a = MakeArmor("mithral_plate", "Mithral Plate", 18, ArmorType.Heavy, false, 0);
            a.Rarity = Rarity.Uncommon;
            SaveA(ref count, a);

            // Adamantine variants — +1 magic bonus
            a = MakeArmor("adamantine_chain_mail", "Adamantine Chain Mail", 16, ArmorType.Heavy, true, 13);
            a.MagicBonus = 1; a.Rarity = Rarity.Uncommon;
            SaveA(ref count, a);

            a = MakeArmor("adamantine_splint", "Adamantine Splint", 17, ArmorType.Heavy, true, 15);
            a.MagicBonus = 1; a.Rarity = Rarity.Uncommon;
            SaveA(ref count, a);

            a = MakeArmor("adamantine_plate", "Adamantine Plate", 18, ArmorType.Heavy, true, 15);
            a.MagicBonus = 1; a.Rarity = Rarity.Uncommon;
            SaveA(ref count, a);

            // Dragon Scale Mail — VeryRare, +2
            a = MakeArmor("dragon_scale_mail", "Dragon Scale Mail", 14, ArmorType.Medium, true, 0);
            a.MagicBonus = 2; a.Rarity = Rarity.VeryRare;
            SaveA(ref count, a);

            // Demon Armor — VeryRare, plate +1
            a = MakeArmor("demon_armor", "Demon Armor", 18, ArmorType.Heavy, true, 15);
            a.MagicBonus = 1; a.Rarity = Rarity.VeryRare;
            SaveA(ref count, a);

            // Animated Shield — VeryRare, +2
            a = MakeArmor("animated_shield", "Animated Shield", 2, ArmorType.Shield, false, 0);
            a.MagicBonus = 2; a.Rarity = Rarity.VeryRare;
            SaveA(ref count, a);
        }
    }
}
```

- [ ] **Step 2: Verify no syntax errors with Unity compilation**

Run: `"/c/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath "C:/Dev/Forever engin" -logFile - -quit 2>&1 | tail -5`
Expected: Exit code 0, no compilation errors

- [ ] **Step 3: Commit**

```bash
git add "Assets/Editor/RPG/EquipmentGenerator.cs"
git commit -m "feat: add EquipmentGenerator with all weapons and armor

27 base weapons (SRD), +1/+2/+3 magic variants, 22 named magic weapons,
13 base armor, +1/+2/+3 armor variants, 11 named magic armor.
Factory helpers: Melee, Ranged, Versatile, MagicWeapon, MakeArmor, MagicArmor."
```

---

### Task 2: Integrate into ContentGenerator

**Files:**
- Modify: `Assets/Editor/RPG/ContentGenerator.cs:8-10` (add constants)
- Modify: `Assets/Editor/RPG/ContentGenerator.cs:19-21` (add generator call)
- Modify: `Assets/Editor/RPG/ContentGenerator.cs:35-37` (add directories)
- Modify: `Assets/Editor/RPG/ContentGenerator.cs:53-75` (add validation)

- [ ] **Step 1: Add directory constants**

At `ContentGenerator.cs:10`, after the SpellDir constant, add:

```csharp
        private const string WeaponDir = "Assets/Resources/RPG/Content/Weapons";
        private const string ArmorDir = "Assets/Resources/RPG/Content/Armor";
```

- [ ] **Step 2: Add EquipmentGenerator.GenerateAll() call**

At `ContentGenerator.cs:21`, after `SpeciesGenerator.GenerateAll();`, add:

```csharp
            EquipmentGenerator.GenerateAll();
```

- [ ] **Step 3: Add directory creation**

At `ContentGenerator.cs:37`, after the Spells folder line, add:

```csharp
            EnsureFolder("Assets/Resources/RPG/Content", "Weapons");
            EnsureFolder("Assets/Resources/RPG/Content", "Armor");
```

- [ ] **Step 4: Add equipment validation**

At `ContentGenerator.cs:55`, after the spells FindAssets line, add:

```csharp
            var weapons = AssetDatabase.FindAssets("t:WeaponData", new[] { WeaponDir });
            var armor = AssetDatabase.FindAssets("t:ArmorData", new[] { ArmorDir });
```

After the spells count log (line 59), add:

```csharp
            Debug.Log($"[ContentGenerator] Generated {weapons.Length} WeaponData assets");
            Debug.Log($"[ContentGenerator] Generated {armor.Length} ArmorData assets");
```

After the spells count check (line 75), add:

```csharp
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
```

Update the total line (line 109) to include weapons and armor:

```csharp
            int total = classes.Length + species.Length + spells.Length + weapons.Length + armor.Length;
```

- [ ] **Step 5: Verify Unity compilation**

Run: `"/c/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath "C:/Dev/Forever engin" -logFile - -quit 2>&1 | tail -5`
Expected: Exit code 0

- [ ] **Step 6: Commit**

```bash
git add "Assets/Editor/RPG/ContentGenerator.cs"
git commit -m "feat: integrate EquipmentGenerator into ContentGenerator

Adds Weapons/Armor directory creation, calls EquipmentGenerator.GenerateAll(),
validates 100+ weapons and 50+ armor in asset counts."
```

---

### Task 3: Update RPGBridge

**Files:**
- Modify: `Assets/Scripts/Demo/RPGBridge.cs:43,49` (remove TODO comments)

- [ ] **Step 1: Remove TODO comments from RPGBridge.cs**

Replace the two TODO comment lines:

Line 43: `// Weapons (TODO: generate weapon/armor assets into Resources/RPG/Content/Weapons/)`
→ `// Weapons (assets in Assets/Resources/RPG/Content/Weapons/)`

Line 49: `// Armor (TODO: generate weapon/armor assets into Resources/RPG/Content/Armor/)`
→ `// Armor (assets in Assets/Resources/RPG/Content/Armor/)`

- [ ] **Step 2: Commit**

```bash
git add "Assets/Scripts/Demo/RPGBridge.cs"
git commit -m "chore: remove equipment TODO comments from RPGBridge

Assets now generated by EquipmentGenerator."
```

---

### Task 4: Generate Assets + Full Validation

- [ ] **Step 1: Run content generation via Unity batch mode**

Run: `"/c/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath "C:/Dev/Forever engin" -logFile - -quit -executeMethod ForeverEngine.Editor.RPG.ContentGenerator.GenerateAll 2>&1 | grep -E "\[ContentGenerator\]|\[EquipmentGenerator\]|error|Error"`

Expected output should include:
- `[EquipmentGenerator] Generated X weapons, Y armor (Z total)` where Z is ~189
- `[ContentGenerator] Generated X WeaponData assets`
- `[ContentGenerator] Generated Y ArmorData assets`
- `0 errors`

- [ ] **Step 2: Verify asset files exist on disk**

Run: `ls "C:/Dev/Forever engin/Assets/Resources/RPG/Content/Weapons/" | wc -l` — expect 130+ .asset files
Run: `ls "C:/Dev/Forever engin/Assets/Resources/RPG/Content/Armor/" | wc -l` — expect 59+ .asset files

- [ ] **Step 3: Spot-check key assets referenced by RPGBridge**

Run: `ls "C:/Dev/Forever engin/Assets/Resources/RPG/Content/Weapons/longsword.asset"` — must exist
Run: `ls "C:/Dev/Forever engin/Assets/Resources/RPG/Content/Weapons/quarterstaff.asset"` — must exist
Run: `ls "C:/Dev/Forever engin/Assets/Resources/RPG/Content/Weapons/mace.asset"` — must exist
Run: `ls "C:/Dev/Forever engin/Assets/Resources/RPG/Content/Weapons/shortsword.asset"` — must exist
Run: `ls "C:/Dev/Forever engin/Assets/Resources/RPG/Content/Armor/chain_mail.asset"` — must exist
Run: `ls "C:/Dev/Forever engin/Assets/Resources/RPG/Content/Armor/scale_mail.asset"` — must exist
Run: `ls "C:/Dev/Forever engin/Assets/Resources/RPG/Content/Armor/leather.asset"` — must exist
Run: `ls "C:/Dev/Forever engin/Assets/Resources/RPG/Content/Armor/shield.asset"` — must exist

- [ ] **Step 4: Rebuild demo scenes**

Run: `"/c/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath "C:/Dev/Forever engin" -logFile - -quit -executeMethod ForeverEngine.Editor.Demo.DemoSceneBuilder.BuildAllScenes 2>&1 | grep -E "\[DemoScene\]|error|Error"`
Expected: All 3 scenes built successfully

- [ ] **Step 5: Commit generated assets**

```bash
git add "Assets/Resources/RPG/Content/Weapons/" "Assets/Resources/RPG/Content/Armor/"
git commit -m "feat: generate ~189 equipment assets

Weapons: 27+ base (SRD), 81+ magic variants, 22 named magic items.
Armor: 13 base (SRD), 39 magic variants, 11 named magic items.
All RPGBridge references now resolve to real assets."
```

---

### Task 5: Final Verification

- [ ] **Step 1: Run full content generation from scratch to verify idempotency**

Run: `"/c/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" -batchmode -nographics -projectPath "C:/Dev/Forever engin" -logFile - -quit -executeMethod ForeverEngine.Editor.RPG.ContentGenerator.GenerateAll 2>&1 | grep -E "\[ContentGenerator\]|\[EquipmentGenerator\]|error|Error"`

Expected: Same counts as Task 4, 0 errors. Idempotent — running twice produces same results.

- [ ] **Step 2: Verify total asset count matches spec**

Run: Count weapon + armor assets. Spec target: ~189.
If count differs significantly (>10% off), investigate and document why.

- [ ] **Step 3: Final commit if any fixes were needed**

Only if changes were made during verification. Otherwise skip.
