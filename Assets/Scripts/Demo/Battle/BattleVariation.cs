using UnityEngine;

namespace ForeverEngine.Demo.Battle
{
    public static class BattleVariation
    {
        public static void Apply(BattleSceneTemplate template, GameObject room,
            BattleGrid grid, int seed)
        {
            if (template.IsBossArena) return;

            var rng = new System.Random(seed);

            // Room rotation (0, 90, 180, 270)
            int rotIndex = rng.Next(4);
            room.transform.rotation = Quaternion.Euler(0f, rotIndex * 90f, 0f);

            // Lighting variation
            var lights = room.GetComponentsInChildren<Light>();
            float hueShift = (float)(rng.NextDouble() * 0.1 - 0.05);
            float intensityMult = 0.8f + (float)(rng.NextDouble() * 0.4);
            foreach (var light in lights)
            {
                Color.RGBToHSV(light.color, out float h, out float s, out float v);
                light.color = Color.HSVToRGB(Mathf.Repeat(h + hueShift, 1f), s, v);
                light.intensity *= intensityMult;
            }

            // Scatter obstacle props within walkable area
            if (template.ObstacleProps != null && template.ObstacleProps.Length > 0)
            {
                int propCount = 2 + rng.Next(5);
                int attempts = 0;
                int placed = 0;

                while (placed < propCount && attempts < 50)
                {
                    attempts++;
                    int x = 2 + rng.Next(grid.Width - 4);
                    int y = 2 + rng.Next(grid.Height - 4);

                    if (!grid.IsWalkable(x, y)) continue;

                    bool inSpawn = false;
                    foreach (var sp in template.PlayerSpawnZone)
                        if (sp.x == x && sp.y == y) { inSpawn = true; break; }
                    if (!inSpawn)
                        foreach (var sp in template.EnemySpawnZone)
                            if (sp.x == x && sp.y == y) { inSpawn = true; break; }
                    if (inSpawn) continue;

                    var prefab = template.ObstacleProps[rng.Next(template.ObstacleProps.Length)];
                    var prop = Object.Instantiate(prefab, room.transform);
                    prop.transform.position = new Vector3(x + 0.5f, 0f, y + 0.5f);
                    prop.transform.rotation = Quaternion.Euler(0f, rng.Next(360), 0f);

                    grid.Walkable[y * grid.Width + x] = false;
                    placed++;
                }
            }
        }
    }
}
