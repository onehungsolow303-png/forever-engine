using System.Collections.Generic;

namespace ForeverEngine.Generation.Data
{
    public class RoomPurpose
    {
        public string Id;
        public string Name;
        public float EncounterMult = 1f;
        public float TrapMult = 1f;
        public float LootMult = 1f;
        public string[] NearPreferences;
        public string[] FarPreferences;

        private static Dictionary<string, RoomPurpose> _purposes;

        public static RoomPurpose Get(string id)
        {
            if (_purposes == null) Init();
            return _purposes.TryGetValue(id, out var p) ? p : new RoomPurpose { Id = id, Name = id };
        }

        private static void Init()
        {
            _purposes = new Dictionary<string, RoomPurpose>
            {
                ["guard_room"] = new RoomPurpose { Id = "guard_room", Name = "Guard Room", EncounterMult = 1.5f, TrapMult = 0.5f, LootMult = 0.3f, NearPreferences = new[] { "armory", "barracks" } },
                ["armory"] = new RoomPurpose { Id = "armory", Name = "Armory", EncounterMult = 0.5f, TrapMult = 0.3f, LootMult = 1.5f, NearPreferences = new[] { "guard_room", "barracks" } },
                ["barracks"] = new RoomPurpose { Id = "barracks", Name = "Barracks", EncounterMult = 1.2f, TrapMult = 0.2f, LootMult = 0.5f },
                ["treasure"] = new RoomPurpose { Id = "treasure", Name = "Treasure Room", EncounterMult = 0.8f, TrapMult = 2.0f, LootMult = 3.0f, FarPreferences = new[] { "entrance" } },
                ["boss_lair"] = new RoomPurpose { Id = "boss_lair", Name = "Boss Lair", EncounterMult = 3.0f, TrapMult = 0.5f, LootMult = 2.0f, FarPreferences = new[] { "entrance" } },
                ["prison"] = new RoomPurpose { Id = "prison", Name = "Prison", EncounterMult = 0.8f, TrapMult = 1.0f, LootMult = 0.2f },
                ["shrine"] = new RoomPurpose { Id = "shrine", Name = "Shrine", EncounterMult = 0.3f, TrapMult = 0.5f, LootMult = 1.0f },
                ["torture"] = new RoomPurpose { Id = "torture", Name = "Torture Chamber", EncounterMult = 1.0f, TrapMult = 1.5f, LootMult = 0.3f },
                ["cavern"] = new RoomPurpose { Id = "cavern", Name = "Cavern", EncounterMult = 0.7f, TrapMult = 0.2f, LootMult = 0.4f },
                ["nest"] = new RoomPurpose { Id = "nest", Name = "Creature Nest", EncounterMult = 2.0f, TrapMult = 0.0f, LootMult = 0.5f },
                ["throne_room"] = new RoomPurpose { Id = "throne_room", Name = "Throne Room", EncounterMult = 2.0f, TrapMult = 1.0f, LootMult = 2.5f },
                ["tavern"] = new RoomPurpose { Id = "tavern", Name = "Tavern", EncounterMult = 0.1f, TrapMult = 0.0f, LootMult = 0.1f },
                ["entrance"] = new RoomPurpose { Id = "entrance", Name = "Entrance", EncounterMult = 0.3f, TrapMult = 0.2f, LootMult = 0.0f },
            };
        }
    }
}
