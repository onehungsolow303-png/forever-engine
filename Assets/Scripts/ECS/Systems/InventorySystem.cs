using Unity.Entities;
using System.Collections.Generic;
using ForeverEngine.ECS.Components;
using ForeverEngine.ECS.Data;

namespace ForeverEngine.ECS.Systems
{
    public partial class InventorySystem : SystemBase
    {
        private Dictionary<Entity, Inventory> _inventories = new();
        public static InventorySystem Instance { get; private set; }

        public Inventory GetInventory(Entity entity)
        {
            if (_inventories.TryGetValue(entity, out var inv)) return inv;
            if (EntityManager.HasComponent<InventoryComponent>(entity))
            {
                var comp = EntityManager.GetComponentData<InventoryComponent>(entity);
                inv = new Inventory(comp.MaxSlots);
                _inventories[entity] = inv;
                return inv;
            }
            return null;
        }

        protected override void OnCreate() => Instance = this;
        protected override void OnUpdate() { }
        protected override void OnDestroy() { _inventories.Clear(); if (Instance == this) Instance = null; }
    }
}
