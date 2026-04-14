using UnityEngine;

namespace ForeverEngine.Demo.Battle
{
    /// <summary>DEPRECATED: Replaced by seamless BattleZone system.</summary>
    public enum ArenaType { Dungeon, Boss, Overworld }

    [CreateAssetMenu(fileName = "BattleTemplate", menuName = "Forever/Battle Scene Template")]
    public class BattleSceneTemplate : ScriptableObject
    {
        public GameObject RoomPrefab;
        public int GridWidth = 8;
        public int GridHeight = 8;
        public ArenaType Arena = ArenaType.Dungeon;
        public string Biome = "dungeon";
        public bool IsBossArena;
        public Color AmbientColor = new Color(0.3f, 0.3f, 0.4f);
        public float LightIntensity = 1.0f;
        public Vector2Int[] PlayerSpawnZone;
        public Vector2Int[] EnemySpawnZone;
        public Vector2Int[] BossSpawnPoints;
        public GameObject[] ObstacleProps;
    }
}
