using UnityEngine;
using ForeverEngine.Generation.Data;
using ForeverEngine.Generation.Agents;
using ForeverEngine.ECS.Data;

namespace ForeverEngine.Generation
{
    public class PipelineCoordinator
    {
        public const int MaxRetries = 3;

        public struct GenerationResult
        {
            public bool Success;
            public string Error;
            public TerrainGenerator.TerrainResult Terrain;
            public RoomGraph Layout;
            public PopulationGenerator.PopulationResult Population;
            public MapGenerationRequest Request;
        }

        public static GenerationResult Generate(MapGenerationRequest request)
        {
            if (!request.Validate(out string error))
                return new GenerationResult { Success = false, Error = error };

            var profile = MapProfile.Get(request.MapType);
            var result = new GenerationResult { Request = request };

            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                // Phase 1: Terrain
                result.Terrain = TerrainGenerator.Generate(request, profile);

                // Cave carving for dungeon/cave families
                if (profile.Family == MapFamily.Dungeon || profile.Family == MapFamily.Cave)
                    CaveCarver.Carve(result.Terrain.Walkability, request.Width, request.Height, request.Seed + attempt);

                // Validate Phase 1: enough walkable area
                int walkable = 0;
                for (int i = 0; i < result.Terrain.Walkability.Length; i++)
                    if (result.Terrain.Walkability[i]) walkable++;

                float walkPercent = (float)walkable / result.Terrain.Walkability.Length;
                if (walkPercent < 0.1f)
                {
                    Debug.LogWarning($"[Pipeline] Phase 1 failed: only {walkPercent:P0} walkable (attempt {attempt+1})");
                    request.Seed += 1000; // Try different seed
                    continue;
                }

                // Phase 2: Layout
                result.Layout = TopologyBuilder.Build(profile.PreferredTopology, profile.BaseRoomCount, request.Seed);
                RoomPlacer.Place(result.Layout, request.Width, request.Height, request.Seed);
                RoomPlacer.CarveRooms(result.Layout, result.Terrain.Walkability, request.Width);
                CorridorCarver.Carve(result.Layout, result.Terrain.Walkability, request.Width, request.Height);

                // Assign room purposes from profile pool
                var rng = new System.Random(request.Seed + 500);
                foreach (var node in result.Layout.Nodes)
                {
                    if (node.Id == result.Layout.EntranceNodeId) { node.Purpose = "entrance"; continue; }
                    if (profile.RoomPool != null && profile.RoomPool.Length > 0)
                        node.Purpose = profile.RoomPool[rng.Next(profile.RoomPool.Length)];
                }

                // Validate Phase 2: rooms connected
                if (!result.Layout.IsConnected())
                {
                    Debug.LogWarning($"[Pipeline] Phase 2 failed: rooms not connected (attempt {attempt+1})");
                    request.Seed += 1000;
                    continue;
                }

                // Phase 3: Population
                result.Population = PopulationGenerator.Populate(result.Layout, request, profile, result.Terrain.Walkability, request.Width);

                result.Success = true;
                Debug.Log($"[Pipeline] Generation complete: {request.MapType} {request.Width}x{request.Height}, {result.Layout.Nodes.Count} rooms, {result.Population.Encounters.Count} encounters");
                return result;
            }

            result.Error = $"Generation failed after {MaxRetries} attempts";
            return result;
        }
    }
}
