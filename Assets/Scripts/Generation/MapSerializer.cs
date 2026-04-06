using UnityEngine;
using System.IO;
using System.Collections.Generic;
using ForeverEngine.Generation.Agents;
using ForeverEngine.Generation.Data;

namespace ForeverEngine.Generation
{
    public static class MapSerializer
    {
        /// <summary>
        /// Serializes a GenerationResult to map_schema v1.1.0 JSON + terrain PNG.
        /// Returns the path to the written MapData.json.
        /// </summary>
        public static string Serialize(PipelineCoordinator.GenerationResult result, string outputDir)
        {
            Directory.CreateDirectory(outputDir);

            // Build terrain PNG
            string pngRelative = "terrain_z0.png";
            string pngPath = Path.Combine(outputDir, pngRelative);
            WriteTerrainPng(result.Terrain, pngPath);

            // Build serializable structure
            var mapData = new SMapData
            {
                config = BuildConfig(result.Request),
                z_levels = new[] { BuildZLevel(result, pngRelative) },
                transitions = new STransition[0],
                spawns = BuildSpawns(result.Population, result.Request.PartyLevel),
                labels = new SLabel[0]
            };

            string jsonPath = Path.Combine(outputDir, "MapData.json");
            string json = JsonUtility.ToJson(mapData, true);
            File.WriteAllText(jsonPath, json);

            Debug.Log($"[MapSerializer] Wrote {jsonPath} ({json.Length} bytes) + {pngPath}");
            return jsonPath;
        }

        /// <summary>
        /// Serializes two floors (z=0 and z=-1) with stairs transitions.
        /// Returns the path to the written MapData.json.
        /// </summary>
        public static string Serialize(
            PipelineCoordinator.GenerationResult floor0,
            PipelineCoordinator.GenerationResult floorMinus1,
            string outputDir)
        {
            Directory.CreateDirectory(outputDir);

            // Build terrain PNGs for both floors
            string png0 = "terrain_z0.png";
            string pngM1 = "terrain_z-1.png";
            WriteTerrainPng(floor0.Terrain, Path.Combine(outputDir, png0));
            WriteTerrainPng(floorMinus1.Terrain, Path.Combine(outputDir, pngM1));

            // Find stairs position: use last room center on floor 0
            var stairsRoom = floor0.Layout.Nodes[floor0.Layout.Nodes.Count - 1];
            int stairsX = stairsRoom.X + stairsRoom.W / 2;
            int stairsY = stairsRoom.Y + stairsRoom.H / 2;

            // Build z-levels
            var zLevel0 = BuildZLevel(floor0, png0);
            zLevel0.z = 0;

            var zLevelM1 = BuildZLevelAtZ(floorMinus1, pngM1, -1);

            // Build transitions (stairs down on z=0, stairs up on z=-1)
            var transitions = new[]
            {
                new STransition { x = stairsX, y = stairsY, from_z = 0, to_z = -1, type = "stairs_down" },
                new STransition { x = stairsX, y = stairsY, from_z = -1, to_z = 0, type = "stairs_up" }
            };

            // Combine spawns from both floors (player only on floor 0)
            var allSpawns = new List<SSpawn>();
            allSpawns.AddRange(BuildSpawns(floor0.Population, floor0.Request.PartyLevel));
            // Floor -1 enemies (no player spawn)
            if (floorMinus1.Population.Encounters != null)
            {
                foreach (var enc in floorMinus1.Population.Encounters)
                {
                    var cs = CreatureDatabase.GetStats(enc.Variant);
                    allSpawns.Add(new SSpawn
                    {
                        name = enc.Variant ?? "creature",
                        x = enc.X, y = enc.Y, z = -1,
                        token_type = "enemy",
                        ai_behavior = cs.AiBehavior,
                        stats = new SStats
                        {
                            hp = cs.HP, ac = cs.AC,
                            strength = cs.STR, dexterity = cs.DEX, constitution = cs.CON,
                            intelligence = cs.INT, wisdom = cs.WIS, charisma = cs.CHA,
                            speed = cs.Speed, atk_dice = cs.AtkDice
                        }
                    });
                }
            }

            var mapData = new SMapData
            {
                config = BuildConfig(floor0.Request),
                z_levels = new[] { zLevel0, zLevelM1 },
                transitions = transitions,
                spawns = allSpawns.ToArray(),
                labels = new SLabel[0]
            };

            string jsonPath = Path.Combine(outputDir, "MapData.json");
            string json = JsonUtility.ToJson(mapData, true);
            File.WriteAllText(jsonPath, json);

            Debug.Log($"[MapSerializer] Wrote 2-floor map: {jsonPath} ({json.Length} bytes)");
            return jsonPath;
        }

