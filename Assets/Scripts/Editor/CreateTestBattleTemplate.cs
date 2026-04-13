#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace ForeverEngine.Editor
{
    public static class CreateTestBattleTemplate
    {
        [MenuItem("Forever/Create Test Battle Template")]
        public static void Create()
        {
            var template = ScriptableObject.CreateInstance<Demo.Battle.BattleSceneTemplate>();
            template.Biome = "dungeon";
            template.GridWidth = 8;
            template.GridHeight = 8;
            template.PlayerSpawnZone = new[] {
                new Vector2Int(1, 1), new Vector2Int(1, 2), new Vector2Int(2, 1)
            };
            template.EnemySpawnZone = new[] {
                new Vector2Int(5, 5), new Vector2Int(5, 6), new Vector2Int(6, 5), new Vector2Int(6, 6)
            };
            template.BossSpawnPoints = new[] { new Vector2Int(4, 4) };

            string dir = "Assets/Resources/BattleTemplates/dungeon";
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/BattleTemplates"))
                AssetDatabase.CreateFolder("Assets/Resources", "BattleTemplates");
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder("Assets/Resources/BattleTemplates", "dungeon");
            AssetDatabase.CreateAsset(template, $"{dir}/TestDungeon_01.asset");
            AssetDatabase.SaveAssets();
            Debug.Log("[CreateTestBattleTemplate] Created TestDungeon_01 at " + dir);
        }
    }
}
#endif
