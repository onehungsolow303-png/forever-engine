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
                                         "Monsters/Lizardfolk fighter",
                                         "Monsters/Lizard folk fighter 2"),
            ["Lizard Folk Archer"] = new(1.0f, "Monsters/Lizard folk archer"),
            ["Orc"]          = new(1.1f, "Monsters/Orc male fighterr",
                                         "Monsters/Orc female fighter"),
            ["Kobold"]       = new(0.8f, "Monsters/Kobold male fighter"),
            ["Knoll"]        = new(1.1f, "Monsters/Knoll male fighter"),
            ["Mephit"]       = new(0.8f, "Monsters/Mephitis"),
            ["Stirge"]       = new(0.6f, "Monsters/Stirges"),
            ["Zombie"]       = new(1.0f, "Monsters/Zombie male fighter",
                                         "Monsters/Zombie female fighter",
                                         "Monsters/zombie male warrior "),
            ["Velociraptor"]      = new(0.9f, "Monsters/Velociraptor"),
            ["Winged Goblin"]     = new(0.9f, "Monsters/Winged Goblin male fighter"),
            ["Goblin Shaman"]     = new(0.9f, "Monsters/Goblin male Shamon",
                                              "Monsters/Goblin female Shamen on a Giant Frog"),
            ["Goblin Rider"]      = new(1.0f, "Monsters/goblin male fighter on Giant bat",
                                              "Monsters/Goblin male fighter on Giant Rat",
                                              "Monsters/Goblin male fighter on Velociraptor",
                                              "Monsters/Goblin male fighter on warthog"),
            ["Lizard Folk Shaman"] = new(1.0f, "Monsters/Lizard Folk Shaman"),
            ["Skeleton Rider"]     = new(1.2f, "Monsters/Skeleton horse and skeleton fighter"),
            ["Raptor Rider"]       = new(1.1f, "Monsters/Young Velociraptor with goblin male fighter rider"),

            // Player race/class combos
            ["Dwarf_Fighter"]      = new(0.9f, "NPCs/Dwarf male fighter",
                                               "NPCs/Dwarf male fighter 2",
                                               "NPCs/Dwarf male fighter 3"),
            ["Dwarf_Cleric"]       = new(0.9f, "NPCs/Dwarf male cleric"),
            ["Elf_Ranger"]         = new(1.0f, "NPCs/Elf female ranger"),
            ["Elf_Fighter"]        = new(1.0f, "NPCs/Elf male fighter"),
            ["Elf_Wizard"]         = new(1.0f, "NPCs/Elf male wizard",
                                               "NPCs/Elf male wizard 2"),
            ["Elf_Rider"]          = new(1.1f, "NPCs/Bare with Elf male rider",
                                               "NPCs/Elf male on a young Dragon"),
            ["Human_Fighter"]      = new(1.0f, "NPCs/Human male fighter",
                                               "NPCs/Human male fighter 2",
                                               "NPCs/Human male fighter 3",
                                               "NPCs/Human female fighter"),
            ["Human_Companion"]    = new(1.0f, "NPCs/Human male and Dog"),
            ["Githyanki_Fighter"]  = new(1.0f, "NPCs/Githyanki female fighter"),
            ["Githyanki_Sorcerer"] = new(1.0f, "NPCs/Githyanl female sorcerer"),
            ["Griffin_Rider"]      = new(1.2f, "NPCs/griffin with elf male fighter rider"),
            ["Panther_Rider"]      = new(1.1f, "NPCs/Panther with Elf female archer rider"),
            ["Pegasus"]            = new(1.2f, "NPCs/Pegasus"),
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
