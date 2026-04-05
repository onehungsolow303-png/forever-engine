using System.Collections.Generic;
using UnityEngine;
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Spells
{
    /// <summary>
    /// Static spell registry. Loads all SpellData ScriptableObjects from Resources
    /// and provides lookup by ID, name, school, level, and class.
    /// </summary>
    public static class SpellDatabase
    {
        private static Dictionary<string, SpellData> _byId;
        private static Dictionary<string, SpellData> _byName;
        private static bool _initialized;

        /// <summary>
        /// Initialize the database by loading all SpellData from Resources.
        /// Call this at startup or on first access.
        /// </summary>
        public static void Initialize()
        {
            _byId = new Dictionary<string, SpellData>();
            _byName = new Dictionary<string, SpellData>();

            var all = Resources.LoadAll<SpellData>("RPG/Spells");
            if (all != null)
            {
                foreach (var spell in all)
                {
                    if (!string.IsNullOrEmpty(spell.Id))
                        _byId[spell.Id] = spell;
                    if (!string.IsNullOrEmpty(spell.Name))
                        _byName[spell.Name.ToLowerInvariant()] = spell;
                }
            }

            _initialized = true;
        }

        private static void EnsureInitialized()
        {
            if (!_initialized) Initialize();
        }

        /// <summary>
        /// Get a spell by its unique ID.
        /// </summary>
        public static SpellData GetById(string id)
        {
            EnsureInitialized();
            _byId.TryGetValue(id, out var spell);
            return spell;
        }

        /// <summary>
        /// Get a spell by name (case-insensitive).
        /// </summary>
        public static SpellData GetByName(string name)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(name)) return null;
            _byName.TryGetValue(name.ToLowerInvariant(), out var spell);
            return spell;
        }

        /// <summary>
        /// Get all spells of a specific school.
        /// </summary>
        public static List<SpellData> GetBySchool(SpellSchool school)
        {
            EnsureInitialized();
            var results = new List<SpellData>();
            foreach (var spell in _byId.Values)
            {
                if (spell.School == school) results.Add(spell);
            }
            return results;
        }

        /// <summary>
        /// Get all spells of a specific level.
        /// </summary>
        public static List<SpellData> GetByLevel(int level)
        {
            EnsureInitialized();
            var results = new List<SpellData>();
            foreach (var spell in _byId.Values)
            {
                if (spell.Level == level) results.Add(spell);
            }
            return results;
        }

        /// <summary>
        /// Get all spells available to a specific class.
        /// </summary>
        public static List<SpellData> GetByClass(ClassFlag classFlag)
        {
            EnsureInitialized();
            var results = new List<SpellData>();
            foreach (var spell in _byId.Values)
            {
                if ((spell.Classes & classFlag) != 0) results.Add(spell);
            }
            return results;
        }

        /// <summary>
        /// Get all spells matching a school and class.
        /// </summary>
        public static List<SpellData> GetBySchoolAndClass(SpellSchool school, ClassFlag classFlag)
        {
            EnsureInitialized();
            var results = new List<SpellData>();
            foreach (var spell in _byId.Values)
            {
                if (spell.School == school && (spell.Classes & classFlag) != 0)
                    results.Add(spell);
            }
            return results;
        }

        /// <summary>
        /// Get total number of registered spells.
        /// </summary>
        public static int Count
        {
            get
            {
                EnsureInitialized();
                return _byId.Count;
            }
        }

        /// <summary>
        /// Get all registered spells.
        /// </summary>
        public static IEnumerable<SpellData> All
        {
            get
            {
                EnsureInitialized();
                return _byId.Values;
            }
        }

        /// <summary>
        /// Force re-initialization (useful after content generation).
        /// </summary>
        public static void Reload()
        {
            _initialized = false;
            Initialize();
        }
    }
}
