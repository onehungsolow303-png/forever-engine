using UnityEngine;

namespace ForeverEngine.AssetGeneration
{
    public static class ProceduralSpriteGenerator
    {
        public static Texture2D GenerateCreatureToken(Color baseColor, int size = 32)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            float center = size / 2f, radius = size / 2f - 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dist = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                    if (dist < radius - 1) tex.SetPixel(x, y, baseColor);
                    else if (dist < radius) tex.SetPixel(x, y, Color.Lerp(baseColor, Color.black, 0.5f)); // Border
                    else tex.SetPixel(x, y, Color.clear);
                }
            tex.Apply();
            return tex;
        }

        public static Texture2D GenerateItemIcon(Color color, string shape = "square", int size = 16)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            float center = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool inside = shape switch
                    {
                        "circle" => Mathf.Sqrt((x-center)*(x-center)+(y-center)*(y-center)) < center - 1,
                        "diamond" => Mathf.Abs(x-center) + Mathf.Abs(y-center) < center - 1,
                        _ => x > 1 && x < size-2 && y > 1 && y < size-2
                    };
                    tex.SetPixel(x, y, inside ? color : Color.clear);
                }
            tex.Apply();
            return tex;
        }

        public static Sprite TextureToSprite(Texture2D tex)
        {
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
        }
    }
}
