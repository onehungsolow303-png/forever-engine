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
            return locationType switch
            {
                "town" or "fortress" or "castle" or "glade" => TownPrefab,
                "camp" or "shrine" => CampPrefab,
                "dungeon" => DungeonEntrancePrefab,
                _ => TownPrefab,
            };
        }
    }
}
