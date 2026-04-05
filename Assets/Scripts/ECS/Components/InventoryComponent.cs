using Unity.Entities;

namespace ForeverEngine.ECS.Components
{
    public struct InventoryComponent : IComponentData
    {
        public int MaxSlots;
    }
}
