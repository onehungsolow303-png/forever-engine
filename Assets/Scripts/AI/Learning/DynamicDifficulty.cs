using UnityEngine;

namespace ForeverEngine.AI.Learning
{
    [CreateAssetMenu(menuName = "Forever Engine/Difficulty Config")]
    public class DifficultyConfig : ScriptableObject
    {
        public float minEnemyHealth = 0.5f, maxEnemyHealth = 2.0f;
        public float minEnemyDamage = 0.5f, maxEnemyDamage = 2.0f;
        public float minLootQuality = 0.5f, maxLootQuality = 2.0f;
        public float adjustSpeed = 0.1f;
    }

    public class DynamicDifficulty : UnityEngine.MonoBehaviour
    {
        public static DynamicDifficulty Instance { get; private set; }
        [SerializeField] private DifficultyConfig _config;

        public float EnemyHealthMult { get; private set; } = 1f;
        public float EnemyDamageMult { get; private set; } = 1f;
        public float LootQualityMult { get; private set; } = 1f;
        public float SpawnRateMult { get; private set; } = 1f;
        public float CurrentLevel { get; private set; } = 0.5f;

        private int _deaths, _kills;
        private float _sessionTime, _lastDeathTime;

        private void Awake() => Instance = this;

        public void RecordDeath() { _deaths++; _lastDeathTime = Time.time; }
        public void RecordKill() => _kills++;

        private void Update()
        {
            _sessionTime += Time.deltaTime;
            if (_sessionTime < 60f) return;

            float deathRate = _deaths / (_sessionTime / 3600f);
            float killRatio = _kills > 0 ? (float)_kills / Mathf.Max(1, _deaths) : 1f;

            float target = Mathf.Clamp01(killRatio / 5f);
            CurrentLevel = Mathf.Lerp(CurrentLevel, target, (_config?.adjustSpeed ?? 0.1f) * Time.deltaTime);

            if (_config != null)
            {
                EnemyHealthMult = Mathf.Lerp(_config.minEnemyHealth, _config.maxEnemyHealth, CurrentLevel);
                EnemyDamageMult = Mathf.Lerp(_config.minEnemyDamage, _config.maxEnemyDamage, CurrentLevel);
                LootQualityMult = Mathf.Lerp(_config.maxLootQuality, _config.minLootQuality, CurrentLevel);
            }
        }
    }
}
