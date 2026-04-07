using UnityEngine;
using UnityEditor;

namespace ForeverEngine.Editor
{
    public class ForeverEngineEditor : EditorWindow
    {
        private int _selectedTab;
        // Phase 3 pivot: AssetGenerationTab archived alongside the AssetGeneration
        // C# code (now reimplemented in Asset Manager Python). The tab will be
        // reintroduced as an HTTP-bridge inspector in a follow-up.
        private static readonly string[] TabNames = { "Generation", "Preview" };

        [MenuItem("Forever Engine/Open Editor %#e")]
        public static void Open() => GetWindow<ForeverEngineEditor>("Forever Engine");

        private void OnGUI()
        {
            _selectedTab = GUILayout.Toolbar(_selectedTab, TabNames, GUILayout.Height(30));
            EditorGUILayout.Space(10);

            switch (_selectedTab)
            {
                case 0: GenerationTab.Draw(); break;
                case 1: MapPreviewTab.Draw(); break;
            }
        }
    }
}
