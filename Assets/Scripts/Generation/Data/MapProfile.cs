using System.Collections.Generic;

namespace ForeverEngine.Generation.Data
{
    public enum TopologyType { LinearWithBranches, LoopBased, HubAndSpoke, Hybrid }
    public enum MapFamily { Dungeon, Cave, Building, Outdoor, Special }

    public class MapProfile
    {
        public string Id;
        public string Name;
        public MapFamily Family;
        public TopologyType PreferredTopology;
        public int BaseRoomCount = 8;
        public float TrapDensity = 0.3f;
        public float LootTier = 1.0f;
        public string[] RoomPool;
        public string[] CreaturePool;
        public string CorridorStyle = "stone";
        public float WalkabilityThreshold = 0.5f;

        private static Dictionary<string, MapProfile> _profiles;

        public static MapProfile Get(string mapType)
        {
            if (_profiles == null) InitProfiles();
            return _profiles.TryGetValue(mapType, out var p) ? p : _profiles["dungeon"];
        }

        public static IEnumerable<string> AllTypes()
        {
            if (_profiles == null) InitProfiles();
            return _profiles.Keys;
        }

        private static void InitProfiles()
        {
            _profiles = new Dictionary<string, MapProfile>
            {
                ["dungeon"] = new MapProfile { Id = "dungeon", Name = "Classic Dungeon", Family = MapFamily.Dungeon, PreferredTopology = TopologyType.LinearWithBranches, BaseRoomCount = 10, TrapDensity = 0.4f, LootTier = 1.0f, RoomPool = new[] { "guard_room", "armory", "barracks", "treasure", "boss_lair", "prison", "torture", "shrine" }, CreaturePool = new[] { "goblin", "skeleton", "zombie", "rat", "spider" } },
                ["cave"] = new MapProfile { Id = "cave", Name = "Natural Cave", Family = MapFamily.Cave, PreferredTopology = TopologyType.Hybrid, BaseRoomCount = 6, TrapDensity = 0.1f, LootTier = 0.5f, RoomPool = new[] { "cavern", "underground_lake", "crystal_chamber", "nest", "mushroom_grove" }, CreaturePool = new[] { "bat", "spider", "beetle", "slime" }, WalkabilityThreshold = 0.45f },
                ["castle"] = new MapProfile { Id = "castle", Name = "Castle", Family = MapFamily.Building, PreferredTopology = TopologyType.HubAndSpoke, BaseRoomCount = 12, TrapDensity = 0.2f, LootTier = 1.5f, RoomPool = new[] { "throne_room", "great_hall", "kitchen", "armory", "barracks", "dungeon", "tower", "chapel", "treasury" }, CreaturePool = new[] { "guard", "knight", "mage", "servant" } },
                ["crypt"] = new MapProfile { Id = "crypt", Name = "Crypt", Family = MapFamily.Dungeon, PreferredTopology = TopologyType.LinearWithBranches, BaseRoomCount = 8, TrapDensity = 0.5f, LootTier = 1.2f, RoomPool = new[] { "burial_chamber", "ossuary", "ritual_room", "crypt_hall", "tomb" }, CreaturePool = new[] { "skeleton", "zombie", "wraith", "vampire_spawn" } },
                ["sewer"] = new MapProfile { Id = "sewer", Name = "Sewer System", Family = MapFamily.Dungeon, PreferredTopology = TopologyType.LoopBased, BaseRoomCount = 7, TrapDensity = 0.15f, LootTier = 0.3f, RoomPool = new[] { "junction", "cistern", "overflow", "drain", "hideout" }, CreaturePool = new[] { "rat", "slime", "crocodile", "thief" } },
                ["temple"] = new MapProfile { Id = "temple", Name = "Ancient Temple", Family = MapFamily.Building, PreferredTopology = TopologyType.HubAndSpoke, BaseRoomCount = 9, TrapDensity = 0.6f, LootTier = 2.0f, RoomPool = new[] { "altar_room", "meditation", "library", "ritual_chamber", "sanctum", "cloister" }, CreaturePool = new[] { "cultist", "golem", "elemental", "priest" } },
                ["mine"] = new MapProfile { Id = "mine", Name = "Abandoned Mine", Family = MapFamily.Cave, PreferredTopology = TopologyType.LinearWithBranches, BaseRoomCount = 7, TrapDensity = 0.35f, LootTier = 0.8f, RoomPool = new[] { "shaft", "vein", "collapse", "cart_room", "foreman_office" }, CreaturePool = new[] { "kobold", "bat", "rust_monster", "earth_elemental" } },
                ["forest"] = new MapProfile { Id = "forest", Name = "Enchanted Forest", Family = MapFamily.Outdoor, PreferredTopology = TopologyType.Hybrid, BaseRoomCount = 5, TrapDensity = 0.1f, LootTier = 0.6f, RoomPool = new[] { "clearing", "grove", "pond", "ruins", "camp" }, CreaturePool = new[] { "wolf", "bear", "treant", "fairy", "bandit" }, WalkabilityThreshold = 0.6f },
                ["village"] = new MapProfile { Id = "village", Name = "Village", Family = MapFamily.Outdoor, PreferredTopology = TopologyType.HubAndSpoke, BaseRoomCount = 8, TrapDensity = 0.0f, LootTier = 0.2f, RoomPool = new[] { "tavern", "shop", "smithy", "temple", "house", "market", "well" }, CreaturePool = new[] { "villager", "guard", "merchant" } },
                ["tower"] = new MapProfile { Id = "tower", Name = "Wizard Tower", Family = MapFamily.Building, PreferredTopology = TopologyType.LinearWithBranches, BaseRoomCount = 6, TrapDensity = 0.7f, LootTier = 2.5f, RoomPool = new[] { "laboratory", "library", "summoning_circle", "observatory", "vault", "study" }, CreaturePool = new[] { "golem", "imp", "animated_armor", "mage" } },
            };
        }
    }
}
