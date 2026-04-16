#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ForeverEngine.Editor
{
    public static class CreateWorldScene
    {
        [MenuItem("Forever Engine/Create World Scene")]
        public static void CreateWorld()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            // Add WorldBootstrap to root
            var bootstrapGO = new GameObject("WorldBootstrap");
            bootstrapGO.AddComponent<World.WorldBootstrap>();

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/World.unity");
            Debug.Log("[CreateWorldScene] World.unity created");
        }
    }
}
#endif
