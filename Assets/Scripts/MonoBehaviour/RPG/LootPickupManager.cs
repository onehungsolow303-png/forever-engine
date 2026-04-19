using UnityEngine;
using Unity.Entities;
using ECSWorld = Unity.Entities.World;
using ForeverEngine.ECS.Components;

namespace ForeverEngine.MonoBehaviour.RPG
{
    /// <summary>
    /// Handles the player interacting with loot containers (chests, corpses, crates).
    /// Reads LootTableComponent from the target entity, checks lock state,
    /// and adds items/gold to the player's Inventory.
    ///
    /// UI hookup:
    ///   - Assign lootPanel (root GameObject).
    ///   - The panel lists item names and a "Take All" button.
    ///   - Call OpenLootContainer(entity) when the player activates a container.
    /// </summary>
    public class LootPickupManager : UnityEngine.MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject lootPanel;

        // ── Events ────────────────────────────────────────────────────────
        public event System.Action<LootTableComponent> OnLootOpened;
        public event System.Action<int, string[]>      OnLootTaken;   // gold, itemIds
        public event System.Action<int>                OnLockFailed;  // roll result

        // ── Internal state ────────────────────────────────────────────────
        private Entity _currentLootEntity = Entity.Null;

        // ── Public API (called by interaction system / UI buttons) ─────────

        /// <summary>
        /// Opens the loot UI for the given container entity.
        /// If locked, the player must pass a Thieves' Tools check first.
        /// </summary>
        public void OpenLootContainer(Entity containerEntity)
        {
            var world = ECSWorld.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var em = world.EntityManager;
            if (!em.Exists(containerEntity)) return;
            if (!em.HasComponent<LootTableComponent>(containerEntity)) return;

            var loot = em.GetComponentData<LootTableComponent>(containerEntity);

            if (loot.Looted)
            {
                Debug.Log("[LootPickupManager] Container already looted.");
                return;
            }

            if (loot.Locked)
            {
                // TODO: Check if player has thieves' tools and proficiency.
                // For now, auto-fail locked containers (Plan 5 implements skill checks).
                OnLockFailed?.Invoke(0);
                Debug.Log($"[LootPickupManager] Container is locked (DC {loot.LockDC}). Thieves' Tools check needed.");
                return;
            }

            _currentLootEntity = containerEntity;
            OnLootOpened?.Invoke(loot);
            ShowLootPanel(loot);
        }

        /// <summary>
        /// Takes all loot from the currently open container and closes the panel.
        /// Adds gold and items to the player's inventory.
        /// </summary>
        public void TakeAll()
        {
            if (_currentLootEntity == Entity.Null) return;

            var world = ECSWorld.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var em = world.EntityManager;
            if (!em.Exists(_currentLootEntity)) return;

            var loot = em.GetComponentData<LootTableComponent>(_currentLootEntity);
            if (loot.Looted) return;

            // Mark as looted
            loot.Looted = true;
            em.SetComponentData(_currentLootEntity, loot);

            // Parse item ids
            string itemIdsRaw = loot.ItemIds.ToString();
            string[] itemIds = string.IsNullOrEmpty(itemIdsRaw)
                ? System.Array.Empty<string>()
                : itemIdsRaw.Split(',');

            // TODO: Add items to player InventoryComponent (Plan 5 wires up item entities).
            // For now: log the reward.
            Debug.Log($"[LootPickupManager] Looted: {loot.GoldValue} gold, items: [{string.Join(", ", itemIds)}]");

            OnLootTaken?.Invoke(loot.GoldValue, itemIds);
            CloseLootPanel();
        }

        /// <summary>Closes the loot panel without taking anything.</summary>
        public void Close()
        {
            _currentLootEntity = Entity.Null;
            CloseLootPanel();
        }

        /// <summary>Picks the lock on the current container using a Thieves' Tools check.</summary>
        public void AttemptPickLock(int thievesToolsBonus)
        {
            if (_currentLootEntity == Entity.Null) return;

            var world = ECSWorld.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var em = world.EntityManager;
            if (!em.HasComponent<LootTableComponent>(_currentLootEntity)) return;

            var loot = em.GetComponentData<LootTableComponent>(_currentLootEntity);
            if (!loot.Locked) return;

            // TODO: Use DiceRoller for proper seeded roll when seeded RNG is accessible here.
            int roll = UnityEngine.Random.Range(1, 21) + thievesToolsBonus;

            if (roll >= loot.LockDC)
            {
                loot.Locked = false;
                em.SetComponentData(_currentLootEntity, loot);
                Debug.Log($"[LootPickupManager] Lock picked (roll {roll} vs DC {loot.LockDC}).");
                ShowLootPanel(loot);
            }
            else
            {
                Debug.Log($"[LootPickupManager] Lock pick failed (roll {roll} vs DC {loot.LockDC}).");
                OnLockFailed?.Invoke(roll);
            }
        }

        // ── Internal ──────────────────────────────────────────────────────

        private void ShowLootPanel(LootTableComponent loot)
        {
            if (lootPanel != null) lootPanel.SetActive(true);

            // TODO: Populate UI Toolkit loot list with item names from ItemDatabase (Plan 5).
            Debug.Log($"[LootPickupManager] Panel open — treasure '{loot.TreasureId}', " +
                      $"{loot.GoldValue} gp, items: {loot.ItemIds}");
        }

        private void CloseLootPanel()
        {
            if (lootPanel != null) lootPanel.SetActive(false);
        }
    }
}
