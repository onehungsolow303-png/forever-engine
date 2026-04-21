using UnityEngine;

namespace ForeverEngine.Procedural
{
    /// <summary>
    /// Authoring-side description of a hero-bake zone. Bounds declared in
    /// world-space meters; resolution typically 1m. At bake time, HeroBakeTool
    /// loads the Terrain covering these bounds and samples it at Resolution.
    /// </summary>
    [CreateAssetMenu(fileName = "HeroZone", menuName = "ForeverEngine/Baked Hero Zone")]
    public class BakedHeroZoneAsset : ScriptableObject
    {
        public string ZoneId;
        public byte LayerId;
        public float WorldMinX, WorldMinZ;
        public float WorldMaxX, WorldMaxZ;
        public float ResolutionMeters = 1f;
    }
}
