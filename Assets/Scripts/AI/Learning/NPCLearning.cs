using UnityEngine;

namespace ForeverEngine.AI.Learning
{
    [CreateAssetMenu(menuName = "Forever Engine/NPC Archetype")]
    public class NPCArchetype : ScriptableObject
    {
        public string archetypeId;
        public int stateFeatures = 16;
        public int possibleActions = 8;
        public float baseLearningRate = 0.1f;
    }

    public class NPCLearning : UnityEngine.MonoBehaviour
    {
        [SerializeField] private NPCArchetype _archetype;
        private QLearner _learner;
        private int _lastState, _lastAction;

        private void Start()
        {
            float lr = _archetype != null ? _archetype.baseLearningRate : 0.1f;
            int states = _archetype != null ? _archetype.stateFeatures : 16;
            int actions = _archetype != null ? _archetype.possibleActions : 8;
            _learner = new QLearner(states, actions, lr);
        }

        public int DecideAction(int currentState)
        {
            _lastState = currentState;
            _lastAction = _learner.ChooseAction(currentState);
            return _lastAction;
        }

        public void RewardAction(float reward, int newState)
        {
            _learner.Update(_lastState, _lastAction, reward, newState);
        }

        public void SetDifficulty(float difficulty)
        {
            _learner.SetLearningRate(_archetype.baseLearningRate * difficulty);
            _learner.SetExplorationRate(0.3f * (1f - difficulty));
        }
    }
}
