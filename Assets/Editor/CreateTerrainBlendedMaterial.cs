using System.IO;
using UnityEditor;
using UnityEngine;

namespace ForeverEngine.Procedural.Editor
{
    /// <summary>
    /// Menu: Forever Engine → Create Terrain Blended Material.
    /// Creates Assets/Resources/TerrainBlendedMaterial.mat + a greyscale detail
    /// texture if they don't already exist. Idempotent: re-running skips if
    /// the material is present. Used by the Phase 4B blending pipeline.
    /// </summary>
    public static class CreateTerrainBlendedMaterial
    {
        private const string MatPath = "Assets/Resources/TerrainBlendedMaterial.mat";
        private const string TexPath = "Assets/Resources/TerrainBlendedDetail.png";
        private const string ShaderName = "ForeverEngine/TerrainBlended";

        [MenuItem("Forever Engine/Create Terrain Blended Material")]
        public static void Create()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MatPath));

            // 1. Detail texture.
            var existingTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath);
            if (existingTex == null)
            {
                var tex = new Texture2D(128, 128, TextureFormat.RGBA32, mipChain: true, linear: false);
                var rand = new System.Random(0x12345);
                var pixels = new Color32[128 * 128];
                for (int i = 0; i < pixels.Length; i++)
                {
                    byte v = (byte)(160 + rand.Next(0, 96));  // 160..255 greyscale
                    pixels[i] = new Color32(v, v, v, 255);
                }
                tex.SetPixels32(pixels);
                tex.Apply();
                File.WriteAllBytes(TexPath, tex.EncodeToPNG());
                AssetDatabase.ImportAsset(TexPath);
                existingTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath);
                Debug.Log($"[TerrainBlended] Wrote detail texture to {TexPath}");
            }

            // 2. Material.
            var existingMat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (existingMat != null)
            {
                Debug.Log($"[TerrainBlended] Material already exists at {MatPath}; leaving untouched.");
                Selection.activeObject = existingMat;
                return;
            }

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                EditorUtility.DisplayDialog("Create Terrain Blended Material",
                    $"Shader '{ShaderName}' not found. Ensure TerrainBlended.shader is imported and compiles cleanly before running this menu.", "OK");
                return;
            }

            var mat = new Material(shader) { name = "TerrainBlendedMaterial" };
            mat.SetTexture("_BaseMap", existingTex);
            mat.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(mat, MatPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = mat;
            Debug.Log($"[TerrainBlended] Created material at {MatPath}");
        }
    }
}
