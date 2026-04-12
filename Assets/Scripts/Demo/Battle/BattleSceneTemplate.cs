using UnityEngine;

namespace ForeverEngine.Demo.Battle
{
    [CreateAssetMenu(fileName = "BattleTemplate", menuName = "Forever/Battle Scene Template")]
    public class BattleSceneTemplate : ScriptableObject
    {
        [Header("Room")]
        public GameObject RoomPrefab;
        public int GridWidth = 8;
        public int GridHeight = 8;

        [Header("Spawn Zones")]
        public Vector2Int[] PlayerSpawnZone = { new(1, 1), new(1, 2), new(2, 1), new(2, 2) };
        public Vector2Int[] EnemySpawnZone = { new(5, 5), new(5, 6), new(6, 5), new(6, 6) };
        public Vector2Int[] BossSpawnPoints = { new(4, 4) };

        [Header("Biome")]
        public string Biome = "dungeon";
        public bool IsBossArena;

        [Header("Lighting")]
        public Color AmbientColor = new Color(0.3f, 0.3f, 0.4f);
        public float LightIntensity = 1.0f;

        [Header("Variation")]
        [Tooltip("Prop prefabs that BattleVariation can scatter as obstacles.")]
        public GameObject[] ObstacleProps;
    }
}
