using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using System.IO;
using ForeverEngine.ECS.Components;
using ForeverEngine.ECS.Systems;

namespace ForeverEngine.MonoBehaviour.Bootstrap
{
    /// <summary>
    /// Imports map_data.json from Map Generator into ECS entities.
    /// Rewritten from pygame map_loader.py load_map().
    /// Bridges JSON → NativeArrays + ECS entities.
    /// </summary>
    public class MapImporter : UnityEngine.MonoBehaviour
    {
        private MapData _mapData;

        [System.Serializable]
        public class MapData
        {
            public MapConfig config;
            public ZLevelData[] z_levels;
            public TransitionData[] transitions;
            public SpawnData[] spawns;
            public LabelData[] labels;
        }

        [System.Serializable]
        public class MapConfig
        {
            public int width;
            public int height;
            public string map_type;
            public string biome;
            public int seed;
            public string schema_version;
        }

        [System.Serializable]
        public class ZLevelData
        {
            public int z;
            public string terrain_png;
            public int[] walkability;
            public EntityData[] entities;
        }

        [System.Serializable]
        public class EntityData
        {
            public string id;
            public string type;
            public int x, y;
            public string variant;
        }

        [System.Serializable]
        public class TransitionData
        {
            public int x, y, from_z, to_z;
            public string type;
        }

        [System.Serializable]
        public class SpawnData
        {
            public string name;
            public int x, y, z;
            public string token_type;
            public string ai_behavior;
            public SpawnStats stats;
        }

        [System.Serializable]
        public class SpawnStats
        {
            public int hp = 10, ac = 10;
            public int strength = 10, dexterity = 10, constitution = 10;
            public int intelligence = 10, wisdom = 10, charisma = 10;
            public int speed = 6;
            public string atk_dice = "1d4";
        }

        [System.Serializable]
        public class LabelData
        {
            public int x, y, z;
            public string text;
            public string category;
        }

        public void Import(string jsonPath, EntityManager em)
        {
            string dir = Path.GetDirectoryName(jsonPath);
            string json = File.ReadAllText(jsonPath);
            _mapData = JsonUtility.FromJson<MapData>(json);

            // Create map singleton entity
            var mapEntity = em.CreateEntity();
            em.AddComponentData(mapEntity, new MapDataSingleton
            {
                Width = _mapData.config.width,
                Height = _mapData.config.height,
                CurrentZ = 0
            });

            // Load terrain textures and walkability per z-level
            foreach (var zLevel in _mapData.z_levels)
            {
                LoadZLevel(zLevel, dir);
            }

            // Spawn creature entities
            if (_mapData.spawns != null)
            {
                foreach (var spawn in _mapData.spawns)
                {
                    SpawnCreature(spawn, em);
                }
            }

            // Create transition entities
            if (_mapData.transitions != null)
            {
                foreach (var transition in _mapData.transitions)
                {
                    CreateTransition(transition, em);
                }
            }

            Debug.Log($"[MapImporter] Loaded {_mapData.config.map_type} " +
                      $"({_mapData.config.width}x{_mapData.config.height}), " +
                      $"{_mapData.z_levels?.Length ?? 0} z-levels, " +
                      $"{_mapData.spawns?.Length ?? 0} spawns");
        }

        private void LoadZLevel(ZLevelData zLevel, string baseDir)
        {
            // Load terrain PNG as Unity texture
            string pngPath = Path.Combine(baseDir, zLevel.terrain_png);
            if (File.Exists(pngPath))
            {
                byte[] pngData = File.ReadAllBytes(pngPath);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                // ImageConversion.LoadImage is an extension method that may not resolve
                // in asmdef-scoped assemblies. Use reflection fallback.
                var method = typeof(Texture2D).GetMethod("LoadImage",
                    new System.Type[] { typeof(byte[]) });
                if (method != null)
                    method.Invoke(tex, new object[] { pngData });
                else
                    Debug.LogWarning("[MapImporter] LoadImage not available, terrain PNG skipped");
                TerrainTextureRegistry.Register(zLevel.z, tex);
            }
        }

