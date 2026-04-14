using UnityEngine;
using ForeverEngine.Genres.Strategy;

namespace ForeverEngine.Demo.Overworld
{
    [CreateAssetMenu(fileName = "OverworldPrefabMap", menuName = "Forever Engine/Overworld Prefab Map")]
    public class OverworldPrefabMapper : ScriptableObject
    {
        [Header("Terrain Prefabs (one random per hex — primary decoration)")]
        public GameObject[] PlainsPrefabs;
        public GameObject[] ForestPrefabs;
        public GameObject[] MountainPrefabs;
        public GameObject[] WaterPrefabs;
        public GameObject[] RuinsPrefabs;

        [Header("Scatter Prefabs (multiple placed randomly within hex)")]
        public GameObject[] ForestScatter;
        public GameObject[] MountainScatter;
        public GameObject[] PlainsScatter;
        public GameObject[] RuinsScatter;

        [Header("Ground Materials (PBR textures per biome)")]
        public Material PlainsGround;
        public Material ForestGround;
        public Material MountainGround;
        public Material WaterGround;
        public Material RuinsGround;

        [Header("Location Prefabs")]
        public GameObject TownPrefab;
        public GameObject CampPrefab;
        public GameObject DungeonEntrancePrefab;

        [Header("Location Markers")]
        public GameObject ShrinePrefab;      // Stone altar or portal base
        public GameObject GladePrefab;       // Arch alley or column arrangement
        public GameObject FortressPrefab;    // Tower prefab
        public GameObject CastlePrefab;      // Larger tower or building set
        public GameObject[] LocationRuinsPrefabs;  // Broken buildings/walls (random pick)
        public GameObject CampFirePrefab;    // Brazier for campsite

        [Header("Player")]
        public GameObject PlayerPrefab;

        [Header("Tile Settings")]
        [Tooltip("World-space size of each hex tile")]
        public float HexWorldSize = 4f;

        [Tooltip("Height scale multiplier for elevation")]
        public float ElevationScale = 2f;

        public GameObject GetPrefabForTile(TileType type, int seed)
        {
            var array = type switch
            {
                TileType.Plains => PlainsPrefabs,
                TileType.Forest => ForestPrefabs,
                TileType.Mountain => MountainPrefabs,
                TileType.Water => WaterPrefabs,
                TileType.Road => RuinsPrefabs,
                _ => PlainsPrefabs,
            };

            if (array == null || array.Length == 0) return null;
            int index = Mathf.Abs(seed) % array.Length;
            return array[index];
        }

        public Material GetGroundMaterial(TileType type)
        {
            return type switch
            {
                TileType.Plains => PlainsGround,
                TileType.Forest => ForestGround,
                TileType.Mountain => MountainGround,
                TileType.Water => WaterGround,
                TileType.Road => RuinsGround,
                _ => PlainsGround,
            };
        }

        public GameObject[] GetScatterPrefabs(TileType type)
        {
            return type switch
            {
                TileType.Plains => PlainsScatter,
                TileType.Forest => ForestScatter,
                TileType.Mountain => MountainScatter,
                TileType.Road => RuinsScatter,
                _ => null,
            };
        }

        public GameObject GetLocationPrefab(string locationType)
        {
            return (locationType ?? "").ToLowerInvariant() switch
            {
                "camp" => CampFirePrefab != null ? CampFirePrefab : CampPrefab,
                "town" => TownPrefab,
                "shrine" => ShrinePrefab,
                "glade" => GladePrefab,
                "dungeon" => DungeonEntrancePrefab,
                "fortress" => FortressPrefab,
                "castle" => CastlePrefab,
                "ruins" => LocationRuinsPrefabs is { Length: > 0 }
                    ? LocationRuinsPrefabs[UnityEngine.Random.Range(0, LocationRuinsPrefabs.Length)]
                    : TownPrefab,
                _ => TownPrefab,
            };
        }
    }
}
