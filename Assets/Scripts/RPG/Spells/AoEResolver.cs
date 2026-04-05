using System.Collections.Generic;
using UnityEngine;
using ForeverEngine.RPG.Enums;

namespace ForeverEngine.RPG.Spells
{
    /// <summary>
    /// Static AoE resolver. Determines which grid positions fall within an area of effect.
    /// Uses tile-based (5ft = 1 tile) grid for all calculations.
    /// </summary>
    public static class AoEResolver
    {
        /// <summary>
        /// Get all grid positions within an area of effect.
        /// </summary>
        /// <param name="origin">Center/origin point of the AoE.</param>
        /// <param name="shape">Shape of the area.</param>
        /// <param name="sizeFeet">Size in feet (radius for sphere/cylinder, length for line/cone, side for cube).</param>
        /// <param name="direction">Direction vector (for cones and lines). Ignored for spheres/cubes.</param>
        /// <returns>List of affected grid positions.</returns>
        public static List<Vector2Int> GetAffectedPositions(
            Vector2Int origin,
            AoEShape shape,
            int sizeFeet,
            Vector2 direction = default)
        {
            var positions = new List<Vector2Int>();
            int sizeTiles = sizeFeet / 5; // Convert feet to tiles (5ft = 1 tile)

            switch (shape)
            {
                case AoEShape.Sphere:
                case AoEShape.Cylinder:
                    // Circle/cylinder: all tiles within radius
                    for (int x = -sizeTiles; x <= sizeTiles; x++)
                    {
                        for (int y = -sizeTiles; y <= sizeTiles; y++)
                        {
                            // Use Chebyshev distance for D&D grid (diagonal = 1 tile)
                            // or Euclidean for more precise circles
                            float dist = Mathf.Sqrt(x * x + y * y);
                            if (dist <= sizeTiles + 0.5f)
                            {
                                positions.Add(new Vector2Int(origin.x + x, origin.y + y));
                            }
                        }
                    }
                    break;

                case AoEShape.Cube:
                    // Cube: square area, sizeTiles on each side
                    int half = sizeTiles / 2;
                    for (int x = -half; x <= half; x++)
                    {
                        for (int y = -half; y <= half; y++)
                        {
                            positions.Add(new Vector2Int(origin.x + x, origin.y + y));
                        }
                    }
                    break;

                case AoEShape.Line:
                    // Line: extends from origin in direction for sizeTiles length, 1 tile wide
                    if (direction == default) direction = Vector2.up;
                    var dir = direction.normalized;
                    for (int i = 0; i <= sizeTiles; i++)
                    {
                        int px = origin.x + Mathf.RoundToInt(dir.x * i);
                        int py = origin.y + Mathf.RoundToInt(dir.y * i);
                        positions.Add(new Vector2Int(px, py));
                    }
                    break;

                case AoEShape.Cone:
                    // Cone: 53-degree spread (per D&D), extends sizeTiles from origin
                    if (direction == default) direction = Vector2.up;
                    var coneDir = direction.normalized;
                    float coneAngle = 53f * Mathf.Deg2Rad / 2f;
                    for (int x = -sizeTiles; x <= sizeTiles; x++)
                    {
                        for (int y = -sizeTiles; y <= sizeTiles; y++)
                        {
                            if (x == 0 && y == 0) continue;
                            float dist = Mathf.Sqrt(x * x + y * y);
                            if (dist > sizeTiles + 0.5f) continue;

                            var toTile = new Vector2(x, y).normalized;
                            float angle = Mathf.Acos(Vector2.Dot(coneDir, toTile));
                            if (angle <= coneAngle)
                            {
                                positions.Add(new Vector2Int(origin.x + x, origin.y + y));
                            }
                        }
                    }
                    // Always include origin
                    positions.Add(origin);
                    break;

                case AoEShape.None:
                default:
                    // Single target (just the origin)
                    positions.Add(origin);
                    break;
            }

            return positions;
        }

        /// <summary>
        /// Check if a specific position is within the AoE.
        /// </summary>
        public static bool IsInArea(
            Vector2Int origin,
            AoEShape shape,
            int sizeFeet,
            Vector2Int target,
            Vector2 direction = default)
        {
            var affected = GetAffectedPositions(origin, shape, sizeFeet, direction);
            return affected.Contains(target);
        }
    }
}
