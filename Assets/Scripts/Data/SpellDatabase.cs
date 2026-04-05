using UnityEngine;
using System.Collections.Generic;

namespace ForeverEngine.Data
{
    /// <summary>
    /// Individual spell entry — serialized from JSON or manually authored in Inspector.
    /// Mirrors the GM module spell schema for cross-project compatibility.
    /// </summary>
    [System.Serializable]
    public struct SpellEntry
    {
        public string id;
        public string name;
        public int level;          // 0 = cantrip
        public string school;      // "evocation", "necromancy", etc.
        public string castingTime; // "1 action", "1 bonus action", "1 minute"
        public string range;       // "60 feet", "Self", "Touch"
        public string duration;    // "Instantaneous", "1 minute", "Concentration, up to 1 hour"
        public bool concentration;
        public string components;  // "V, S, M (material)"
        public string savingThrow; // "DEX", "CON", "" if none
        public string damage;      // e.g. "8d6" — empty for non-damaging spells
        public string damageType;  // "fire", "radiant", etc.
        public string healing;     // e.g. "1d8+5" — empty for non-healing spells
        public string[] classes;   // classes that can cast this spell
        public string description;
    }

    /// <summary>
    /// ScriptableObject container for all spells.
    /// Loaded once at startup; systems query via GetSpell(id).
    /// </summary>
    [CreateAssetMenu(fileName = "SpellDatabase", menuName = "RPG/Spell Database")]
    public class SpellDatabase : ScriptableObject
    {
        public SpellEntry[] spells;

        private Dictionary<string, SpellEntry> _lookup;
        private bool _built;

        private void BuildLookup()
        {
            if (_built) return;
            _lookup = new Dictionary<string, SpellEntry>(spells?.Length ?? 0);
            if (spells == null) { _built = true; return; }
            foreach (var spell in spells)
                if (!string.IsNullOrEmpty(spell.id))
                    _lookup[spell.id] = spell;
            _built = true;
        }

        /// <summary>Returns the spell with the given id, or null if not found.</summary>
        public SpellEntry? GetSpell(string id)
        {
            BuildLookup();
            return _lookup.TryGetValue(id, out var s) ? s : (SpellEntry?)null;
        }

        /// <summary>Returns all spells of the given spell level (0 = cantrips).</summary>
        public SpellEntry[] GetSpellsByLevel(int level)
        {
            if (spells == null) return System.Array.Empty<SpellEntry>();
            var result = new List<SpellEntry>();
            foreach (var s in spells)
                if (s.level == level) result.Add(s);
            return result.ToArray();
        }

        /// <summary>Returns all spells available to the given class name (case-insensitive).</summary>
        public SpellEntry[] GetSpellsByClass(string className)
        {
            if (spells == null || string.IsNullOrEmpty(className))
                return System.Array.Empty<SpellEntry>();

            string lower = className.ToLowerInvariant();
            var result = new List<SpellEntry>();
            foreach (var s in spells)
            {
                if (s.classes == null) continue;
                foreach (var c in s.classes)
                    if (c != null && c.ToLowerInvariant() == lower) { result.Add(s); break; }
            }
            return result.ToArray();
        }

        private void OnValidate() => _built = false; // Rebuild after Inspector edits
    }
}