        /// <summary>
        /// BuildZLevel variant that sets z to a specific value instead of 0.
        /// </summary>
        private static SZLevel BuildZLevelAtZ(PipelineCoordinator.GenerationResult result, string pngRelative, int z)
        {
            var level = BuildZLevel(result, pngRelative);
            level.z = z;
            return level;
        }

        // ── Builders ──────────────────────────────────────────────────────

        private static SConfig BuildConfig(MapGenerationRequest req)
        {
            return new SConfig
            {
                width = req.Width,
                height = req.Height,
                map_type = req.MapType,
                biome = req.Biome,
                seed = req.Seed,
                schema_version = "1.1.0",
                generator_version = "forever-engine-cs-1.0",
                created_at = System.DateTime.UtcNow.ToString("o")
            };
        }

        private static SZLevel BuildZLevel(PipelineCoordinator.GenerationResult result, string pngRelative)
        {
            int len = result.Terrain.Width * result.Terrain.Height;

            // bool[] -> int[]
            var walk = new int[len];
            for (int i = 0; i < len; i++)
                walk[i] = result.Terrain.Walkability[i] ? 1 : 0;

            // Entities from traps, loot, dressing
            var entities = new List<SEntity>();
            int eid = 0;
            if (result.Population.Traps != null)
                foreach (var t in result.Population.Traps)
                    entities.Add(new SEntity { id = $"trap_{eid++}", type = "trap", x = t.X, y = t.Y, variant = t.Variant });
            if (result.Population.Loot != null)
                foreach (var l in result.Population.Loot)
                    entities.Add(new SEntity { id = $"loot_{eid++}", type = "loot", x = l.X, y = l.Y, variant = l.Variant });
            if (result.Population.Dressing != null)
                foreach (var d in result.Population.Dressing)
                    entities.Add(new SEntity { id = $"dressing_{eid++}", type = "dressing", x = d.X, y = d.Y, variant = d.Variant });

            // Room graph
            var rooms = new List<SRoom>();
            var connections = new List<SConnection>();
            if (result.Layout != null)
            {
                foreach (var node in result.Layout.Nodes)
                    rooms.Add(new SRoom { id = node.Id, x = node.X, y = node.Y, w = node.W, h = node.H, purpose = node.Purpose ?? "" });
                foreach (var edge in result.Layout.Edges)
                    connections.Add(new SConnection
                    {
                        from_room = edge.FromId,
                        to_room = edge.ToId,
                        type = edge.Type switch
                        {
                            ConnectionType.Door => "door",
                            ConnectionType.Secret => "secret",
                            ConnectionType.Open => "open",
                            _ => "corridor"
                        }
                    });
            }

            return new SZLevel
            {
                z = 0,
                terrain_png = pngRelative,
                walkability = walk,
                entities = entities.ToArray(),
                room_graph = new SRoomGraph { rooms = rooms.ToArray(), connections = connections.ToArray() }
            };
        }

