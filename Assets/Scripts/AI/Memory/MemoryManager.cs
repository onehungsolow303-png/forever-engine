using UnityEngine;

namespace ForeverEngine.AI.Memory
{
    public class MemoryManager : MonoBehaviour
    {
        public static MemoryManager Instance { get; private set; }

        public ShortTermMemory ShortTerm { get; private set; }
        public EpisodicMemory Episodic { get; private set; }
        public LongTermMemory LongTerm { get; private set; }
        [SerializeField] public SemanticMemory Semantic;

        [SerializeField] private float _shortTermDecay = 10f;
        [SerializeField] private int _maxEpisodes = 500;
        [SerializeField] private int _maxLongTermEvents = 10000;

        private string LTMPath => System.IO.Path.Combine(Application.persistentDataPath, "memory.json");

        private void Awake()
        {
            Instance = this;
            ShortTerm = new ShortTermMemory(_shortTermDecay);
            Episodic = new EpisodicMemory(_maxEpisodes);
            LongTerm = new LongTermMemory(_maxLongTermEvents);
            LongTerm.LoadFromFile(LTMPath);
        }

        private void Update() => ShortTerm.Decay();

        public void SaveLongTerm() => LongTerm.SaveToFile(LTMPath);

        private void OnApplicationQuit() => SaveLongTerm();
        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
