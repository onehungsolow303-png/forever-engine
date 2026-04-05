using UnityEditor;

namespace ForeverEngine.Editor
{
    public static class DemoBuildSettings
    {
        [MenuItem("Forever Engine/Configure Build Settings")]
        public static void Configure()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Overworld.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/BattleMap.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Game.unity", true)
            };
            UnityEngine.Debug.Log("[BuildSettings] Demo scenes configured: MainMenu (0), Overworld (1), BattleMap (2), Game (3)");
        }
    }
}
