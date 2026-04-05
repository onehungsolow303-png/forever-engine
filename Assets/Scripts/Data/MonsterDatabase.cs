using UnityEngine;
using System.Collections.Generic;

namespace ForeverEngine.Data
{
    /// <summary>
    /// A single monster's stat block — mirrors the GM module monster schema.
    /// </summary>
    [System.Serializable]
    public struct MonsterEntry
    {
        public string id;
        public string name;
        public string monsterType;   // "undead", "beast", "humanoid", etc.
        public string size;          // "tiny","small","medium","large","huge","gargantuan"
        public float cr;             // challenge rating (0.125, 0.25, 0.5, 1, 2, …)
        public int xp;               // XP value (overrides CRToXP if non-zero)
        public string alignment;

        // Ability scores
        public int strength;
        public int dexterity;
        public int constitution;
        public int intelligence;
        public int wisdom;
        public int charisma;

        public int ac;
        public string acType;        // "natural armor", "chain mail", etc.
        public int hp;
        public string hitDice;       // "4d8+4"
        public int speed;            // feet per turn
        public int flySpeed;
        public int swimSpeed;

        // Saves (bonus added on top of ability mod; 0 = not proficient)
        public int savStr; public int savDex; public int savCon;
        public int savInt; public int savWis; public int savCha;

        // Skills (bonus on top of ability mod; 0 = not proficient)
        public int perception;
        public int stealth;

        public string damageResistances;
        public string damageImmunities;
        public string conditionImmunities;
        public string senses;
        public string languages;

        // Actions
        public MonsterAction[] actions;
        public string description;
        public string aiDefaultBehavior; // "chase", "guard", "flee", etc. — maps to AIType
    }

    [System.Serializable]
    public struct MonsterAction
    {
        public string name;
        public string attackType;  // "melee weapon attack", "spell attack", ""
        public string toHit;       // "+5" (parsed at runtime)
        public string reach;       // "5 ft"
        public string damage;      // "2d6+3"
        public string damageType;  // "slashing"
        public string description;
    }

    /// <summary>
    /// ScriptableObject container for all monsters.
    /// Loaded once at startup; systems query via GetMonster(id).
    /// </summary>
    [CreateAssetMenu(fileName = "MonsterDatabase", menuName = "RPG/Monster Database")]
    public class MonsterDatabase : ScriptableObject
    {
        public MonsterEntry[] monsters;

        private Dictionary<string, MonsterEntry> _lookup;
        private bool _built;

        private void BuildLookup()
        {
            if (_built) return;
            _lookup = new Dictionary<string, MonsterEntry>(monsters?.Length ?? 0);
            if (monsters == null) { _built = true; return; }
            foreach (var m in monsters)
                if (!string.IsNullOrEmpty(m.id))
                    _lookup[m.id] = m;
            _built = true;
        }

        /// <summary>Returns the monster with the given id, or null if not found.</summary>
        public MonsterEntry? GetMonster(string id)
        {
            BuildLookup();
            return _lookup.TryGetValue(id, out var m) ? m : (MonsterEntry?)null;
        }

        /// <summary>Returns all monsters of the given type (case-insensitive).</summary>
        public MonsterEntry[] GetMonstersByType(string type)
        {
            if (monsters == null) return System.Array.Empty<MonsterEntry>();
            string lower = type.ToLowerInvariant();
            var result = new List<MonsterEntry>();
            foreach (var m in monsters)
                if (m.monsterType != null && m.monsterType.ToLowerInvariant() == lower)
                    result.Add(m);
            return result.ToArray();
        }

        /// <summary>Returns all monsters with CR up to the given value — useful for encounter building.</summary>
        public MonsterEntry[] GetMonstersByCRRange(float minCR, float maxCR)
        {
            if (monsters == null) return System.Array.Empty<MonsterEntry>();
            var result = new List<MonsterEntry>();
            foreach (var m in monsters)
                if (m.cr >= minCR && m.cr <= maxCR)
                    result.Add(m);
            return result.ToArray();
        }

        private void OnValidate() => _built = false;
    }
}
