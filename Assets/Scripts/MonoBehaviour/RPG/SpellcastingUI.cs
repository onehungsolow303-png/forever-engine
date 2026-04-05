using UnityEngine;
using Unity.Entities;
using ForeverEngine.ECS.Components;
using ForeverEngine.Data;

namespace ForeverEngine.MonoBehaviour.RPG
{
    /// <summary>
    /// Manages the spell casting UI panel.
    /// Displays available spells and remaining spell slots.
    /// Routes cast requests by adding SpellCastRequestComponent to the player entity.
    ///
    /// UI hookup:
    ///   - Assign spellPanel (root GameObject).
    ///   - Wire SpellSlot buttons to CastSpell(spellId, slotLevel).
    ///   - Call RefreshUI() after combat state changes.
    /// </summary>
    public class SpellcastingUI : UnityEngine.MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject spellPanel;

        // ── Events ────────────────────────────────────────────────────────
        public event System.Action<string, int> OnSpellCast;   // spellId, slotLevel
        public event System.Action<string>      OnCastFailed;  // reason

        // ── Public API (called by UI buttons) ─────────────────────────────

        /// <summary>
        /// Submits a cast request for the given spell at the given slot level.
        /// SpellSystem will validate and resolve on the next ECS update.
        /// </summary>
        /// <param name="spellId">Spell id from SpellDatabase.</param>
        /// <param name="slotLevel">0 = cantrip, 1-9 = spell slot level to expend.</param>
        /// <param name="targetEntity">Target entity, or Entity.Null for self/AoE.</param>
        public void CastSpell(string spellId, int slotLevel, Entity targetEntity = default)
        {
            var playerEntity = GetPlayerEntity();
            if (playerEntity == Entity.Null)
            {
                OnCastFailed?.Invoke("no_player");
                return;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var em = world.EntityManager;

            // Validate slot available before even adding the request
            if (slotLevel > 0 && em.HasComponent<CharacterSheetComponent>(playerEntity))
            {
                var sheet = em.GetComponentData<CharacterSheetComponent>(playerEntity);
                if (!sheet.HasSlotAvailable(slotLevel))
                {
                    OnCastFailed?.Invoke("no_slot");
                    return;
                }
            }

            // Add request — SpellSystem picks it up next frame
            em.AddComponentData(playerEntity, new SpellCastRequestComponent
            {
                SpellId   = spellId,
                SlotLevel = slotLevel,
                Target    = targetEntity,
            });

            OnSpellCast?.Invoke(spellId, slotLevel);
            RefreshUI();
        }

        /// <summary>Shows the spell casting panel.</summary>
        public void ShowSpellPanel()
        {
            if (spellPanel != null) spellPanel.SetActive(true);
            RefreshUI();
        }

        /// <summary>Hides the spell casting panel.</summary>
        public void HideSpellPanel()
        {
            if (spellPanel != null) spellPanel.SetActive(false);
        }

        /// <summary>
        /// Refreshes all slot count labels and spell availability indicators.
        /// TODO: wire to actual UI Toolkit labels in Plan 5.
        /// </summary>
        public void RefreshUI()
        {
            var playerEntity = GetPlayerEntity();
            if (playerEntity == Entity.Null) return;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var em = world.EntityManager;
            if (!em.HasComponent<CharacterSheetComponent>(playerEntity)) return;

            var sheet = em.GetComponentData<CharacterSheetComponent>(playerEntity);

            // TODO: update UI Toolkit labels for each spell level (Plan 5)
            // Example pattern:
            //   slotLabel[i].text = $"{sheet.GetMaxSlots(i) - sheet.GetUsedSlots(i)} / {sheet.GetMaxSlots(i)}";
            Debug.Log($"[SpellcastingUI] Slot 1: {sheet.SpellSlot1Max - sheet.SpellSlot1Used}/{sheet.SpellSlot1Max} | " +
                      $"Slot 2: {sheet.SpellSlot2Max - sheet.SpellSlot2Used}/{sheet.SpellSlot2Max} | " +
                      $"Concentrating: {sheet.IsConcentrating}");
        }

        // ── Unity lifecycle ────────────────────────────────────────────────

        private void Update()
        {
            // Poll for SpellCastResultComponent and surface feedback
            CheckCastResult();
        }

        // ── Internal ──────────────────────────────────────────────────────

        private void CheckCastResult()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var em = world.EntityManager;
            var playerEntity = GetPlayerEntity();
            if (playerEntity == Entity.Null) return;

            if (!em.HasComponent<SpellCastResultComponent>(playerEntity)) return;

            var result = em.GetComponentData<SpellCastResultComponent>(playerEntity);
            em.RemoveComponent<SpellCastResultComponent>(playerEntity);

            if (!result.Success)
            {
                OnCastFailed?.Invoke(result.FailReason.ToString());
                Debug.LogWarning($"[SpellcastingUI] Cast failed: {result.FailReason}");
            }
            else
            {
                Debug.Log($"[SpellcastingUI] Cast '{result.SpellId}': {result.DamageDealt} dmg, {result.HealingDone} heal.");
                RefreshUI();
            }
        }

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
    }
}
