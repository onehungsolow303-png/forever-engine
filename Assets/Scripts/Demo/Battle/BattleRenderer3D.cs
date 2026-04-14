using UnityEngine;
using System.Collections.Generic;

namespace ForeverEngine.Demo.Battle
{
    /// <summary>
    /// DEPRECATED: Arena-based battle rendering replaced by seamless in-world BattleZone system.
    /// DamagePopup and HitFlash moved to BattleEffects.cs.
    /// Stub retained for backward compatibility with any remaining references.
    /// </summary>
    public class BattleRenderer3D : UnityEngine.MonoBehaviour
    {
        public void Initialize(BattleSceneTemplate template, BattleGrid grid,
            List<BattleCombatant> combatants, Camera cam) { }
        public void UpdateVisuals(List<BattleCombatant> combatants, BattleCombatant currentTurn) { }
        public void ShowDamage(BattleCombatant target, int amount, bool isCrit) { }
        public void ShowHitFlash(BattleCombatant target) { }
        public void ShowPathPreview(BattleGrid grid, BattleCombatant mover,
            int targetX, int targetY, List<BattleCombatant> combatants) { }
        public void ClearPathPreview() { }
        public Vector3 GridToWorld(int x, int y) => Vector3.zero;
        public (int x, int y) WorldToGrid(Vector3 pos) => (0, 0);
        public void Cleanup() { }
    }
}
