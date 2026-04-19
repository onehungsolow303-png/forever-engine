using UnityEngine;
using Unity.Entities;
using ECSWorld = Unity.Entities.World;
using ForeverEngine.ECS.Components;
using ForeverEngine.Data;

namespace ForeverEngine.MonoBehaviour.RPG
{
    /// <summary>
    /// Monitors ExperienceSystem for level-up events and presents the level-up UI.
    /// Handles: hit point increase, new spell slot allocation, subclass choice (at defined levels).
    ///
    /// UI hookup: assign levelUpPanel and wire its buttons to the public methods below.
    /// The panel should be hidden by default and shown via ShowLevelUpPanel().
    /// </summary>
    public class LevelUpManager : UnityEngine.MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Root panel to enable when a level-up is available.")]
        [SerializeField] private GameObject levelUpPanel;

        // ── Events ────────────────────────────────────────────────────────
        public event System.Action<int> OnLevelUpApplied; // arg: new level

        private int _lastKnownLevel = 1;
        private Entity _playerEntity;
        private bool _pendingLevelUp = false;

        // ── Unity lifecycle ────────────────────────────────────────────────

        private void Update()
        {
            CheckForLevelUp();
        }

        // ── Public API (called by UI buttons) ─────────────────────────────

        /// <summary>
        /// Applies the level-up for the player entity.
        /// Called when the player confirms their choices in the level-up panel.
        /// </summary>
        public void ApplyLevelUp()
        {
            if (!_pendingLevelUp) return;

            var world = ECSWorld.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var em = world.EntityManager;
            if (!em.Exists(_playerEntity)) return;

            var sheet = em.GetComponentData<CharacterSheetComponent>(_playerEntity);
            var stats = em.GetComponentData<StatsComponent>(_playerEntity);

            int newLevel = sheet.Level; // ExperienceSystem already incremented Level

            // ── HP increase: average hit die + CON mod ─────────────────
            int hitDieSides  = InfinityRPGData.HitDieValue(sheet.HitDieType.ToString());
            int avgRoll      = (hitDieSides / 2) + 1; // average rounded up
            int conMod       = InfinityRPGData.AbilityModifier(stats.Constitution);
            int hpGain       = Mathf.Max(1, avgRoll + conMod);

            stats.MaxHP  += hpGain;
            stats.HP     += hpGain;

            // ── Update spell slots for full casters ────────────────────
            UpdateSpellSlots(ref sheet, newLevel);

            // ── Hit dice ───────────────────────────────────────────────
            sheet.HitDiceTotal = newLevel;

            em.SetComponentData(_playerEntity, sheet);
            em.SetComponentData(_playerEntity, stats);

            _pendingLevelUp = false;
            HideLevelUpPanel();

            OnLevelUpApplied?.Invoke(newLevel);
            Debug.Log($"[LevelUpManager] Level up applied: level {newLevel}, +{hpGain} HP.");
        }

        /// <summary>
        /// Shows the level-up selection panel.
        /// Call from UI when the player is ready to choose their upgrades.
        /// </summary>
        public void ShowLevelUpPanel()
        {
            if (levelUpPanel != null) levelUpPanel.SetActive(true);
        }

        /// <summary>Hides the level-up panel.</summary>
        public void HideLevelUpPanel()
        {
            if (levelUpPanel != null) levelUpPanel.SetActive(false);
        }

        // ── Internal ──────────────────────────────────────────────────────

        private void CheckForLevelUp()
        {
            var world = ECSWorld.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var em = world.EntityManager;
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<CharacterSheetComponent>(),
                ComponentType.ReadOnly<PlayerTag>());

            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (entities.Length == 0) return;

            _playerEntity = entities[0];
            var sheet = em.GetComponentData<CharacterSheetComponent>(_playerEntity);

            if (sheet.Level > _lastKnownLevel)
            {
                _lastKnownLevel   = sheet.Level;
                _pendingLevelUp   = true;
                ShowLevelUpPanel();
            }
        }

        private static void UpdateSpellSlots(ref CharacterSheetComponent sheet, int newLevel)
        {
            // TODO: Determine if this character is a full/half/third caster.
            // For Plan 4, we default to full caster slot progression.
            // Plan 5 will read ClassName to select the correct table.
            sheet.SpellSlot1Max = InfinityRPGData.GetFullCasterSlots(newLevel, 1);
            sheet.SpellSlot2Max = InfinityRPGData.GetFullCasterSlots(newLevel, 2);
            sheet.SpellSlot3Max = InfinityRPGData.GetFullCasterSlots(newLevel, 3);
            sheet.SpellSlot4Max = InfinityRPGData.GetFullCasterSlots(newLevel, 4);
            sheet.SpellSlot5Max = InfinityRPGData.GetFullCasterSlots(newLevel, 5);
            sheet.SpellSlot6Max = InfinityRPGData.GetFullCasterSlots(newLevel, 6);
            sheet.SpellSlot7Max = InfinityRPGData.GetFullCasterSlots(newLevel, 7);
            sheet.SpellSlot8Max = InfinityRPGData.GetFullCasterSlots(newLevel, 8);
            sheet.SpellSlot9Max = InfinityRPGData.GetFullCasterSlots(newLevel, 9);
        }
    }
}
