using ForeverEngine.AI.Learning;
using UnityEngine;

namespace ForeverEngine.Demo.Battle
{
    public class CombatBrain
    {
        // State Space B: 3×3×3×3×2×2×2×2 = 1296 states, 7 actions
        // Original (A): 3×3×3×3×2 = 162 states, 5 actions
        //
        // New state factors:
        //   has_ranged_attack (2): can this enemy attack from range?
        //   ally_hp_critical  (2): is any ally below 25% HP?
        //   terrain_advantage (2): is this enemy on favorable terrain (e.g. guarding a chokepoint)?
        //
        // New actions:
        //   UseAbility: ranged attack from distance (only valid if has_ranged_attack)
        //   ProtectAlly: move to shield a low-HP ally (only valid if ally_hp_critical)

        public const int StateSize = 1296; // 3*3*3*3*2*2*2*2
        public const int ActionSize = 7;

        public enum Action { Advance, Retreat, Flank, Attack, Hold, UseAbility, ProtectAlly }

        private QLearner _learner;
        private int _lastState = -1;
        private int _lastAction = -1;
        private float _pendingReward;

        public CombatBrain(float[] savedQTable = null, int seed = 42)
        {
            var config = Resources.Load<GameConfig>("GameConfig");
            float lr = config != null ? config.QLearningRate : 0.12f;
            float df = config != null ? config.QDiscountFactor : 0.9f;
            float er = config != null ? config.QExplorationRate : 0.15f;
            _learner = new QLearner(StateSize, ActionSize, learningRate: lr,
                discountFactor: df, explorationRate: er, seed: seed);
            if (savedQTable != null) _learner.LoadTable(savedQTable);
        }

        public int EncodeState(BattleCombatant self, BattleCombatant player,
            int aliveAllies, string behavior,
            bool hasRanged = false, bool allyHpCritical = false, bool terrainAdvantage = false)
        {
            int dist = System.Math.Abs(self.X - player.X) + System.Math.Abs(self.Y - player.Y);
            int distBin = dist <= 1 ? 0 : dist <= 3 ? 1 : 2;

            float selfHpRatio = self.MaxHP > 0 ? (float)self.HP / self.MaxHP : 0f;
            int selfHpBin = selfHpRatio < 0.25f ? 0 : selfHpRatio < 0.6f ? 1 : 2;

            float playerHpRatio = player.MaxHP > 0 ? (float)player.HP / player.MaxHP : 0f;
            int playerHpBin = playerHpRatio < 0.25f ? 0 : playerHpRatio < 0.6f ? 1 : 2;

            int allyBin = aliveAllies <= 0 ? 0 : aliveAllies <= 2 ? 1 : 2;
            int behaviorBin = behavior == "guard" ? 1 : 0;
            int rangedBin = hasRanged ? 1 : 0;
            int allyCritBin = allyHpCritical ? 1 : 0;
            int terrainBin = terrainAdvantage ? 1 : 0;

            // Mixed-radix encoding: 3×3×3×3×2×2×2×2
            return distBin
                + 3 * (selfHpBin
                + 3 * (playerHpBin
                + 3 * (allyBin
                + 3 * (behaviorBin
                + 2 * (rangedBin
                + 2 * (allyCritBin
                + 2 * terrainBin))))));
        }

        public Action Decide(BattleCombatant self, BattleCombatant player,
            int aliveAllies, string behavior,
            bool hasRanged = false, bool allyHpCritical = false, bool terrainAdvantage = false)
        {
            int state = EncodeState(self, player, aliveAllies, behavior,
                hasRanged, allyHpCritical, terrainAdvantage);
            if (_lastState >= 0)
                _learner.Update(_lastState, _lastAction, _pendingReward, state);

            _pendingReward = 0f;
            _lastState = state;
            _lastAction = _learner.ChooseAction(state);

            // Action validity masking: if the chosen action isn't valid for this
            // enemy's capabilities, fall back to the best valid action.
            var action = (Action)_lastAction;
            if (action == Action.UseAbility && !hasRanged)
                action = Action.Attack; // fallback to melee
            if (action == Action.ProtectAlly && !allyHpCritical)
                action = Action.Hold; // nothing to protect

            return action;
        }

        public void AddReward(float reward) => _pendingReward += reward;

        public void OnEpisodeEnd(float finalReward)
        {
            if (_lastState >= 0)
            {
                _learner.Update(_lastState, _lastAction, finalReward + _pendingReward, _lastState);
                _lastState = -1;
                _pendingReward = 0f;
            }
        }

        public float[] SaveQTable() => _learner.SaveTable();

        public void SetExploration(float rate) => _learner.SetExplorationRate(rate);
    }
}
