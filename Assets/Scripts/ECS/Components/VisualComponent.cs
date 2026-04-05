using Unity.Entities;
using Unity.Collections;

namespace ForeverEngine.ECS.Components
{
    /// <summary>
    /// Links an ECS entity to its visual representation.
    /// The rendering MonoBehaviour layer reads this to update sprites/GameObjects.
    /// </summary>
    public struct VisualComponent : IComponentData
    {
        public FixedString64Bytes SpriteId;   // References asset_manifest.json
        public FixedString32Bytes Variant;     // e.g., "torch", "chest_gold"
        public float Scale;
        public byte TintR;
        public byte TintG;
        public byte TintB;
        public bool Dirty;  // Set true when visual needs re-sync to GameObject
    }
}
