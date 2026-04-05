using System.Collections.Generic;
using ForeverEngine.Generation.Data;
using ForeverEngine.ECS.Data;

namespace ForeverEngine.Generation.Agents
{
    public static class PopulationGenerator
    {
        public struct PopulationResult
        {
            public List<EntitySpawn> Encounters;
            public List<EntitySpawn> Traps;
            public List<EntitySpawn> Loot;
            public List<EntitySpawn> Dressing;
            public EntitySpawn PlayerSpawn;
        }

        public struct EntitySpawn
        {
            public int X, Y;
            public string Type;     // "creature", "trap", "loot", "dressing"
            public string Variant;  // "goblin", "spike_trap", "chest", "torch"
            public int Value;       // XP, damage, gold amount
        }

        public static PopulationResult Populate(RoomGraph graph, MapGenerationRequest request, MapProfile profile, bool[] walkability, int mapWidth)
        {
            var rng = new System.Random(request.Seed + 999);
            int xpBudget = GameTables.GetXPBudget(request.PartyLevel, request.PartySize);
            int goldBudget = GameTables.GetGoldBudget(request.PartyLevel, profile.LootTier);

            var result = new PopulationResult
            {
                Encounters = new List<EntitySpawn>(),
                Traps = new List<EntitySpawn>(),
                Loot = new List<EntitySpawn>(),
                Dressing = new List<EntitySpawn>()
            };

            // Player spawns in entrance room
            var entrance = graph.GetNode(graph.EntranceNodeId) ?? (graph.Nodes.Count > 0 ? graph.Nodes[0] : null);
            if (entrance != null)
            {
                result.PlayerSpawn = new EntitySpawn
                {
                    X = entrance.X + entrance.W / 2,
                    Y = entrance.Y + entrance.H / 2,
                    Type = "spawn", Variant = "player"
                };
            }

            int xpSpent = 0, goldSpent = 0;

            foreach (var room in graph.Nodes)
            {
                if (room.Id == graph.EntranceNodeId) continue;

                var purpose = RoomPurpose.Get(room.Purpose ?? "guard_room");
                int cx = room.X + room.W / 2, cy = room.Y + room.H / 2;

                // Encounters
                if (rng.NextDouble() < purpose.EncounterMult * 0.5f && xpSpent < xpBudget)
                {
                    string creature = profile.CreaturePool != null && profile.CreaturePool.Length > 0
                        ? profile.CreaturePool[rng.Next(profile.CreaturePool.Length)] : "goblin";
                    int xp = System.Math.Min(xpBudget / graph.Nodes.Count, xpBudget - xpSpent);
                    result.Encounters.Add(new EntitySpawn { X = cx, Y = cy, Type = "creature", Variant = creature, Value = xp });
                    xpSpent += xp;
                }

                // Traps
                if (rng.NextDouble() < purpose.TrapMult * profile.TrapDensity)
                {
                    result.Traps.Add(new EntitySpawn { X = cx + rng.Next(-2, 3), Y = cy + rng.Next(-2, 3), Type = "trap", Variant = "spike_trap", Value = request.PartyLevel * 2 });
                }

                // Loot
                if (rng.NextDouble() < purpose.LootMult * 0.4f && goldSpent < goldBudget)
                {
                    int gold = System.Math.Min(goldBudget / graph.Nodes.Count, goldBudget - goldSpent);
                    result.Loot.Add(new EntitySpawn { X = cx + rng.Next(-1, 2), Y = cy + rng.Next(-1, 2), Type = "loot", Variant = "chest", Value = gold });
                    goldSpent += gold;
                }

                // Dressing (always add some atmosphere)
                string[] dressingOptions = { "torch", "barrel", "crate", "cobweb", "bones" };
                int dressingCount = rng.Next(1, 4);
                for (int d = 0; d < dressingCount; d++)
                {
                    int dx = room.X + rng.Next(1, System.Math.Max(2, room.W - 1));
                    int dy = room.Y + rng.Next(1, System.Math.Max(2, room.H - 1));
                    result.Dressing.Add(new EntitySpawn { X = dx, Y = dy, Type = "dressing", Variant = dressingOptions[rng.Next(dressingOptions.Length)] });
                }
            }

            return result;
        }
    }
}
