// Assets/Scripts/Network/ChunkDataMapper.cs
using System;
using UnityEngine;
using CoreChunk = ForeverEngine.Core.World.ChunkData;
using LocalChunk = ForeverEngine.Procedural.ChunkData;

namespace ForeverEngine.Network
{
    /// <summary>
    /// Maps server-side ChunkData (ForeverEngine.Core.World) to the local
    /// client-side ChunkData (ForeverEngine.Procedural). Handles biome string →
    /// enum conversion and heightmap upscaling (16×16 server → 64×64 client).
    /// </summary>
    public static class ChunkDataMapper
    {
        /// <summary>Server heightmap resolution per axis.</summary>
        private const int ServerRes = 16;

        /// <summary>Client heightmap resolution per axis (must match ChunkData.HeightmapRes).</summary>
        private static readonly int ClientRes = Procedural.ChunkData.HeightmapRes; // 64

        /// <summary>
        /// Convert a Core.World.ChunkData from the server into the local
        /// Procedural.ChunkData the terrain pipeline expects.
        /// </summary>
        public static LocalChunk MapServerToLocal(CoreChunk src)
        {
            var dst = new LocalChunk(src.ChunkX, src.ChunkZ)
            {
                Biome = ParseBiome(src.Biome),
                BaseElevation = src.Elevation,
                ContentGenerated = src.ContentSource == "director_hub",
            };

            // Heightmap: upscale from server resolution to client resolution
            if (src.Heightmap != null && src.Heightmap.Length > 0)
            {
                int srcRes = (int)Mathf.Sqrt(src.Heightmap.Length);
                if (srcRes * srcRes != src.Heightmap.Length)
                    srcRes = ServerRes; // fallback

                dst.Heightmap = UpscaleHeightmap(src.Heightmap, srcRes, ClientRes);
            }

            // Map structures
            if (src.Structures != null)
            {
                foreach (var s in src.Structures)
                {
                    dst.Structures.Add(new Procedural.PlacedStructure
                    {
                        Type = s.Type,
                        Name = s.Name,
                        PosX = s.PositionX,
                        PosZ = s.PositionZ,
                        Size = s.Size,
                    });
                }
            }

            // Map entities → NPCs
            if (src.Entities != null)
            {
                foreach (var e in src.Entities)
                {
                    dst.NPCs.Add(new Procedural.ChunkNPC
                    {
                        Name = e.Name,
                        Role = e.Role,
                        PosX = e.PositionX,
                        PosZ = e.PositionZ,
                        Faction = e.Faction,
                    });
                }
            }

            // Map encounter zones
            if (src.EncounterZones != null)
            {
                foreach (var ez in src.EncounterZones)
                {
                    dst.EncounterZones.Add(new Procedural.EncounterZoneDef
                    {
                        PosX = ez.PositionX,
                        PosZ = ez.PositionZ,
                        Radius = ez.Radius,
                        DangerLevel = ez.DangerLevel,
                    });
                }
            }

            return dst;
        }

        /// <summary>
        /// Parse a server biome string (e.g. "boreal_forest") to the local BiomeType enum.
        /// Falls back to Grassland for unknown values.
        /// </summary>
        private static Procedural.BiomeType ParseBiome(string biome)
        {
            if (string.IsNullOrEmpty(biome))
                return Procedural.BiomeType.Grassland;

            // Normalize: lowercase, remove underscores/hyphens/spaces
            string normalized = biome.ToLowerInvariant()
                .Replace("_", "")
                .Replace("-", "")
                .Replace(" ", "");

            return normalized switch
            {
                "ocean" => Procedural.BiomeType.Ocean,
                "beach" => Procedural.BiomeType.Beach,
                "desert" => Procedural.BiomeType.Desert,
                "aridsteppe" => Procedural.BiomeType.AridSteppe,
                "savanna" => Procedural.BiomeType.Savanna,
                "grassland" => Procedural.BiomeType.Grassland,
                "temperateforest" => Procedural.BiomeType.TemperateForest,
                "tropicalrainforest" => Procedural.BiomeType.TropicalRainforest,
                "borealforest" => Procedural.BiomeType.BorealForest,
                "taiga" => Procedural.BiomeType.Taiga,
                "tundra" => Procedural.BiomeType.Tundra,
                "icesheet" => Procedural.BiomeType.IceSheet,
                "mountain" => Procedural.BiomeType.Mountain,
                "river" => Procedural.BiomeType.River,
                _ => FallbackParseBiome(biome),
            };
        }

        /// <summary>
        /// Fallback: try Enum.TryParse before defaulting to Grassland.
        /// Handles cases where the server sends the enum name directly.
        /// </summary>
        private static Procedural.BiomeType FallbackParseBiome(string biome)
        {
            if (Enum.TryParse<Procedural.BiomeType>(biome, ignoreCase: true, out var result))
                return result;

            Debug.LogWarning($"[ChunkDataMapper] Unknown biome '{biome}', defaulting to Grassland.");
            return Procedural.BiomeType.Grassland;
        }

        /// <summary>
        /// Bilinear upscale a flat heightmap array from srcRes×srcRes to dstRes×dstRes.
        /// </summary>
        private static float[] UpscaleHeightmap(float[] src, int srcRes, int dstRes)
        {
            var dst = new float[dstRes * dstRes];
            float scale = (float)(srcRes - 1) / (dstRes - 1);

            for (int dz = 0; dz < dstRes; dz++)
            {
                for (int dx = 0; dx < dstRes; dx++)
                {
                    float sx = dx * scale;
                    float sz = dz * scale;

                    int x0 = Mathf.FloorToInt(sx);
                    int z0 = Mathf.FloorToInt(sz);
                    int x1 = Mathf.Min(x0 + 1, srcRes - 1);
                    int z1 = Mathf.Min(z0 + 1, srcRes - 1);

                    float fx = sx - x0;
                    float fz = sz - z0;

                    float h00 = src[z0 * srcRes + x0];
                    float h10 = src[z0 * srcRes + x1];
                    float h01 = src[z1 * srcRes + x0];
                    float h11 = src[z1 * srcRes + x1];

                    dst[dz * dstRes + dx] = Mathf.Lerp(
                        Mathf.Lerp(h00, h10, fx),
                        Mathf.Lerp(h01, h11, fx),
                        fz);
                }
            }

            return dst;
        }
    }
}
