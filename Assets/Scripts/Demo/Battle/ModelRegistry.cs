using System.Collections.Generic;

namespace ForeverEngine.Demo.Battle
{
    public static class ModelRegistry
    {
        public struct ModelEntry
        {
            public string[] Paths;
            public float Scale;

            public ModelEntry(float scale, params string[] paths)
            {
                Paths = paths;
                Scale = scale;
            }
        }

        private static readonly Dictionary<string, ModelEntry> _map = new()
        {
            // Forest biome
            ["Wolf"]       = new(0.8f, "Models/Monsters/Giant Rat"),
            ["Dire Wolf"]  = new(1.2f, "Models/Monsters/Giant Rat"),
            ["Alpha Wolf"] = new(1.4f, "Models/Monsters/Giant Rat"),

            // Road / Ruins biome
            ["Skeleton"]        = new(1.0f, "Models/Monsters/skeleton Fighter"),
            ["Skeleton Archer"] = new(1.0f, "Models/Monsters/skeleton archer"),
            ["Mutant"]          = new(1.0f, "Models/Monsters/Zombie male fighter",
                                           "Models/Monsters/Zombie female fighter"),
            ["Mutant Hulk"]     = new(1.3f, "Models/Monsters/zombie male warrior"),

            // Plains biome
            ["Bandit"]         = new(1.0f, "Models/Monsters/Human Female Bandit",
                                           "Models/Monsters/Human male bandit fighter",
                                           "Models/Monsters/Human Male Bandit fighter 4",
                                           "Models/Monsters/Human male bandit fighter 5"),
            ["Bandit Captain"] = new(1.1f, "Models/Monsters/Halfling male bandit"),
            ["Cultist"]        = new(1.0f, "Models/Monsters/human feamle assassin",
                                           "Models/Monsters/Human male Alchenist"),

            // Dungeon biome (for Phase 2 encounter expansion)
            ["Goblin"]       = new(0.9f, "Models/Monsters/goblin female fighter",
                                         "Models/Monsters/Goblin male archer",
                                         "Models/Monsters/goblin male rogue"),
            ["Goblin King"]  = new(1.1f, "Models/Monsters/Goblin King"),
            ["Mummy"]        = new(1.0f, "Models/Monsters/Mummy"),
            ["Lizard Folk"]  = new(1.0f, "Models/Monsters/Lizard folk fighter",
                                         "Models/Monsters/Lizardfolk fighter"),
            ["Lizard Folk Archer"] = new(1.0f, "Models/Monsters/Lizard folk archer"),
            ["Orc"]          = new(1.1f, "Models/Monsters/Orc male fighterr",
                                         "Models/Monsters/Orc female fighter"),
            ["Kobold"]       = new(0.8f, "Models/Monsters/Kobold male fighter"),

            // Player race/class combos
            ["Dwarf_Fighter"]      = new(0.9f, "Models/NPCs/Dwarf male fighter",
                                               "Models/NPCs/Dwarf male fighter 2",
                                               "Models/NPCs/Dwarf male fighter 3"),
            ["Dwarf_Cleric"]       = new(0.9f, "Models/NPCs/Dwarf male cleric"),
            ["Elf_Ranger"]         = new(1.0f, "Models/NPCs/Elf female ranger"),
            ["Elf_Fighter"]        = new(1.0f, "Models/NPCs/Elf male fighter"),
            ["Elf_Wizard"]         = new(1.0f, "Models/NPCs/Elf male wizard",
                                               "Models/NPCs/Elf male wizard 2"),
            ["Human_Fighter"]      = new(1.0f, "Models/NPCs/Human male fighter",
                                               "Models/NPCs/Human male fighter 2",
                                               "Models/NPCs/Human male fighter 3",
                                               "Models/NPCs/Human female fighter"),
            ["Dragonborn_Fighter"]  = new(1.1f, "Models/NPCs/Dragon born male fighter"),
            ["Dragonborn_Sorcerer"] = new(1.1f, "Models/NPCs/Dragon born male sorcerer"),
            ["Default_Player"]     = new(1.0f, "Models/NPCs/Human male fighter"),
        };

        public static (string path, float scale) Resolve(string name)
        {
            if (string.IsNullOrEmpty(name) || !_map.TryGetValue(name, out var entry))
                return (null, 1f);
            var path = entry.Paths[UnityEngine.Random.Range(0, entry.Paths.Length)];
            return (path, entry.Scale);
        }

        public static bool HasMapping(string name)
            => !string.IsNullOrEmpty(name) && _map.ContainsKey(name);
    }
}
