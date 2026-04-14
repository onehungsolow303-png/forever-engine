using System.Collections.Generic;
using UnityEngine;

namespace ForeverEngine
{
    public enum PropCategory { Lighting, Furniture, Container, Debris, Decorative }

    [System.Serializable]
    public class PropEntry
    {
        public GameObject Prefab;
        public PropCategory Category;
        [Tooltip("Wall-mounted props snap to walls; floor props place on ground")]
        public bool WallMounted;
        [Tooltip("Vertical offset from placement surface")]
        public float YOffset;
    }

    [System.Serializable]
    public class RoomDecorPreset
    {
        public string Name;
        [Tooltip("Min room tier to use this preset (0 = any)")]
        public int MinTier;
        [Tooltip("Max room tier (0 = any)")]
        public int MaxTier;
        public bool ForCorridor;
        public bool ForBoss;
        [Range(0, 10)]
        public int LightingCount = 2;
        [Range(0, 6)]
        public int FurnitureCount = 1;
        [Range(0, 6)]
        public int ContainerCount = 1;
        [Range(0, 4)]
        public int DebrisCount;
        [Range(0, 4)]
        public int DecorativeCount = 1;
    }

    /// <summary>
    /// Catalog of decoration props from installed asset packs. Populated by
    /// the RoomCatalogPopulator editor script. Used at runtime by RoomDecorator
    /// to dress DA Snap rooms with visual variety.
    /// </summary>
    [CreateAssetMenu(fileName = "RoomCatalog", menuName = "Forever Engine/Room Catalog")]
    public class RoomCatalog : ScriptableObject
    {
        [Header("Props by Category")]
        public List<PropEntry> Props = new();

        [Header("Decoration Presets")]
        public List<RoomDecorPreset> Presets = new();

        public List<PropEntry> GetByCategory(PropCategory category)
        {
            var result = new List<PropEntry>();
            foreach (var p in Props)
                if (p.Prefab != null && p.Category == category)
                    result.Add(p);
            return result;
        }

        /// <summary>
        /// Find the best matching preset for a room, or null if none match.
        /// </summary>
        public RoomDecorPreset GetPreset(int tier, bool isCorridor, bool isBoss)
        {
            RoomDecorPreset best = null;
            int bestScore = -1;

            foreach (var preset in Presets)
            {
                int score = 0;

                if (isBoss && preset.ForBoss) score += 10;
                else if (isBoss && !preset.ForBoss) continue;

                if (isCorridor && preset.ForCorridor) score += 10;
                else if (isCorridor && !preset.ForCorridor) continue;
                else if (!isCorridor && preset.ForCorridor) continue;

                if (preset.MinTier > 0 && tier < preset.MinTier) continue;
                if (preset.MaxTier > 0 && tier > preset.MaxTier) continue;
                if (preset.MinTier > 0 && tier >= preset.MinTier) score += 5;

                if (score > bestScore) { bestScore = score; best = preset; }
            }

            return best;
        }
    }
}
