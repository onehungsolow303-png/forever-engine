using System.Collections.Generic;

namespace ForeverEngine.AI.Learning
{
    public class QLearner
    {
        private float[,] _qTable;
        private float _learningRate, _discountFactor, _explorationRate;
        private int _stateSize, _actionSize;
        private System.Random _rng;

        public QLearner(int stateSize, int actionSize, float learningRate = 0.1f, float discountFactor = 0.9f, float explorationRate = 0.2f, int seed = 42)
        {
            _stateSize = stateSize; _actionSize = actionSize;
            _learningRate = learningRate; _discountFactor = discountFactor; _explorationRate = explorationRate;
            _qTable = new float[stateSize, actionSize];
            _rng = new System.Random(seed);
        }

        public int ChooseAction(int state)
        {
            if (_rng.NextDouble() < _explorationRate)
                return _rng.Next(_actionSize);
            return GetBestAction(state);
        }

        public int GetBestAction(int state)
        {
            int best = 0; float bestVal = _qTable[state, 0];
            for (int a = 1; a < _actionSize; a++)
                if (_qTable[state, a] > bestVal) { bestVal = _qTable[state, a]; best = a; }
            return best;
        }

        public void Update(int state, int action, float reward, int nextState)
        {
            float maxNext = _qTable[nextState, 0];
            for (int a = 1; a < _actionSize; a++)
                if (_qTable[nextState, a] > maxNext) maxNext = _qTable[nextState, a];
            _qTable[state, action] += _learningRate * (reward + _discountFactor * maxNext - _qTable[state, action]);
        }

        public float GetQValue(int state, int action) => _qTable[state, action];

        public float[] SaveTable()
        {
            var flat = new float[_stateSize * _actionSize];
            for (int s = 0; s < _stateSize; s++)
                for (int a = 0; a < _actionSize; a++)
                    flat[s * _actionSize + a] = _qTable[s, a];
            return flat;
        }

        public void LoadTable(float[] flat)
        {
            if (flat == null || flat.Length != _stateSize * _actionSize) return;
            for (int s = 0; s < _stateSize; s++)
                for (int a = 0; a < _actionSize; a++)
                    _qTable[s, a] = flat[s * _actionSize + a];
        }

        public void SetExplorationRate(float rate) => _explorationRate = System.Math.Clamp(rate, 0f, 1f);
        public void SetLearningRate(float rate) => _learningRate = System.Math.Clamp(rate, 0f, 1f);
    }
}
