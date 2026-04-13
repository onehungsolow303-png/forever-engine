using ForeverEngine.AI.Learning;
using UnityEngine;

namespace ForeverEngine.Demo.Battle
{
    public class CombatBrain
    {
        public const int StateSize = 162; // 3*3*3*3*2
        public const int ActionSize = 5;

        public enum Action { Advance, Retreat, Flank, Attack, Hold }

        private QLearner _learner;
        private int _lastState = -1;
        private int _lastAction = -1;
        private float _pendingReward;

        public CombatBrain(float[] savedQTable = null, int seed = 42)
        {
            var config = Resources.Load<GameConfig>("GameConfig");
            float lr = config != null ? config.QLearningRate : 0.15f;
            float df = config != null ? config.QDiscountFactor : 0.85f;
            float er = config != null ? config.QExplorationRate : 0.25f;
            _learner = new QLearner(StateSize, ActionSize, learningRate: lr,
                discountFactor: df, explorationRate: er, seed: seed);
            if (savedQTable != null) _learner.LoadTable(savedQTable);
        }

        public int EncodeState(BattleCombatant self, BattleCombatant player,
            int aliveAllies, string behavior)
        {
            int dist = System.Math.Abs(self.X - player.X) + System.Math.Abs(self.Y - player.Y);
            int distBin = dist <= 1 ? 0 : dist <= 3 ? 1 : 2;

            float selfHpRatio = self.MaxHP > 0 ? (float)self.HP / self.MaxHP : 0f;
            int selfHpBin = selfHpRatio < 0.25f ? 0 : selfHpRatio < 0.6f ? 1 : 2;

            float playerHpRatio = player.MaxHP > 0 ? (float)player.HP / player.MaxHP : 0f;
            int playerHpBin = playerHpRatio < 0.25f ? 0 : playerHpRatio < 0.6f ? 1 : 2;

            int allyBin = aliveAllies <= 0 ? 0 : aliveAllies <= 2 ? 1 : 2;
            int behaviorBin = behavior == "guard" ? 1 : 0;

            return distBin + 3 * (selfHpBin + 3 * (playerHpBin + 3 * (allyBin + 3 * behaviorBin)));
        }

        public Action Decide(BattleCombatant self, BattleCombatant player,
            int aliveAllies, string behavior)
        {
            int state = EncodeState(self, player, aliveAllies, behavior);
            if (_lastState >= 0)
                _learner.Update(_lastState, _lastAction, _pendingReward, state);

            _pendingReward = 0f;
            _lastState = state;
            _lastAction = _learner.ChooseAction(state);
            return (Action)_lastAction;
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
