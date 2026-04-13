using UnityEngine;

namespace ForeverEngine.Demo.Dungeon
{
    /// <summary>
    /// Rule for spawning a friendly NPC (Merchant, Prisoner, QuestGiver) in a dungeon.
    /// </summary>
    [System.Serializable]
    public class FriendlyNPCRule
    {
        public DungeonNPCRole Role;
        public int[] TierFilter = { 1 };
        [Range(0f, 1f)] public float SpawnChance = 0.5f;
        public int MaxPerDungeon = 1;
        public string[] ModelKeys = { "Default_Player" };
    }

    /// <summary>
    /// Rule for spawning ambient enemies (patrols) in a dungeon.
    /// </summary>
    [System.Serializable]
    public class AmbientEnemyRule
    {
        public int[] TierFilter = { 2, 3 };
        public string[] EnemyNames = { "Skeleton" };
        public Vector2Int CountRange = new(1, 2);
        public float PatrolRadius = 3f;
    }

    /// <summary>
    /// ScriptableObject configuration for NPC spawn rules across all dungeons.
    /// Defines which NPCs (friendly and enemy) can spawn, their tiers, chances, and patrol parameters.
    ///
    /// Created via Assets > Create > Dungeon NPC Config
    /// Referenced by DungeonNPCSpawner for runtime NPC instantiation.
    /// </summary>
    [CreateAssetMenu(fileName = "DungeonNPCConfig", menuName = "Dungeon NPC Config")]
    public class DungeonNPCConfig : ScriptableObject
    {
        [Header("Friendly NPCs")]
        public FriendlyNPCRule[] FriendlyRules = new[]
        {
            new FriendlyNPCRule
            {
                Role = DungeonNPCRole.Merchant,
                TierFilter = new[] { 1 },
                SpawnChance = 0.7f,
                MaxPerDungeon = 1,
                ModelKeys = new[] { "Default_Player" }
            },
            new FriendlyNPCRule
            {
                Role = DungeonNPCRole.Prisoner,
                TierFilter = new[] { 1, 2 },
                SpawnChance = 0.4f,
                MaxPerDungeon = 2,
                ModelKeys = new[] { "Default_Player" }
            },
            new FriendlyNPCRule
            {
                Role = DungeonNPCRole.QuestGiver,
                TierFilter = new[] { 3 },
                SpawnChance = 0.3f,
                MaxPerDungeon = 1,
                ModelKeys = new[] { "Default_Player" }
            }
        };

        [Header("Ambient Enemies")]
        public AmbientEnemyRule[] EnemyRules = new[]
        {
            new AmbientEnemyRule
            {
                TierFilter = new[] { 2, 3 },
                EnemyNames = new[] { "Skeleton", "Lizard Folk" },
                CountRange = new Vector2Int(1, 2),
                PatrolRadius = 3f
            }
        };

        [Header("Director Integration")]
        public bool DirectorOverrides = true;
    }
}
