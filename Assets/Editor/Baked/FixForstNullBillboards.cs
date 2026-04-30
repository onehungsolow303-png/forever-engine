#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ForeverEngine.Procedural.Editor
{
    /// <summary>
    /// Forst Conifer prefabs (PF Conifer Bare/Small/Medium/Tall BOTD URP) have a
    /// child GameObject "Conifer * BOTD Billboard" whose Renderer has a NULL
    /// material. Identified via WhiteQuadDiagnostic 2026-04-29 PM. The NULL
    /// material renders as a solid white quad at distance.
    ///
    /// Fix: synthesize a transparent URP/Lit stub material at
    /// Assets/_RecoveryMaterials/Synth/ForstBillboardStub.mat and assign to all
    /// 4 prefabs' billboard children. The stub has alpha=0 so the billboards
    /// render invisible (better than white). True billboard texture would
    /// require vendor-side asset that isn't on disk.
    /// </summary>
    public static class FixForstNullBillboards
    {
        private const string StubPath = "Assets/_RecoveryMaterials/Synth/ForstBillboardStub.mat";

        public static void Run()
        {
            Debug.Log("[FixForstBillboards] === starting ===");

            // 1. Create stub material if missing
            var stub = AssetDatabase.LoadAssetAtPath<Material>(StubPath);
            if (stub == null)
            {
                var urpLit = Shader.Find("Universal Render Pipeline/Lit");
                if (urpLit == null) throw new System.Exception("URP/Lit shader not found");
                stub = new Material(urpLit) { name = "ForstBillboardStub" };
                stub.SetFloat("_Surface", 1f); // Transparent
                stub.SetFloat("_SrcBlend", 5f); // SrcAlpha
                stub.SetFloat("_DstBlend", 10f); // OneMinusSrcAlpha
                stub.SetFloat("_DstBlendAlpha", 10f);
                stub.SetFloat("_ZWrite", 0f);
                stub.SetFloat("_AlphaClip", 0f);
                stub.SetColor("_BaseColor", new Color(0, 0, 0, 0));
                stub.renderQueue = 3000;
                Directory.CreateDirectory(Path.GetDirectoryName(StubPath));
                AssetDatabase.CreateAsset(stub, StubPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[FixForstBillboards] created stub at {StubPath}");
            }
            else
            {
                Debug.Log($"[FixForstBillboards] stub already exists at {StubPath}");
            }

            // 2. Find Forst Conifer prefabs and patch their billboard children
            var prefabPaths = new[]
            {
                "Assets/Forst/Conifers [BOTD]/Render Pipeline Support/URP/Prefabs/PF Conifer Bare BOTD URP.prefab",
                "Assets/Forst/Conifers [BOTD]/Render Pipeline Support/URP/Prefabs/PF Conifer Small BOTD URP.prefab",
                "Assets/Forst/Conifers [BOTD]/Render Pipeline Support/URP/Prefabs/PF Conifer Medium BOTD URP.prefab",
                "Assets/Forst/Conifers [BOTD]/Render Pipeline Support/URP/Prefabs/PF Conifer Tall BOTD URP.prefab",
            };

            int patched = 0;
            foreach (var path in prefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) { Debug.LogWarning($"[FixForstBillboards] missing: {path}"); continue; }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                bool changed = false;
                foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    var newMats = mats.Select(m => m == null ? stub : m).ToArray();
                    if (!mats.SequenceEqual(newMats))
                    {
                        r.sharedMaterials = newMats;
                        Debug.Log($"[FixForstBillboards]   patched {r.gameObject.name} in {prefab.name}");
                        changed = true;
                    }
                }
                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAssetAndConnect(instance, path, InteractionMode.AutomatedAction);
                    patched++;
                }
                Object.DestroyImmediate(instance);
            }

            Debug.Log($"[FixForstBillboards] patched {patched}/{prefabPaths.Length} prefabs");
            Debug.Log("[FixForstBillboards] === DONE ===");
        }
    }
}
#endif
