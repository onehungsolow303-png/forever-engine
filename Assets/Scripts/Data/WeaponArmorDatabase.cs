using UnityEngine;
using System.Collections.Generic;

namespace ForeverEngine.Data
{
    // ── Weapon ──────────────────────────────────────────────────────────────

    [System.Serializable]
    public struct WeaponEntry
    {
        public string id;
        public string name;
        public string category;      // "simple melee", "martial ranged", etc.
        public string damage;        // "1d8"
        public string damageType;    // "slashing", "piercing", "bludgeoning"
        public int weight;           // lbs
        public int cost;             // gold pieces
        public string properties;    // "finesse, light", etc.
        public string range;         // "20/60" for ranged, "" for melee
        public bool twoHanded;
        public bool versatile;
        public string versatileDamage; // damage when used two-handed
    }

    // ── Armor ────────────────────────────────────────────────────────────────

    [System.Serializable]
    public struct ArmorEntry
    {
        public string id;
        public string name;
        public string category;      // "light", "medium", "heavy", "shield"
        public int baseAC;
        public bool addDexModifier;  // true for light/medium armor
        public int maxDexBonus;      // cap on DEX mod (2 for medium, 0 for heavy, -1 = uncapped)
        public int strengthRequirement; // 0 = none
        public bool stealthDisadvantage;
        public int weight;
        public int cost;
        public string description;
    }

    // ── Databases ─────────────────────────────────────────────────────────────

    [CreateAssetMenu(fileName = "WeaponDatabase", menuName = "RPG/Weapon Database")]
    public class WeaponDatabase : ScriptableObject
    {
        public WeaponEntry[] weapons;

        private Dictionary<string, WeaponEntry> _lookup;
        private bool _built;

        private void BuildLookup()
        {
            if (_built) return;
            _lookup = new Dictionary<string, WeaponEntry>(weapons?.Length ?? 0);
            if (weapons == null) { _built = true; return; }
            foreach (var w in weapons)
                if (!string.IsNullOrEmpty(w.id))
                    _lookup[w.id] = w;
            _built = true;
        }

        public WeaponEntry? GetWeapon(string id)
        {
            BuildLookup();
            return _lookup.TryGetValue(id, out var w) ? w : (WeaponEntry?)null;
        }

        public WeaponEntry[] GetWeaponsByCategory(string category)
        {
            if (weapons == null) return System.Array.Empty<WeaponEntry>();
            string lower = category.ToLowerInvariant();
            var result = new List<WeaponEntry>();
            foreach (var w in weapons)
                if (w.category != null && w.category.ToLowerInvariant().Contains(lower))
                    result.Add(w);
            return result.ToArray();
        }

        private void OnValidate() => _built = false;
    }

    [CreateAssetMenu(fileName = "ArmorDatabase", menuName = "RPG/Armor Database")]
    public class ArmorDatabase : ScriptableObject
    {
        public ArmorEntry[] armors;

        private Dictionary<string, ArmorEntry> _lookup;
        private bool _built;

        private void BuildLookup()
        {
            if (_built) return;
            _lookup = new Dictionary<string, ArmorEntry>(armors?.Length ?? 0);
            if (armors == null) { _built = true; return; }
            foreach (var a in armors)
                if (!string.IsNullOrEmpty(a.id))
                    _lookup[a.id] = a;
            _built = true;
        }

        public ArmorEntry? GetArmor(string id)
        {
            BuildLookup();
            return _lookup.TryGetValue(id, out var a) ? a : (ArmorEntry?)null;
        }

        /// <summary>Calculates effective AC for a character wearing this armor.</summary>
        public int CalculateAC(ArmorEntry armor, int dexScore)
        {
            int dexMod = InfinityRPGData.AbilityModifier(dexScore);
            if (!armor.addDexModifier) return armor.baseAC;
            if (armor.maxDexBonus >= 0) dexMod = System.Math.Min(dexMod, armor.maxDexBonus);
            return armor.baseAC + dexMod;
        }

        private void OnValidate() => _built = false;
    }
}
