using UnityEngine;
using Unity.Entities;
using ForeverEngine.ECS.Components;
using ForeverEngine.ECS.Systems;

namespace ForeverEngine.MonoBehaviour.RPG
{
    /// <summary>
    /// Manages the rest UI and submits RestRequestComponent to the player entity.
    /// Delegates all mechanical resolution to RestSystem (ECS).
    ///
    /// UI hookup:
    ///   - Assign restPanel (root GameObject).
    ///   - Wire hitDiceSpinner to SetHitDiceToSpend(n).
    ///   - Wire "Short Rest" button to RequestShortRest().
    ///   - Wire "Long Rest" button to RequestLongRest().
    ///   - Wire "Cancel" button to Close().
    /// </summary>
    public class RestManager : UnityEngine.MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject restPanel;

        // ── Events ────────────────────────────────────────────────────────
        public event System.Action OnShortRestBegin;
        public event System.Action OnLongRestBegin;
        public event System.Action OnRestComplete;

        // ── Internal state ────────────────────────────────────────────────
        private int _hitDiceToSpend = 1;

        // ── Public API (called by UI buttons) ─────────────────────────────

        /// <summary>Sets the number of hit dice the player wants to spend during a short rest.</summary>
        public void SetHitDiceToSpend(int count) => _hitDiceToSpend = Mathf.Max(0, count);

        /// <summary>
        /// Submits a short rest request to RestSystem.
        /// Player spends hit dice to regain HP.
        /// </summary>
        public void RequestShortRest()
        {
            var playerEntity = GetPlayerEntity();
            if (playerEntity == Entity.Null)
            {
                Debug.LogWarning("[RestManager] No player entity found.");
                return;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var em = world.EntityManager;

            // Guard: can only rest during exploration
            var gameState = SystemAPI_GetGameState(em);
            if (gameState != GameState.Exploration && gameState != GameState.Paused)
            {
                Debug.LogWarning("[RestManager] Cannot rest during combat.");
                return;
            }

            em.AddComponentData(playerEntity, new RestRequestComponent
            {
                Type            = RestType.Short,
                HitDiceToSpend  = _hitDiceToSpend,
            });

            OnShortRestBegin?.Invoke();
            Debug.Log($"[RestManager] Short rest requested: spend {_hitDiceToSpend} hit dice.");
            Close();

            // Poll for rest completion in next frame via Update
        }

        /// <summary>
        /// Submits a long rest request to RestSystem.
        /// Restores all HP, spell slots, and half of spent hit dice.
        /// </summary>
        public void RequestLongRest()
        {
            var playerEntity = GetPlayerEntity();
            if (playerEntity == Entity.Null)
            {
                Debug.LogWarning("[RestManager] No player entity found.");
                return;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var em = world.EntityManager;

            var gameState = SystemAPI_GetGameState(em);
            if (gameState != GameState.Exploration && gameState != GameState.Paused)
            {
                Debug.LogWarning("[RestManager] Cannot rest during combat.");
                return;
            }

            em.AddComponentData(playerEntity, new RestRequestComponent
            {
                Type           = RestType.Long,
                HitDiceToSpend = 0,
            });

            OnLongRestBegin?.Invoke();
            Debug.Log("[RestManager] Long rest requested.");
            Close();
        }

        /// <summary>Shows the rest selection panel.</summary>
        public void ShowRestPanel()
        {
            RefreshRestInfo();
            if (restPanel != null) restPanel.SetActive(true);
        }

        /// <summary>Closes the rest panel.</summary>
        public void Close()
        {
            if (restPanel != null) restPanel.SetActive(false);
        }

        /// <summary>
        /// Refreshes hit dice availability display in the rest panel.
        /// TODO: update UI Toolkit labels in Plan 5.
        /// </summary>
        public void RefreshRestInfo()
        {
            var playerEntity = GetPlayerEntity();
            if (playerEntity == Entity.Null) return;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var em = world.EntityManager;
            if (!em.HasComponent<CharacterSheetComponent>(playerEntity)) return;

            var sheet = em.GetComponentData<CharacterSheetComponent>(playerEntity);
            var stats = em.GetComponentData<StatsComponent>(playerEntity);

            int availableHitDice = sheet.HitDiceTotal - sheet.HitDiceUsed;
            // TODO: Update UI labels:
            //   hitDiceLabel.text  = $"{availableHitDice} / {sheet.HitDiceTotal} {sheet.HitDieType} available";
            //   hpLabel.text       = $"HP: {stats.HP} / {stats.MaxHP}";
            //   slot1Label.text    = $"Spell slots L1: {sheet.SpellSlot1Max - sheet.SpellSlot1Used}/{sheet.SpellSlot1Max}";
            Debug.Log($"[RestManager] HP: {stats.HP}/{stats.MaxHP} | " +
                      $"Hit dice: {availableHitDice}/{sheet.HitDiceTotal} {sheet.HitDieType} | " +
                      $"L1 slots: {sheet.SpellSlot1Max - sheet.SpellSlot1Used}/{sheet.SpellSlot1Max}");
        }

        // ── Internal ──────────────────────────────────────────────────────

        private static Entity GetPlayerEntity()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return Entity.Null;

            var em = world.EntityManager;
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<CharacterSheetComponent>());

            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            return entities.Length > 0 ? entities[0] : Entity.Null;
        }

        // Avoid SystemAPI dependency in MonoBehaviour by reading the singleton directly
        private static GameState SystemAPI_GetGameState(EntityManager em)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<GameStateSingleton>());
            if (query.IsEmpty) return GameState.Exploration;
            return query.GetSingleton<GameStateSingleton>().CurrentState;
        }
    }
}
