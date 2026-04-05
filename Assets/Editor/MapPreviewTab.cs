using UnityEngine;
using UnityEditor;
using ForeverEngine.Generation;
using ForeverEngine.Generation.Data;

namespace ForeverEngine.Editor
{
    public static class MapPreviewTab
    {
        private static Texture2D _previewTexture;

        public static void Draw()
        {
            EditorGUILayout.LabelField("Map Preview", EditorStyles.boldLabel);

            if (GUILayout.Button("Generate Preview (64x64 dungeon)"))
            {
                var req = new GenerationRequest { MapType = "dungeon", Width = 64, Height = 64, Seed = Random.Range(1, 99999), PartyLevel = 3, PartySize = 4 };
                var result = PipelineCoordinator.Generate(req);
                if (result.Success) _previewTexture = BuildPreview(result, 64, 64);
                else Debug.LogWarning($"Preview failed: {result.Error}");
            }

            if (_previewTexture != null)
            {
                EditorGUILayout.Space();
                float maxSize = Mathf.Min(EditorGUIUtility.currentViewWidth - 20, 400);
                var rect = GUILayoutUtility.GetRect(maxSize, maxSize);
                GUI.DrawTexture(rect, _previewTexture, ScaleMode.ScaleToFit);
            }
        }

        private static Texture2D BuildPreview(PipelineCoordinator.GenerationResult result, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;

            // Draw terrain
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    int ci = idx * 3;
                    if (ci + 2 < result.Terrain.TerrainColor.Length)
                        tex.SetPixel(x, h-1-y, new Color(result.Terrain.TerrainColor[ci]/255f, result.Terrain.TerrainColor[ci+1]/255f, result.Terrain.TerrainColor[ci+2]/255f));
                }

            // Draw rooms
            foreach (var node in result.Layout.Nodes)
            {
                Color roomColor = node.Purpose == "boss_lair" ? Color.red : node.Purpose == "treasure" ? Color.yellow : new Color(0.3f, 0.6f, 0.3f, 0.5f);
                for (int y = node.Y; y < node.Y + node.H && y < h; y++)
                    for (int x = node.X; x < node.X + node.W && x < w; x++)
                        tex.SetPixel(x, h-1-y, Color.Lerp(tex.GetPixel(x, h-1-y), roomColor, 0.4f));
            }

            // Draw spawns
            var ps = result.Population.PlayerSpawn;
            if (ps.X > 0) for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) { int px = ps.X+dx, py = ps.Y+dy; if (px>=0&&px<w&&py>=0&&py<h) tex.SetPixel(px, h-1-py, Color.cyan); }

            foreach (var e in result.Population.Encounters)
                if (e.X>=0&&e.X<w&&e.Y>=0&&e.Y<h) tex.SetPixel(e.X, h-1-e.Y, Color.red);

            tex.Apply();
            return tex;
        }
    }
}
