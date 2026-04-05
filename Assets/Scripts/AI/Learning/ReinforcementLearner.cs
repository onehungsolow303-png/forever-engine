namespace ForeverEngine.AI.Learning
{
    public class ReinforcementLearner
    {
        private QLearner _learner;
        private float _totalReward;
        private int _episodes;

        public ReinforcementLearner(int states, int actions, float lr = 0.1f, float gamma = 0.95f)
        {
            _learner = new QLearner(states, actions, lr, gamma);
        }

        public int Act(int state) => _learner.ChooseAction(state);

        public void Step(int state, int action, float reward, int nextState, bool done)
        {
            _learner.Update(state, action, reward, nextState);
            _totalReward += reward;
            if (done) { _episodes++; _totalReward = 0; }
        }

        public float AverageReward => _episodes > 0 ? _totalReward / _episodes : 0;
        public int Episodes => _episodes;

        public void DecayExploration(float minRate = 0.01f)
        {
            float rate = System.Math.Max(minRate, 1f / (_episodes + 1));
            _learner.SetExplorationRate(rate);
        }
    }
}
