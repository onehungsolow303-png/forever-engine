using UnityEngine;
using ForeverEngine.AI.Inference;

namespace ForeverEngine.Demo.Battle
{
    public class CombatIntelligence : IntelligentBehavior
    {
        private CombatBrain _fallbackBrain;
        private BattleCombatant _self;
        private BattleCombatant _player;
        private int _aliveAllies;
        private string _behavior;
        private CombatBrain.Action _lastDecision;

        public CombatBrain.Action LastDecision => _lastDecision;
        public bool UsingNeural { get; private set; }

        public void Configure(BattleCombatant self, CombatBrain fallback)
        {
            _self = self;
            _fallbackBrain = fallback;
        }

        public void SetBattleContext(BattleCombatant player, int aliveAllies, string behavior)
        {
            _player = player;
            _aliveAllies = aliveAllies;
            _behavior = behavior;
        }

        public CombatBrain.Action DecideAction()
        {
            if (inferenceEngine != null && inferenceEngine.IsAvailable)
            {
                float[] input = GetModelInput();
                float[] output = inferenceEngine.Infer(input);
                ApplyModelOutput(output);
                UsingNeural = true;
            }
            else
            {
                FallbackBehavior();
                UsingNeural = false;
            }
            return _lastDecision;
        }

        protected override float[] GetModelInput()
        {
            if (_self == null || _player == null) return new float[8];

            int dist = System.Math.Abs(_self.X - _player.X) + System.Math.Abs(_self.Y - _player.Y);
            return new float[]
            {
                dist / 10f,                                                  // normalized distance
                (float)_self.HP / System.Math.Max(_self.MaxHP, 1),           // self HP ratio
                (float)_player.HP / System.Math.Max(_player.MaxHP, 1),       // player HP ratio
                _aliveAllies / 5f,                                           // normalized ally count
                _behavior == "guard" ? 1f : 0f,                              // behavior flag
                0f,                                                          // round (placeholder)
                _self.MovementRemaining > 0 ? 1f : 0f,                       // can move
                _self.HasAction ? 1f : 0f                                    // can act
            };
        }

        protected override void ApplyModelOutput(float[] output)
        {
            if (output == null || output.Length < CombatBrain.ActionSize)
            {
                FallbackBehavior();
                return;
            }

            int best = 0;
            for (int i = 1; i < CombatBrain.ActionSize; i++)
                if (output[i] > output[best]) best = i;

            _lastDecision = (CombatBrain.Action)best;
        }

        protected override void FallbackBehavior()
        {
            if (_fallbackBrain != null && _self != null && _player != null)
                _lastDecision = _fallbackBrain.Decide(_self, _player, _aliveAllies, _behavior);
            else
                _lastDecision = CombatBrain.Action.Advance;
        }
    }
}
