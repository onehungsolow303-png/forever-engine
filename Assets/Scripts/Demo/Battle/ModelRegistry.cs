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
            ["Wolf"]       = new(0.8f, "Monsters/Giant Rat"),
            ["Dire Wolf"]  = new(1.2f, "Monsters/Giant Rat"),
            ["Alpha Wolf"] = new(1.4f, "Monsters/Giant Rat"),
            ["Wolf Pup"]   = new(0.6f, "Monsters/Giant Rat"),

            // Road / Ruins biome
            ["Skeleton"]        = new(1.0f, "Monsters/skeleton Fighter"),
            ["Skeleton Archer"] = new(1.0f, "Monsters/skeleton archer "),
            ["Mutant"]          = new(1.0f, "Monsters/Zombie male fighter",
                                           "Monsters/Zombie female fighter"),
            ["Mutant Hulk"]     = new(1.3f, "Monsters/zombie male warrior "),

            // Plains biome
            ["Bandit"]         = new(1.0f, "Monsters/Human Female Bandit",
                                           "Monsters/Human male bandit fighter",
                                           "Monsters/Human Male Bandit fighter 4",
                                           "Monsters/Human male bandit fighter 5"),
            ["Bandit Captain"] = new(1.1f, "Monsters/Halfling male bandit"),
            ["Cultist"]        = new(1.0f, "Monsters/human feamle assassin",
                                           "Monsters/Human male Alchenist"),

            // Dungeon biome (for Phase 2 encounter expansion)
            ["Goblin"]       = new(0.9f, "Monsters/goblin female fighter",
                                         "Monsters/Goblin male archer",
                                         "Monsters/goblin male rogue "),
            ["Goblin King"]  = new(1.1f, "Monsters/Goblin King"),
            ["Mummy"]        = new(1.0f, "Monsters/Mummy"),
            ["Plague Rat"]   = new(0.6f, "Monsters/Giant Rat"),
            ["Lizard Folk"]  = new(1.0f, "Monsters/Lizard folk fighter",
                                         "Monsters/Lizardfolk fighter"),
            ["Lizard Folk Archer"] = new(1.0f, "Monsters/Lizard folk archer"),
            ["Orc"]          = new(1.1f, "Monsters/Orc male fighterr",
                                         "Monsters/Orc female fighter"),
            ["Kobold"]       = new(0.8f, "Monsters/Kobold male fighter"),

            // Player race/class combos
            ["Dwarf_Fighter"]      = new(0.9f, "NPCs/Dwarf male fighter",
                                               "NPCs/Dwarf male fighter 2",
                                               "NPCs/Dwarf male fighter 3"),
            ["Dwarf_Cleric"]       = new(0.9f, "NPCs/Dwarf male cleric"),
            ["Elf_Ranger"]         = new(1.0f, "NPCs/Elf female ranger"),
            ["Elf_Fighter"]        = new(1.0f, "NPCs/Elf male fighter"),
            ["Elf_Wizard"]         = new(1.0f, "NPCs/Elf male wizard",
                                               "NPCs/Elf male wizard 2"),
            ["Human_Fighter"]      = new(1.0f, "NPCs/Human male fighter",
                                               "NPCs/Human male fighter 2",
                                               "NPCs/Human male fighter 3",
                                               "NPCs/Human female fighter"),
            ["Dragonborn_Fighter"]  = new(1.1f, "NPCs/Dragon born male fighter"),
            ["Dragonborn_Sorcerer"] = new(1.1f, "NPCs/Dragon born male sorcerer"),
            ["Default_Player"]     = new(1.0f, "NPCs/Human male fighter"),
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
