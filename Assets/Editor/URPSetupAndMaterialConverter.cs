using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.IO;
using System.Collections.Generic;

namespace ForeverEngine.Editor
{
    /// <summary>
    /// Batchmode-safe script that:
    /// 1. Creates a project-level URP pipeline asset + forward renderer
    /// 2. Assigns it to GraphicsSettings and all QualitySettings levels
    /// 3. Converts all Built-in RP and HDRP materials to URP/Lit
    ///
    /// Run from menu: Forever Engine > Setup URP & Convert Materials
    /// Run headless:  Unity.exe -batchmode -executeMethod ForeverEngine.Editor.URPSetupAndMaterialConverter.Run -quit -logFile -
    /// </summary>
    public static class URPSetupAndMaterialConverter
    {
        private const string URPAssetPath = "Assets/Settings/ForeverEngine-URP.asset";
        private const string RendererPath = "Assets/Settings/ForeverEngine-URP-Renderer.asset";

        [MenuItem("Forever Engine/Setup URP && Convert Materials")]
        public static void Run()
        {
            Debug.Log("[URPConverter] Starting URP setup and material conversion...");

            EnsureSettingsFolder();
            var urpAsset = CreateOrLoadURPAsset();
            AssignToGraphicsAndQuality(urpAsset);
            int converted = ConvertAllMaterials();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[URPConverter] Done. URP assigned. {converted} materials converted.");
        }

        private static void EnsureSettingsFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Settings"))
                AssetDatabase.CreateFolder("Assets", "Settings");
        }

        private static UniversalRenderPipelineAsset CreateOrLoadURPAsset()
        {
            // If we already created one, reuse it
            var existing = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(URPAssetPath);
            if (existing != null)
            {
                Debug.Log("[URPConverter] Reusing existing URP asset.");
                return existing;
            }

            // Create forward renderer data
            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, RendererPath);

            // Create pipeline asset referencing the renderer
            var pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
            pipelineAsset.name = "ForeverEngine-URP";

            // Configure for quality
            pipelineAsset.shadowDistance = 100f;
            pipelineAsset.supportsCameraOpaqueTexture = true;
            pipelineAsset.supportsCameraDepthTexture = true;

            AssetDatabase.CreateAsset(pipelineAsset, URPAssetPath);
            Debug.Log("[URPConverter] Created new URP pipeline asset.");

