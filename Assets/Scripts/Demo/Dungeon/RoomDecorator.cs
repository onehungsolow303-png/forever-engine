using System.Collections.Generic;
using UnityEngine;

namespace ForeverEngine.Demo.Dungeon
{
    /// <summary>
    /// Places decoration props from <see cref="RoomCatalog"/> into DA Snap rooms
    /// after dungeon generation. Called by <see cref="DADungeonBuilder"/> post-build.
    /// </summary>
    public static class RoomDecorator
    {
        /// <summary>
        /// Decorate all rooms using the catalog. Deterministic per-room RNG seeded
        /// from dungeon seed + room index for reproducible results.
        /// </summary>
        public static void DecorateAll(DADungeonBuilder.RoomInfo[] rooms, RoomCatalog catalog, int dungeonSeed = 0)
        {
            if (catalog == null || catalog.Props.Count == 0)
            {
                Debug.LogWarning("[RoomDecorator] No catalog or props — skipping decoration");
                return;
            }

            int placed = 0;
            for (int i = 0; i < rooms.Length; i++)
            {
                placed += DecorateRoom(rooms[i], catalog, dungeonSeed ^ (i * 7919));
            }

            Debug.Log($"[RoomDecorator] Placed {placed} props across {rooms.Length} rooms");
        }

        private static int DecorateRoom(DADungeonBuilder.RoomInfo room, RoomCatalog catalog, int seed)
        {
            var preset = catalog.GetPreset(room.Tier, room.IsCorridor, room.IsBoss);
            if (preset == null) return 0;

            var rng = new System.Random(seed);
            int placed = 0;

            // Create a container for all decorations in this room
            var container = new GameObject($"Decor_{room.Index}");
            if (room.RoomObject != null)
                container.transform.SetParent(room.RoomObject.transform);
            container.transform.position = room.WorldBounds.center;

            placed += PlaceCategory(catalog, PropCategory.Lighting, preset.LightingCount,
                room, container.transform, rng);
            placed += PlaceCategory(catalog, PropCategory.Furniture, preset.FurnitureCount,
                room, container.transform, rng);
            placed += PlaceCategory(catalog, PropCategory.Container, preset.ContainerCount,
                room, container.transform, rng);
            placed += PlaceCategory(catalog, PropCategory.Debris, preset.DebrisCount,
                room, container.transform, rng);
            placed += PlaceCategory(catalog, PropCategory.Decorative, preset.DecorativeCount,
                room, container.transform, rng);

            return placed;
        }

        private static int PlaceCategory(RoomCatalog catalog, PropCategory category, int count,
            DADungeonBuilder.RoomInfo room, Transform parent, System.Random rng)
        {
            if (count <= 0) return 0;

            var props = catalog.GetByCategory(category);
            if (props.Count == 0) return 0;

            int placed = 0;
            var bounds = room.WorldBounds;

            for (int i = 0; i < count; i++)
            {
                var entry = props[rng.Next(props.Count)];

                Vector3 position;
                Quaternion rotation;

                if (entry.WallMounted)
                {
                    // Place along a wall edge
                    position = GetWallPosition(bounds, rng);
                    rotation = GetWallRotation(position, bounds.center);
                }
                else
                {
                    // Place on floor within room bounds (shrink by 20% to avoid walls)
                    position = GetFloorPosition(bounds, rng);
                    rotation = Quaternion.Euler(0, rng.Next(360), 0);
                }

                position.y = bounds.min.y + entry.YOffset;

                var go = Object.Instantiate(entry.Prefab, position, rotation, parent);
                go.name = $"{entry.Prefab.name}_{i}";

                // Ensure decorations don't block gameplay
                foreach (var col in go.GetComponentsInChildren<Collider>())
                {
                    if (!col.isTrigger)
                        col.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
                }

                placed++;
            }

            return placed;
        }

        private static Vector3 GetWallPosition(Bounds bounds, System.Random rng)
        {
            // Pick a random wall (N/S/E/W) and a random point along it
            float margin = 0.3f;
            int wall = rng.Next(4);
            float t = (float)rng.NextDouble();

            return wall switch
            {
                0 => new Vector3(
                    Mathf.Lerp(bounds.min.x + margin, bounds.max.x - margin, t),
                    0,
                    bounds.max.z - margin), // North
                1 => new Vector3(
                    Mathf.Lerp(bounds.min.x + margin, bounds.max.x - margin, t),
                    0,
                    bounds.min.z + margin), // South
                2 => new Vector3(
                    bounds.max.x - margin,
                    0,
                    Mathf.Lerp(bounds.min.z + margin, bounds.max.z - margin, t)), // East
                _ => new Vector3(
                    bounds.min.x + margin,
                    0,
                    Mathf.Lerp(bounds.min.z + margin, bounds.max.z - margin, t)), // West
            };
        }

        private static Quaternion GetWallRotation(Vector3 position, Vector3 roomCenter)
        {
            // Face inward toward room center
            Vector3 dir = roomCenter - position;
            dir.y = 0;
            if (dir.sqrMagnitude < 0.01f) return Quaternion.identity;
            return Quaternion.LookRotation(dir);
        }

        private static Vector3 GetFloorPosition(Bounds bounds, System.Random rng)
        {
            // Shrink placement area to 60% of room to avoid walls and center (where player spawns)
            float shrink = 0.2f;
            float xRange = bounds.size.x * (1 - shrink * 2);
            float zRange = bounds.size.z * (1 - shrink * 2);

            float x = bounds.min.x + bounds.size.x * shrink + (float)rng.NextDouble() * xRange;
            float z = bounds.min.z + bounds.size.z * shrink + (float)rng.NextDouble() * zRange;

            return new Vector3(x, 0, z);
        }
    }
}
