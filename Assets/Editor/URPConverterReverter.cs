using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ForeverEngine.Editor
{
    /// <summary>
    /// Recovery script. URPSetupAndMaterialConverter's "Unknown shader → force URP/Lit"
    /// fallback overwrote ~486 valid custom URP shaders. This script reads the damage
    /// log (urp-converter-damage.log at project root), extracts each (original-shader-name,
    /// material-path) pair, and restores the shader binding. Texture/color/float properties
    /// are still serialized inside each .mat file, so they snap back into place when the
    /// original shader is re-bound.
    /// </summary>
    public static class URPConverterReverter
    {
        private const string LogPath = "urp-converter-damage.log";

        // Matches: [URPConverter] Unknown shader 'NAME' on PATH — forcing URP/Lit.
        // The em-dash is U+2014.
        private static readonly Regex UnknownLine = new Regex(
            @"\[URPConverter\] Unknown shader '([^']+)' on (.+?) \u2014 forcing URP/Lit\.",
            RegexOptions.Compiled);

        [MenuItem("Forever Engine/Revert URP Converter Overrides")]
        public static void Run()
        {
            string logFile = Path.Combine(Directory.GetCurrentDirectory(), LogPath);
            if (!File.Exists(logFile))
            {
                Debug.LogError($"[Reverter] Damage log not found at {logFile}");
                return;
            }

            string[] lines = File.ReadAllLines(logFile);

            var pairs = new List<(string ShaderName, string MatPath)>();
            var seenPaths = new HashSet<string>();
            foreach (string line in lines)
            {
                var m = UnknownLine.Match(line);
                if (!m.Success) continue;
                string shaderName = m.Groups[1].Value;
                string matPath = m.Groups[2].Value;
                if (seenPaths.Add(matPath))
                    pairs.Add((shaderName, matPath));
            }
            Debug.Log($"[Reverter] Parsed {pairs.Count} unique material overrides from {logFile}.");

            // Build name → Shader index from the project (covers .shader and .shadergraph)
            var shaderIndex = new Dictionary<string, Shader>();
            string[] shaderGuids = AssetDatabase.FindAssets("t:Shader");
            foreach (string guid in shaderGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Shader s = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (s == null || string.IsNullOrEmpty(s.name)) continue;
                if (!shaderIndex.ContainsKey(s.name))
                    shaderIndex[s.name] = s;
            }
            Debug.Log($"[Reverter] Indexed {shaderIndex.Count} shaders by name.");

            int restored = 0;
            int alreadyOk = 0;
            int shaderMissing = 0;
            int matMissing = 0;
            var missingShaderNames = new HashSet<string>();

            foreach (var (shaderName, matPath) in pairs)
            {
                if (!shaderIndex.TryGetValue(shaderName, out Shader shader))
                {
                    // Fallback: Shader.Find may resolve built-in shaders not in AssetDatabase
                    shader = Shader.Find(shaderName);
                }

                if (shader == null)
                {
                    if (missingShaderNames.Add(shaderName))
                        Debug.LogWarning($"[Reverter] Shader not found: '{shaderName}' (first occurrence: {matPath})");
                    shaderMissing++;
                    continue;
                }

                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    Debug.LogWarning($"[Reverter] Material not found: {matPath}");
                    matMissing++;
                    continue;
                }

                if (mat.shader == shader)
                {
                    alreadyOk++;
                    continue;
                }

                mat.shader = shader;
                EditorUtility.SetDirty(mat);
                restored++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Reverter] Done. Restored: {restored}, Already OK: {alreadyOk}, " +
                      $"Shader missing: {shaderMissing} ({missingShaderNames.Count} unique), " +
                      $"Material missing: {matMissing}");
        }
    }
}
