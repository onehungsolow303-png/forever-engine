#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ForeverEngine.Editor
{
    /// <summary>
    /// Repairs Procedural Worlds asset-pack materials whose shaders are
    /// Built-In-RP-only (no UniversalPipeline SubShader tag) and therefore
    /// don't render under URP at all. Rebinds every mat using
    /// PWS/PW_General_Deferred or PWS/PW_General_Forward to the standard
    /// URP/Lit shader, preserving texture references + smoothness/metallic
    /// numeric values, and enabling alpha clip on foliage names.
    ///
    /// 199 .mat files in the PW Gaia Pro Assets and Biomes pack are
    /// affected. Trees, bushes, ferns all bind to these dead shaders;
    /// rebinding unblocks the entire vegetation render path.
    ///
    /// Run headless:
    ///   Unity.exe -batchmode -projectPath "C:/Dev/Forever engine" \
    ///     -executeMethod ForeverEngine.Editor.PWPackMatFixer.Run \
    ///     -quit -logFile "C:/tmp/pw-matfix.log"
    /// </summary>
    public static class PWPackMatFixer
    {
        // PWS/PW_General_Deferred  guid 676d51a93fad6434bb7ee1f64ad89551
        // PWS/PW_General_Forward   guid b59fbf1ddafb64aac89ae315fbb65877
        private static readonly HashSet<string> DeadShaderNames = new HashSet<string>
        {
            "PWS/PW_General_Deferred",
            "PWS/PW_General_Forward",
        };

        // Search roots — confine to the PW packs to avoid disturbing other vendor
        // assets. AssetDatabase.FindAssets ignores roots that don't exist, so
        // listing all known PW dirs is safe even on partial installs.
        private static readonly string[] SearchRoots = new[]
        {
            "Assets/Procedural Worlds/Packages - Install/Gaia Pro Assets and Biomes",
            "Assets/Procedural Worlds/Packages - Install/Gaia",
        };

        [MenuItem("Forever Engine/Fix PW Pack Materials (PWS → URP)")]
        public static void RunMenu() => Run();

        public static void Run()
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("[PWMatFixer] URP/Lit shader not found — is URP installed?");
                return;
            }

            var matGuids = AssetDatabase.FindAssets("t:Material", SearchRoots);
            int rebindCount = 0;
            int skippedNotPWS = 0;
            int alphaClipCount = 0;
            int errorCount = 0;

            foreach (var guid in matGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) { errorCount++; continue; }
                if (mat.shader == null || !DeadShaderNames.Contains(mat.shader.name))
                {
                    skippedNotPWS++;
                    continue;
                }

                // Snapshot textures + numeric values from the PWS shader BEFORE
                // we swap, since rebinding loses the old property bag.
                var mainTex   = mat.HasProperty("_MainTex")            ? mat.GetTexture("_MainTex")            : null;
                var bumpMap   = mat.HasProperty("_BumpMap")            ? mat.GetTexture("_BumpMap")            : null;
                var metalMap  = mat.HasProperty("_MetallicGlossMap")   ? mat.GetTexture("_MetallicGlossMap")   : null;
                var color     = mat.HasProperty("_Color")              ? mat.GetColor("_Color")                : Color.white;
                var cutoff    = mat.HasProperty("_Cutoff")             ? mat.GetFloat("_Cutoff")               : 0.5f;
                var smoothness = mat.HasProperty("_Glossiness")        ? mat.GetFloat("_Glossiness")           : 0.5f;
                // Force _Metallic = 0 regardless of source. PWS authored at 0
                // mostly anyway, but pack mats with metallic maps drive URP
                // wrong (mirror reflection bug — see NatureManufactureMatFixer).
                var bumpScale = mat.HasProperty("_BumpMapScale")       ? mat.GetFloat("_BumpMapScale")         : 1f;

                mat.shader = urpLit;

                if (mainTex   != null && mat.HasProperty("_BaseMap"))           mat.SetTexture("_BaseMap", mainTex);
                if (bumpMap   != null && mat.HasProperty("_BumpMap"))           mat.SetTexture("_BumpMap", bumpMap);
                if (metalMap  != null && mat.HasProperty("_MetallicGlossMap"))  mat.SetTexture("_MetallicGlossMap", metalMap);

                if (mat.HasProperty("_BaseColor"))   mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color"))       mat.SetColor("_Color", color);
                if (mat.HasProperty("_Cutoff"))      mat.SetFloat("_Cutoff", cutoff);
                if (mat.HasProperty("_Smoothness"))  mat.SetFloat("_Smoothness", smoothness);
                if (mat.HasProperty("_Metallic"))    mat.SetFloat("_Metallic", 0f);
                if (mat.HasProperty("_BumpScale"))   mat.SetFloat("_BumpScale", bumpScale);
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);

                // Enable normal map keyword if a bump map is bound.
                if (bumpMap != null) mat.EnableKeyword("_NORMALMAP");

                // Foliage / leaf / needle / cross materials need alpha clip.
                var lname = mat.name.ToLowerInvariant();
                bool isFoliage = lname.Contains("leaves") || lname.Contains("needle") ||
                                 lname.Contains("cross")  || lname.Contains("foliage") ||
                                 lname.Contains("plant")  || lname.Contains("fern")    ||
                                 lname.Contains("grass")  || lname.Contains("bush")    ||
                                 lname.Contains("flower");
                if (isFoliage)
                {
                    if (mat.HasProperty("_AlphaClip"))  mat.SetFloat("_AlphaClip", 1f);
                    if (mat.HasProperty("_Cull"))       mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
                    if (mat.HasProperty("_Surface"))    mat.SetFloat("_Surface", 0f); // opaque
                    mat.EnableKeyword("_ALPHATEST_ON");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                    alphaClipCount++;
                }

                EditorUtility.SetDirty(mat);
                rebindCount++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[PWMatFixer] scanned={matGuids.Length}  rebound-to-URP={rebindCount}  " +
                      $"foliage-alphaclip={alphaClipCount}  skipped-non-PWS={skippedNotPWS}  errors={errorCount}");
        }

        public static void RunBatch()
        {
            try { Run(); EditorApplication.Exit(0); }
            catch (System.Exception e)
            {
                Debug.LogError($"[PWMatFixer] FAIL: {e}");
                EditorApplication.Exit(1);
            }
        }
    }
}
#endif