        private void SpawnCreature(SpawnData spawn, EntityManager em)
        {
            var entity = em.CreateEntity();

            // Position
            em.AddComponentData(entity, new PositionComponent
            {
                X = spawn.x, Y = spawn.y, Z = spawn.z
            });

            // Stats (parse atk_dice string into components)
            ParseDice(spawn.stats?.atk_dice ?? "1d4", out int count, out int sides, out int bonus);
            em.AddComponentData(entity, new StatsComponent
            {
                Strength = spawn.stats?.strength ?? 10,
                Dexterity = spawn.stats?.dexterity ?? 10,
                Constitution = spawn.stats?.constitution ?? 10,
                Intelligence = spawn.stats?.intelligence ?? 10,
                Wisdom = spawn.stats?.wisdom ?? 10,
                Charisma = spawn.stats?.charisma ?? 10,
                AC = spawn.stats?.ac ?? 10,
                HP = spawn.stats?.hp ?? 10,
                MaxHP = spawn.stats?.hp ?? 10,
                Speed = spawn.stats?.speed ?? 6,
                AtkDiceCount = count,
                AtkDiceSides = sides,
                AtkDiceBonus = bonus
            });

            // Combat state
            TokenType tokenType = spawn.token_type switch
            {
                "player" => TokenType.Player,
                "enemy" => TokenType.Enemy,
                "npc" => TokenType.NPC,
                _ => TokenType.Neutral
            };

            em.AddComponentData(entity, new CombatStateComponent
            {
                TokenType = tokenType,
                Alive = true,
                HasAction = true,
                MovementRemaining = spawn.stats?.speed ?? 6
            });

            // AI behavior (non-player only)
            if (tokenType != TokenType.Player)
            {
                AIType aiType = spawn.ai_behavior switch
                {
                    "chase" => AIType.Chase,
                    "patrol" => AIType.Patrol,
                    "guard" => AIType.Guard,
                    "flee" => AIType.Flee,
                    "wander" => AIType.Wander,
                    "scripted" => AIType.Scripted,
                    _ => AIType.Chase // Default: chase (matches pygame behavior)
                };

                em.AddComponentData(entity, new AIBehaviorComponent
                {
                    Type = aiType,
                    Aggression = tokenType == TokenType.Enemy ? 1f : 0f,
                    DetectRange = 12,  // COMBAT_DETECT_RANGE from pygame config.py
                    LeashRange = 20,
                    SpawnX = spawn.x,
                    SpawnY = spawn.y
                });
            }
            else
            {
                // Player gets fog vision
                em.AddComponentData(entity, new FogVisionComponent
                {
                    SightRadius = 16  // FOW_SIGHT_RADIUS from pygame config.py
                });
                em.AddComponent<PlayerTag>(entity);
            }

            // Visual
            em.AddComponentData(entity, new VisualComponent
            {
                Variant = spawn.token_type ?? "enemy",
                Scale = 1f,
                Dirty = true
            });
        }

        private void CreateTransition(TransitionData data, EntityManager em)
        {
            var entity = em.CreateEntity();
            em.AddComponentData(entity, new PositionComponent
            {
                X = data.x, Y = data.y, Z = data.from_z
            });
            // Transition component would go here
        }

        private static void ParseDice(string dice, out int count, out int sides, out int bonus)
        {
            // Parse "2d8+3" format
            count = 1; sides = 4; bonus = 0;
            try
            {
                string s = dice.ToLower().Trim();
                int dIdx = s.IndexOf('d');
                if (dIdx < 0) return;

                count = int.Parse(s.Substring(0, dIdx));
                string rest = s.Substring(dIdx + 1);

                int plusIdx = rest.IndexOf('+');
                int minusIdx = rest.IndexOf('-');
                int modIdx = plusIdx >= 0 ? plusIdx : minusIdx;

                if (modIdx >= 0)
                {
                    sides = int.Parse(rest.Substring(0, modIdx));
                    bonus = int.Parse(rest.Substring(modIdx));
                }
                else
                {
                    sides = int.Parse(rest);
                }
            }
            catch { /* Keep defaults */ }
        }

        public int2 GetPlayerSpawnPosition()
        {
            if (_mapData?.spawns != null)
            {
                foreach (var spawn in _mapData.spawns)
                {
                    if (spawn.token_type == "player")
                        return new int2(spawn.x, spawn.y);
                }
            }
            return new int2(_mapData?.config.width / 2 ?? 0, _mapData?.config.height / 2 ?? 0);
        }
    }

    /// <summary>
    /// Static registry for terrain textures loaded per z-level.
    /// TileRenderer reads these to display the map.
    /// </summary>
    public static class TerrainTextureRegistry
    {
        private static readonly System.Collections.Generic.Dictionary<int, Texture2D> _textures = new();

        public static void Register(int z, Texture2D tex) => _textures[z] = tex;
        public static Texture2D Get(int z) => _textures.TryGetValue(z, out var tex) ? tex : null;
        public static void Clear() => _textures.Clear();
    }
}
