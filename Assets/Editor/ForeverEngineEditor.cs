using UnityEngine;
using UnityEditor;

namespace ForeverEngine.Editor
{
    public class ForeverEngineEditor : EditorWindow
    {
        private int _selectedTab;
        private static readonly string[] TabNames = { "Generation", "Assets", "Preview" };

        [MenuItem("Forever Engine/Open Editor %#e")]
        public static void Open() => GetWindow<ForeverEngineEditor>("Forever Engine");

        private void OnGUI()
        {
            _selectedTab = GUILayout.Toolbar(_selectedTab, TabNames, GUILayout.Height(30));
            EditorGUILayout.Space(10);

            switch (_selectedTab)
            {
                case 0: GenerationTab.Draw(); break;
                case 1: AssetGenerationTab.Draw(); break;
                case 2: MapPreviewTab.Draw(); break;
            }
        }
    }
}
