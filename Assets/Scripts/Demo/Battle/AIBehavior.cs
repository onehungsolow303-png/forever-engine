namespace ForeverEngine.Demo.Battle
{
    public static class AIBehavior
    {
        public enum Action { Advance, Attack, Retreat, Flank, Hold, RangedAttack, ProtectAlly }

        public static Action Decide(BattleCombatant self, BattleCombatant player,
            int aliveAllies, string behavior)
        {
            int dist = System.Math.Abs(self.X - player.X) + System.Math.Abs(self.Y - player.Y);
            float selfHpRatio = self.MaxHP > 0 ? (float)self.HP / self.MaxHP : 0f;
            float playerHpRatio = player.MaxHP > 0 ? (float)player.HP / player.MaxHP : 0f;
            bool adjacent = dist <= 1;
            bool hasRanged = self.HasRangedAttack && self.AttackRange > 0;
            bool inRange = hasRanged && dist <= self.AttackRange;

            return (behavior ?? "chase").ToLowerInvariant() switch
            {
                "chase" or "aggressive" => DecideAggressive(adjacent, dist, selfHpRatio, hasRanged, inRange),
                "guard" => DecideGuard(adjacent, dist, selfHpRatio, hasRanged, inRange),
                "ranged" => DecideRanged(adjacent, dist, selfHpRatio, hasRanged, inRange),
                "coward" => DecideCoward(adjacent, dist, selfHpRatio, playerHpRatio, hasRanged, inRange),
                _ => DecideAggressive(adjacent, dist, selfHpRatio, hasRanged, inRange)
            };
        }

        // AGGRESSIVE: Close distance and attack. Only retreat if nearly dead.
        private static Action DecideAggressive(bool adjacent, int dist, float selfHp, bool hasRanged, bool inRange)
        {
            if (selfHp < 0.15f) return Action.Retreat;
            if (adjacent) return Action.Attack;
            if (hasRanged && inRange) return Action.RangedAttack;
            return Action.Advance;
        }

        // GUARD: Hold position, attack if player approaches within 3 cells.
        private static Action DecideGuard(bool adjacent, int dist, float selfHp, bool hasRanged, bool inRange)
        {
            if (selfHp < 0.15f) return Action.Retreat;
            if (adjacent) return Action.Attack;
            if (hasRanged && inRange) return Action.RangedAttack;
            if (dist <= 3) return Action.Advance;
            return Action.Hold;
        }

        // RANGED: Maintain distance 3-5, shoot. Back up if too close.
        private static Action DecideRanged(bool adjacent, int dist, float selfHp, bool hasRanged, bool inRange)
        {
            if (selfHp < 0.15f) return Action.Retreat;
            if (adjacent) return Action.Retreat;
            if (hasRanged && inRange && dist >= 3) return Action.RangedAttack;
            if (hasRanged && dist < 3) return Action.Retreat;
            if (hasRanged && !inRange) return Action.Advance;
            if (adjacent) return Action.Attack;
            return Action.Advance;
        }

        // COWARD: Attack when winning, flee when losing.
        private static Action DecideCoward(bool adjacent, int dist, float selfHp, float playerHp, bool hasRanged, bool inRange)
        {
            if (selfHp < 0.3f) return Action.Retreat;
            if (playerHp < 0.4f)
            {
                if (adjacent) return Action.Attack;
                return Action.Advance;
            }
            if (adjacent) return Action.Attack;
            if (hasRanged && inRange) return Action.RangedAttack;
            return Action.Hold;
        }

        // Auto-detect behavior: ranged enemies get "ranged" profile
        public static string ResolveBehavior(BattleCombatant c)
        {
            if (c.HasRangedAttack && c.AttackRange > 0 && c.Behavior != "guard")
                return "ranged";
            return c.Behavior ?? "chase";
        }
    }
}
