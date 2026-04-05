using UnityEngine;
using UnityEditor;
using ForeverEngine.AssetGeneration;

namespace ForeverEngine.Editor
{
    public static class AssetGenerationTab
    {
        private static int _tokenSize = 32;
        private static Color _tokenColor = Color.blue;
        private static int _texWidth = 64, _texHeight = 64;
        private static int _seed = 42;

        public static void Draw()
        {
            EditorGUILayout.LabelField("Asset Generation", EditorStyles.boldLabel);

            EditorGUILayout.LabelField("Creature Tokens", EditorStyles.miniBoldLabel);
            _tokenSize = EditorGUILayout.IntSlider("Size", _tokenSize, 16, 64);
            _tokenColor = EditorGUILayout.ColorField("Color", _tokenColor);
            if (GUILayout.Button("Generate Token"))
            {
                var tex = ProceduralSpriteGenerator.GenerateCreatureToken(_tokenColor, _tokenSize);
                string path = $"Assets/Resources/Sprites/token_{System.DateTime.Now.Ticks}.png";
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                AssetDatabase.Refresh();
                Debug.Log($"[AssetGen] Token saved to {path}");
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Terrain Textures", EditorStyles.miniBoldLabel);
            _texWidth = EditorGUILayout.IntSlider("Width", _texWidth, 16, 256);
            _texHeight = EditorGUILayout.IntSlider("Height", _texHeight, 16, 256);
            _seed = EditorGUILayout.IntField("Seed", _seed);
            if (GUILayout.Button("Generate Terrain Texture"))
            {
                var tex = TextureGenerator.GenerateTerrainTexture(_texWidth, _texHeight, new Color(0.47f, 0.39f, 0.31f), new Color(0.16f, 0.16f, 0.2f), _seed);
                string path = $"Assets/Resources/Tilesets/terrain_{_seed}.png";
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                AssetDatabase.Refresh();
                Debug.Log($"[AssetGen] Terrain texture saved to {path}");
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Build Asset Manifest"))
            {
                var manifest = AssetManifestBuilder.Build(Application.dataPath + "/Resources");
                AssetManifestBuilder.SaveManifest(manifest, Application.dataPath + "/../asset_manifest.json");
            }
        }
    }
}
