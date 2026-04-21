using UnityEngine;

namespace ForeverEngine.Demo.Overworld
{
    [CreateAssetMenu(fileName = "OverworldPrefabMap", menuName = "Forever Engine/Overworld Prefab Map")]
    public class OverworldPrefabMapper : ScriptableObject
    {
        [Header("Terrain Prefabs (one random per biome — primary decoration)")]
        public GameObject[] PlainsPrefabs;
        public GameObject[] ForestPrefabs;
        public GameObject[] MountainPrefabs;
        public GameObject[] WaterPrefabs;
        public GameObject[] RuinsPrefabs;

        [Header("Scatter Prefabs (multiple placed randomly within chunk)")]
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

        [Header("Location Variety Arrays (picked random-per-location, falls back to single above if empty)")]
        public GameObject[] TownPrefabs;
        public GameObject[] CampPrefabs;

        [Header("Location Markers")]
        public GameObject ShrinePrefab;      // Stone altar or portal base
        public GameObject GladePrefab;       // Arch alley or column arrangement
        public GameObject FortressPrefab;    // Tower prefab
        public GameObject CastlePrefab;      // Larger tower or building set
        public GameObject[] LocationRuinsPrefabs;  // Broken buildings/walls (random pick)
        public GameObject CampFirePrefab;    // Brazier for campsite

        [Header("Player")]
        public GameObject PlayerPrefab;

        [Header("Chunk Settings")]
        [Tooltip("World-space size of each terrain chunk")]
        public float HexWorldSize = 4f;

        [Tooltip("Height scale multiplier for elevation")]
        public float ElevationScale = 2f;

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
