// Assets/Scripts/World/ChunkData.cs
using System;
using System.Collections.Generic;
using ForeverEngine.Core.World.Baked;

namespace ForeverEngine.Procedural
{
    /// <summary>
    /// All data for a single 256×256 chunk. Serializable to disk.
    /// </summary>
    [Serializable]
    public class ChunkData
    {
        public int ChunkX;
        public int ChunkZ;
        public BiomeType Biome;
        public float BaseElevation;

        /// <summary>Heightmap resolution (samples per axis). 64 = 4m per sample.</summary>
        public const int HeightmapRes = 64;

        /// <summary>128×128 heightmap values (terrain elevation, 2m per sample).</summary>
        public float[] Heightmap;

        /// <summary>Whether Director Hub has populated content for this chunk.</summary>
        public bool ContentGenerated;

        /// <summary>Structures placed by Director Hub (serialized as JSON strings).</summary>
        public List<PlacedStructure> Structures = new();

        /// <summary>NPC definitions for this chunk.</summary>
        public List<ChunkNPC> NPCs = new();

        /// <summary>Encounter zones in this chunk.</summary>
        public List<EncounterZoneDef> EncounterZones = new();

        /// <summary>Road segments in this chunk.</summary>
        public List<RoadSegment> Roads = new();

        /// <summary>Dungeon entrances.</summary>
        public List<DungeonEntrance> DungeonEntrances = new();

        /// <summary>Baked prop placements loaded from the macro-bake file.</summary>
        public List<BakedPropPlacement> Props = new();

        public ChunkData(int chunkX, int chunkZ)
        {
            ChunkX = chunkX;
            ChunkZ = chunkZ;
            Heightmap = new float[HeightmapRes * HeightmapRes];
        }
    }

    [Serializable]
    public class PlacedStructure
    {
        public string Type;     // "lumber_camp", "inn", "blacksmith"
        public string Name;
        public float PosX, PosZ;
        public string Size;     // "small", "medium", "large"
    }

    [Serializable]
    public class ChunkNPC
    {
        public string Name;
        public string Role;     // "merchant", "quest_giver", "guard"
        public float PosX, PosZ;
        public string Faction;
    }

    [Serializable]
    public class EncounterZoneDef
    {
        public float PosX, PosZ;
        public float Radius;
        public string DangerLevel; // "low", "medium", "high"
    }

    [Serializable]
    public class RoadSegment
    {
        public float FromX, FromZ;
        public float ToX, ToZ;
        public string RoadType;    // "dirt_path", "cobblestone", "trail"
    }

    [Serializable]
    public class DungeonEntrance
    {
        public string EntranceId;
        public string Type;     // "minor", "major"
        public float PosX, PosZ;
        public string Visual;   // "cave_mouth", "ruined_doorway", "mine_shaft"
    }
}