            return pipelineAsset;
        }

        private static void AssignToGraphicsAndQuality(UniversalRenderPipelineAsset urpAsset)
        {
            // Assign to GraphicsSettings default pipeline
            GraphicsSettings.defaultRenderPipeline = urpAsset;
            Debug.Log("[URPConverter] Assigned URP to GraphicsSettings.defaultRenderPipeline.");

            // Assign to all quality levels
            int levels = QualitySettings.names.Length;
            for (int i = 0; i < levels; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urpAsset;
            }
            // Restore to Ultra (index 5)
            QualitySettings.SetQualityLevel(levels - 1, true);
            Debug.Log($"[URPConverter] Assigned URP to all {levels} quality levels.");
        }

        private static int ConvertAllMaterials()
        {
            // Target asset pack folders (not our own scripts/resources)
            string[] searchFolders = {
                "Assets/Eternal Temple",
                "Assets/EternalTemple",
                "Assets/Lordenfel",
                "Assets/Magic Pig Games (Infinity PBR)",
                "Assets/Multistory Dungeons",
                "Assets/Multistory Dungeons 2",
                "Assets/NAKED_SINGULARITY",
                "Assets/NatureManufacture Assets",
                "Assets/GeneratedModels",
                "Assets/Prefabs",
            };

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            Shader urpSimpleLit = Shader.Find("Universal Render Pipeline/Simple Lit");
            Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");

            if (urpLit == null)
            {
                Debug.LogError("[URPConverter] Cannot find URP/Lit shader! Is URP package installed?");
                return 0;
            }

            int converted = 0;
            int skipped = 0;
            int errors = 0;

            // Find all materials in the project
            string[] matGuids = AssetDatabase.FindAssets("t:Material", searchFolders);
            Debug.Log($"[URPConverter] Found {matGuids.Length} materials to check.");

            foreach (string guid in matGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                string shaderName = mat.shader != null ? mat.shader.name : "null";

                // Skip materials already on URP shaders
                if (shaderName.StartsWith("Universal Render Pipeline/") ||
                    shaderName.StartsWith("Shader Graphs/"))
                {
                    skipped++;
                    continue;
                }

                try
                {
                    if (TryConvertBuiltIn(mat, urpLit, urpSimpleLit, urpUnlit, shaderName))
                    {
                        EditorUtility.SetDirty(mat);
                        converted++;
                    }
                    else if (TryConvertHDRP(mat, urpLit, shaderName))
                    {
                        EditorUtility.SetDirty(mat);
                        converted++;
                    }
                    else
                    {
                        // Unknown shader — force to URP/Lit as fallback so it's
                        // at least not pink. Textures may need manual fixup.
                        Debug.LogWarning($"[URPConverter] Unknown shader '{shaderName}' on {path} — forcing URP/Lit.");
                        mat.shader = urpLit;
                        EditorUtility.SetDirty(mat);
                        converted++;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[URPConverter] Error converting {path}: {ex.Message}");
                    errors++;
                }
            }

            Debug.Log($"[URPConverter] Converted: {converted}, Already URP: {skipped}, Errors: {errors}");
            return converted;
        }

        /// <summary>
        /// Convert Built-in RP Standard/Legacy shaders to URP/Lit.
        /// Returns true if this material was a Built-in shader and was converted.
        /// </summary>
        private static bool TryConvertBuiltIn(Material mat, Shader urpLit, Shader urpSimpleLit, Shader urpUnlit, string shaderName)
        {
            bool isStandard = shaderName == "Standard" || shaderName == "Standard (Specular setup)";
            bool isLegacy = shaderName.StartsWith("Legacy Shaders/");
            bool isMobile = shaderName.StartsWith("Mobile/");
            bool isUnlit = shaderName.Contains("Unlit") || shaderName == "Unlit/Texture" ||
                           shaderName == "Unlit/Color" || shaderName == "Unlit/Transparent";
            bool isParticle = shaderName.StartsWith("Particles/");
            bool isNature = shaderName.StartsWith("Nature/");

            if (!isStandard && !isLegacy && !isMobile && !isUnlit && !isParticle && !isNature)
                return false;

            // Capture texture references before shader swap
            Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
            Texture bumpMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
            float bumpScale = mat.HasProperty("_BumpScale") ? mat.GetFloat("_BumpScale") : 1f;
            Texture metallicMap = mat.HasProperty("_MetallicGlossMap") ? mat.GetTexture("_MetallicGlossMap") : null;
            float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
            float smoothness = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") :
                               mat.HasProperty("_GlossMapScale") ? mat.GetFloat("_GlossMapScale") : 0.5f;
            Texture occlusionMap = mat.HasProperty("_OcclusionMap") ? mat.GetTexture("_OcclusionMap") : null;
            float occlusionStr = mat.HasProperty("_OcclusionStrength") ? mat.GetFloat("_OcclusionStrength") : 1f;
            Texture emissionMap = mat.HasProperty("_EmissionMap") ? mat.GetTexture("_EmissionMap") : null;
            Color emissionColor = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;
            Texture detailAlbedo = mat.HasProperty("_DetailAlbedoMap") ? mat.GetTexture("_DetailAlbedoMap") : null;
            Texture detailNormal = mat.HasProperty("_DetailNormalMap") ? mat.GetTexture("_DetailNormalMap") : null;

            // Swap shader
            if (isUnlit)
                mat.shader = urpUnlit;
            else
                mat.shader = urpLit;

            // Remap properties — URP/Lit uses _BaseMap/_BaseColor instead of _MainTex/_Color
            if (mainTex != null && mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", mainTex);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);

            if (bumpMap != null && mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", bumpMap);
                mat.SetFloat("_BumpScale", bumpScale);
            }

            if (metallicMap != null && mat.HasProperty("_MetallicGlossMap"))
                mat.SetTexture("_MetallicGlossMap", metallicMap);
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", smoothness);

            if (occlusionMap != null && mat.HasProperty("_OcclusionMap"))
            {
                mat.SetTexture("_OcclusionMap", occlusionMap);
                mat.SetFloat("_OcclusionStrength", occlusionStr);
            }

            if (emissionMap != null && mat.HasProperty("_EmissionMap"))
            {
                mat.SetTexture("_EmissionMap", emissionMap);
                mat.SetColor("_EmissionColor", emissionColor);
                mat.EnableKeyword("_EMISSION");
            }
            else if (emissionColor != Color.black && mat.HasProperty("_EmissionColor"))
            {
                mat.SetColor("_EmissionColor", emissionColor);
                mat.EnableKeyword("_EMISSION");
            }

            if (detailAlbedo != null && mat.HasProperty("_DetailAlbedoMap"))
                mat.SetTexture("_DetailAlbedoMap", detailAlbedo);
            if (detailNormal != null && mat.HasProperty("_DetailNormalMapScale"))
                mat.SetTexture("_DetailNormalMap", detailNormal);

            // Preserve render mode (transparent, cutout, etc.)
            if (mat.HasProperty("_Surface"))
            {
                // Check original render queue for transparency
                if (mat.renderQueue >= 3000)
                {
                    mat.SetFloat("_Surface", 1); // Transparent
                    mat.SetFloat("_Blend", 0);   // Alpha blend
                }
                else if (mat.renderQueue == 2450)
                {
                    mat.SetFloat("_Surface", 0); // Opaque
                    mat.SetFloat("_AlphaClip", 1); // Alpha cutout
                }
            }

            return true;
        }

        /// <summary>
        /// Convert HDRP/Lit and HDRP variants to URP/Lit.
        /// HDRP uses different texture property names than Built-in.
        /// </summary>
        private static bool TryConvertHDRP(Material mat, Shader urpLit, string shaderName)
        {
            if (!shaderName.StartsWith("HDRP/") && !shaderName.StartsWith("HDRenderPipeline/"))
                return false;

            // HDRP property names
            Texture baseMap = mat.HasProperty("_BaseColorMap") ? mat.GetTexture("_BaseColorMap") : null;
            Color baseColor = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
            Texture normalMap = mat.HasProperty("_NormalMap") ? mat.GetTexture("_NormalMap") : null;
            float normalScale = mat.HasProperty("_NormalScale") ? mat.GetFloat("_NormalScale") : 1f;
            Texture maskMap = mat.HasProperty("_MaskMap") ? mat.GetTexture("_MaskMap") : null;
            float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
            float smoothness = mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") : 0.5f;
            Texture emissiveMap = mat.HasProperty("_EmissiveColorMap") ? mat.GetTexture("_EmissiveColorMap") : null;
            Color emissiveColor = mat.HasProperty("_EmissiveColor") ? mat.GetColor("_EmissiveColor") : Color.black;
            Texture detailMap = mat.HasProperty("_DetailMap") ? mat.GetTexture("_DetailMap") : null;
            Texture heightMap = mat.HasProperty("_HeightMap") ? mat.GetTexture("_HeightMap") : null;

            // Swap to URP/Lit
            mat.shader = urpLit;

            // Remap textures
            if (baseMap != null && mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", baseMap);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", baseColor);

            if (normalMap != null && mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", normalMap);
                mat.SetFloat("_BumpScale", normalScale);
            }

            // HDRP MaskMap (R=metallic, G=AO, A=smoothness) → URP separate maps
            // Use it as metallic map — not perfect but better than nothing
            if (maskMap != null && mat.HasProperty("_MetallicGlossMap"))
                mat.SetTexture("_MetallicGlossMap", maskMap);

            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", smoothness);

            if (emissiveMap != null && mat.HasProperty("_EmissionMap"))
            {
                mat.SetTexture("_EmissionMap", emissiveMap);
                mat.EnableKeyword("_EMISSION");
            }
            if (emissiveColor != Color.black && mat.HasProperty("_EmissionColor"))
            {
                mat.SetColor("_EmissionColor", emissiveColor);
                mat.EnableKeyword("_EMISSION");
            }

            if (heightMap != null && mat.HasProperty("_ParallaxMap"))
                mat.SetTexture("_ParallaxMap", heightMap);

            return true;
        }
    }
}
