using UnityEngine;
using UnityEditor;

namespace ForeverEngine.Editor
{
    /// <summary>
    /// Repairs NatureManufacture (and other) materials that have HDRP-style texture
    /// property names serialized but are using the URP/Lit shader which expects
    /// different property names. Copies texture references from HDRP slots to URP slots.
    ///
    /// Run headless: Unity.exe -batchmode -executeMethod ForeverEngine.Editor.NatureManufactureMatFixer.Run -quit -logFile -
    /// </summary>
    public static class NatureManufactureMatFixer
    {
        [MenuItem("Forever Engine/Fix HDRP Material Properties")]
        public static void Run()
        {
            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[]
            {
                "Assets/NatureManufacture Assets",
                "Assets/Lordenfel",
                "Assets/NAKED_SINGULARITY",
                "Assets/Prefabs/Overworld"
            });

            int fixed_count = 0;
            int skipped = 0;

            foreach (string guid in matGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                // Use SerializedObject to access raw saved properties regardless of current shader
                var so = new SerializedObject(mat);
                var texEnvs = so.FindProperty("m_SavedProperties.m_TexEnvs");
                if (texEnvs == null) { skipped++; continue; }

                bool changed = false;

                // Remap _BaseColorMap → _BaseMap (albedo)
                changed |= CopyTextureIfEmpty(texEnvs, "_BaseColorMap", "_BaseMap");

                // Remap _NormalMap → _BumpMap (normal) — only if _BumpMap is empty
                changed |= CopyTextureIfEmpty(texEnvs, "_NormalMap", "_BumpMap");

                // Remap _MaskMap → _MetallicGlossMap
                changed |= CopyTextureIfEmpty(texEnvs, "_MaskMap", "_MetallicGlossMap");

                // Also copy _BaseColorMap → _MainTex for any fallback references
                changed |= CopyTextureIfEmpty(texEnvs, "_BaseColorMap", "_MainTex");

                // Enable alpha clip for leaf/cross materials
                string lowerName = mat.name.ToLowerInvariant();
                if (lowerName.Contains("leaves") || lowerName.Contains("cross") ||
                    lowerName.Contains("foliage") || lowerName.Contains("grass"))
                {
                    var floats = so.FindProperty("m_SavedProperties.m_Floats");
                    if (floats != null)
                    {
                        changed |= SetFloatProperty(floats, "_AlphaClip", 1f);
                        changed |= SetFloatProperty(floats, "_Cutoff", 0.5f);
                        // Set render queue for alpha test
                        var renderQueue = so.FindProperty("m_CustomRenderQueue");
                        if (renderQueue != null && renderQueue.intValue < 2450)
                        {
                            renderQueue.intValue = 2450;
                            changed = true;
                        }
                    }
                }

                if (changed)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(mat);
                    fixed_count++;
                }
                else
                {
                    skipped++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[MatFixer] Fixed: {fixed_count}, Skipped (already ok): {skipped}");
        }

        private static bool CopyTextureIfEmpty(SerializedProperty texEnvs, string srcName, string dstName)
        {
            SerializedProperty srcEntry = null;
            SerializedProperty dstEntry = null;

            for (int i = 0; i < texEnvs.arraySize; i++)
            {
                var entry = texEnvs.GetArrayElementAtIndex(i);
                var key = entry.FindPropertyRelative("first");
                if (key == null) continue;
                string name = key.stringValue;
                if (name == srcName) srcEntry = entry;
                if (name == dstName) dstEntry = entry;
            }

            if (srcEntry == null) return false;

            var srcTex = srcEntry.FindPropertyRelative("second.m_Texture");
            if (srcTex == null || srcTex.objectReferenceValue == null) return false;

            // If destination doesn't exist, create it
            if (dstEntry == null)
            {
                texEnvs.InsertArrayElementAtIndex(texEnvs.arraySize);
                dstEntry = texEnvs.GetArrayElementAtIndex(texEnvs.arraySize - 1);
                var dstKey = dstEntry.FindPropertyRelative("first");
                dstKey.stringValue = dstName;
            }

            var dstTex = dstEntry.FindPropertyRelative("second.m_Texture");
            if (dstTex == null) return false;

            // Only copy if destination is empty
            if (dstTex.objectReferenceValue != null) return false;

            dstTex.objectReferenceValue = srcTex.objectReferenceValue;

            // Also copy scale/offset
            var srcScale = srcEntry.FindPropertyRelative("second.m_Scale");
            var dstScale = dstEntry.FindPropertyRelative("second.m_Scale");
            if (srcScale != null && dstScale != null)
                dstScale.vector2Value = srcScale.vector2Value;

            var srcOffset = srcEntry.FindPropertyRelative("second.m_Offset");
            var dstOffset = dstEntry.FindPropertyRelative("second.m_Offset");
            if (srcOffset != null && dstOffset != null)
                dstOffset.vector2Value = srcOffset.vector2Value;

            return true;
        }

        private static bool SetFloatProperty(SerializedProperty floats, string name, float value)
        {
            for (int i = 0; i < floats.arraySize; i++)
            {
                var entry = floats.GetArrayElementAtIndex(i);
                var key = entry.FindPropertyRelative("first");
                if (key != null && key.stringValue == name)
                {
                    var val = entry.FindPropertyRelative("second");
                    if (val != null && !Mathf.Approximately(val.floatValue, value))
                    {
                        val.floatValue = value;
                        return true;
                    }
                    return false;
                }
            }

            // Property doesn't exist, add it
            floats.InsertArrayElementAtIndex(floats.arraySize);
            var newEntry = floats.GetArrayElementAtIndex(floats.arraySize - 1);
            newEntry.FindPropertyRelative("first").stringValue = name;
            newEntry.FindPropertyRelative("second").floatValue = value;
            return true;
        }
    }
}