        private static SSpawn[] BuildSpawns(PopulationGenerator.PopulationResult pop, int partyLevel)
        {
            var spawns = new List<SSpawn>();

            // Player spawn
            if (pop.PlayerSpawn.Type != null)
            {
                spawns.Add(new SSpawn
                {
                    name = "Player",
                    x = pop.PlayerSpawn.X,
                    y = pop.PlayerSpawn.Y,
                    z = 0,
                    token_type = "player",
                    ai_behavior = "scripted",
                    stats = new SStats { hp = 20, ac = 14, strength = 14, dexterity = 14, constitution = 14, intelligence = 10, wisdom = 10, charisma = 10, speed = 6, atk_dice = "1d8+2" }
                });
            }

            // Enemy encounters — real D&D 5e stats from CreatureDatabase
            if (pop.Encounters != null)
            {
                foreach (var enc in pop.Encounters)
                {
                    var cs = CreatureDatabase.GetStats(enc.Variant);
                    spawns.Add(new SSpawn
                    {
                        name = enc.Variant ?? "creature",
                        x = enc.X,
                        y = enc.Y,
                        z = 0,
                        token_type = "enemy",
                        ai_behavior = cs.AiBehavior,
                        stats = new SStats
                        {
                            hp = cs.HP,
                            ac = cs.AC,
                            strength = cs.STR,
                            dexterity = cs.DEX,
                            constitution = cs.CON,
                            intelligence = cs.INT,
                            wisdom = cs.WIS,
                            charisma = cs.CHA,
                            speed = cs.Speed,
                            atk_dice = cs.AtkDice
                        }
                    });
                }
            }

            return spawns.ToArray();
        }

        // ── Terrain PNG ───────────────────────────────────────────────────

        private static void WriteTerrainPng(TerrainGenerator.TerrainResult terrain, string path)
        {
            int w = terrain.Width, h = terrain.Height;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.filterMode = FilterMode.Point;

            // TerrainColor is byte[w*h*3] in row-major RGB
            // Texture2D.SetPixels32 expects bottom-to-top, so flip Y
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                int srcRow = (h - 1 - y) * w; // flip Y for Unity texture coords
                int dstRow = y * w;
                for (int x = 0; x < w; x++)
                {
                    int ci = (srcRow + x) * 3;
                    pixels[dstRow + x] = new Color32(
                        terrain.TerrainColor[ci],
                        terrain.TerrainColor[ci + 1],
                        terrain.TerrainColor[ci + 2],
                        255);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            byte[] png = tex.EncodeToPNG();
            File.WriteAllBytes(path, png);
            Object.DestroyImmediate(tex);
        }

        // ── Serialization DTOs (snake_case field names match map_schema v1.1.0) ──

        [System.Serializable]
        public class SMapData
        {
            public SConfig config;
            public SZLevel[] z_levels;
            public STransition[] transitions;
            public SSpawn[] spawns;
            public SLabel[] labels;
        }

        [System.Serializable]
        public class SConfig
        {
            public int width, height;
            public string map_type, biome;
            public int seed;
            public string schema_version, generator_version, created_at;
        }

        [System.Serializable]
        public class SZLevel
        {
            public int z;
            public string terrain_png;
            public int[] walkability;
            public SEntity[] entities;
            public SRoomGraph room_graph;
        }

        [System.Serializable]
        public class SEntity
        {
            public string id, type;
            public int x, y;
            public string variant;
        }

        [System.Serializable]
        public class SRoomGraph
        {
            public SRoom[] rooms;
            public SConnection[] connections;
        }

        [System.Serializable]
        public class SRoom
        {
            public int id, x, y, w, h;
            public string purpose;
        }

        [System.Serializable]
        public class SConnection
        {
            public int from_room, to_room;
            public string type;
        }

        [System.Serializable]
        public class STransition
        {
            public int x, y, from_z, to_z;
            public string type;
        }

        [System.Serializable]
        public class SSpawn
        {
            public string name;
            public int x, y, z;
            public string token_type, ai_behavior;
            public SStats stats;
        }

        [System.Serializable]
        public class SStats
        {
            public int hp, ac;
            public int strength, dexterity, constitution, intelligence, wisdom, charisma;
            public int speed;
            public string atk_dice;
        }

        [System.Serializable]
        public class SLabel
        {
            public int x, y, z;
            public string text, category;
        }
    }
}
