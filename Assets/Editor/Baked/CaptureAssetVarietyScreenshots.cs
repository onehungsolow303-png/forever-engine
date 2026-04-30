#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ForeverEngine.Procedural.Editor
{
    public static class CaptureAssetVarietyScreenshots
    {
        private const string ScenePath = "Assets/Scenes/GaiaWorld_Coniferous_Forest_Medium.unity";
        private const string OutputDir = "C:/tmp/asset-showcase";
        private const int Width = 2560;
        private const int Height = 1440;

        public static void Run()
        {
            Debug.Log("[ShowcaseShots] === starting ===");
            Directory.CreateDirectory(OutputDir);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid()) throw new System.Exception($"Couldn't open {ScenePath}");

            var overlay = GameObject.Find("AssetVarietyOverlay");
            if (overlay == null) throw new System.Exception("No AssetVarietyOverlay GameObject in scene — run AddAssetVarietyOverlay.Run first");

            // Compute aggregate bounds of all overlay children
            var renderers = overlay.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new System.Exception("Overlay has no renderers");
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            var center = bounds.center;
            var size = bounds.size;
            float radius = Mathf.Max(size.x, size.z) * 0.6f;
            Debug.Log($"[ShowcaseShots] overlay bounds center={center} size={size} radius={radius:F1}");

            // Set up sun
            var sun = new GameObject("ShowcaseSun");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            light.color = new Color(1.0f, 0.96f, 0.86f);
            sun.transform.rotation = Quaternion.Euler(50, 30, 0);
            RenderSettings.ambientIntensity = 1.2f;

            // Camera
            var camGo = new GameObject("ShowcaseCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 2000;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.allowHDR = true;

            // Shot table: angle name + (camera offset from center, look-at offset)
            var shots = new (string name, Vector3 camOffset, Vector3 lookAtOffset)[]
            {
                ("01-front-eye-level",  new Vector3(0,            6f,   -radius * 1.2f), new Vector3(0, 4f, 0)),
                ("02-front-elevated",   new Vector3(0,            radius * 0.8f, -radius * 1.5f), new Vector3(0, 0, 0)),
                ("03-side-elevated",    new Vector3(radius * 1.4f, radius * 0.9f, 0), new Vector3(0, 0, 0)),
                ("04-rear-elevated",    new Vector3(0,            radius * 0.9f, radius * 1.4f),  new Vector3(0, 0, 0)),
                ("05-top-down",         new Vector3(0,            radius * 1.8f, 0.01f), new Vector3(0, 0, 0)),
                ("06-walkthrough-corner", new Vector3(-radius * 0.3f, 3f, -radius * 0.3f), new Vector3(radius * 0.3f, 1.5f, radius * 0.3f)),
                ("07-walkthrough-low",    new Vector3(-radius * 0.6f, 2f,  0), new Vector3(radius * 0.5f, 2f, 0)),
            };

            foreach (var (name, off, lookOff) in shots)
            {
                camGo.transform.position = center + off;
                camGo.transform.LookAt(center + lookOff);

                var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                cam.targetTexture = rt;
                cam.Render();

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;

                var path = $"{OutputDir}/asset-showcase-{name}.png";
                File.WriteAllBytes(path, tex.EncodeToPNG());
                cam.targetTexture = null;
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
                Debug.Log($"[ShowcaseShots] wrote {path}");
            }

            Debug.Log("[ShowcaseShots] === DONE ===");
        }
    }
}
#endif
