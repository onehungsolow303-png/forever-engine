using UnityEngine;
using UnityEngine.UIElements;
using ForeverEngine.ECS.Systems;
using ForeverEngine.ECS.Components;
using Unity.Entities;

namespace ForeverEngine.MonoBehaviour.UI
{
    public class InventoryUI : UnityEngine.MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;
        private VisualElement _inventoryPanel;
        private VisualElement _slotGrid;
        private Label _goldLabel;
        private bool _visible;

        private void OnEnable()
        {
            if (_uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;
            _inventoryPanel = root.Q<VisualElement>("inventory-panel");
            _slotGrid = root.Q<VisualElement>("inventory-grid");
            _goldLabel = root.Q<Label>("gold-label");
        }

        public void Toggle()
        {
            _visible = !_visible;
            if (_inventoryPanel != null) _inventoryPanel.style.display = _visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_visible) Refresh();
        }

        public void Refresh()
        {
            if (_slotGrid == null) return;
            _slotGrid.Clear();
            var invSys = InventorySystem.Instance;
            if (invSys == null) return;
            var em = World.DefaultGameObjectInjectionWorld?.EntityManager;
            if (em == null) return;
            var query = em.Value.CreateEntityQuery(typeof(PlayerTag), typeof(InventoryComponent));
            if (query.CalculateEntityCount() == 0) return;
            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            var inv = invSys.GetInventory(entities[0]);
            entities.Dispose();
            if (inv == null) return;
            if (_goldLabel != null) _goldLabel.text = $"Gold: {inv.Gold}";
            foreach (var slot in inv.GetAllSlots())
            {
                var el = new VisualElement();
                el.style.width = 48; el.style.height = 48;
                el.style.borderBottomWidth = el.style.borderTopWidth = el.style.borderLeftWidth = el.style.borderRightWidth = 1;
                var borderColor = slot.Equipped ? new Color(1f, 0.84f, 0f) : new Color(0.4f, 0.4f, 0.4f);
                el.style.borderBottomColor = el.style.borderTopColor = el.style.borderLeftColor = el.style.borderRightColor = borderColor;
                el.style.backgroundColor = new Color(0.15f, 0.15f, 0.2f);
                el.Add(new Label($"#{slot.ItemId}\nx{slot.StackCount}") { style = { fontSize = 10, color = Color.white } });
                _slotGrid.Add(el);
            }
        }
    }
}
