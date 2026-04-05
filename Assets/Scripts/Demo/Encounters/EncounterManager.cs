using UnityEngine;
using ForeverEngine.AI.Director;
using ForeverEngine.AI.Learning;

namespace ForeverEngine.Demo.Encounters
{
    public class EncounterManager : UnityEngine.MonoBehaviour
    {
        public static EncounterManager Instance { get; private set; }

        private void Awake() => Instance = this;

        public bool ShouldSuppressEncounter()
        {
            // AI Director pacing: suppress if too many recent fights
            var director = AIDirector.Instance;
            if (director != null && director.Pacing.CurrentIntensity > 0.7f)
                return true;

            var gm = GameManager.Instance;
            if (gm != null)
            {
                var overworldMgr = Overworld.OverworldManager.Instance;
                if (overworldMgr != null && overworldMgr.EncountersSinceRest >= 3)
                    return Random.Range(0f, 1f) < 0.5f; // 50% suppress after 3 fights
            }
            return false;
        }

        public EncounterData ScaleEncounter(EncounterData baseData)
        {
            var dda = DynamicDifficulty.Instance;
            if (dda == null) return baseData;

            foreach (var enemy in baseData.Enemies)
            {
                enemy.HP = Mathf.RoundToInt(enemy.HP * dda.EnemyHealthMult);
                enemy.HP = Mathf.Max(1, enemy.HP);
            }
            baseData.GoldReward = Mathf.RoundToInt(baseData.GoldReward * dda.LootQualityMult);
            return baseData;
        }

        public void StartEncounter(string encounterId)
        {
            if (ShouldSuppressEncounter() && !encounterId.Contains("boss"))
            {
                Debug.Log("[Encounters] Suppressed by AI Director pacing");
                return;
            }

            var data = EncounterData.Get(encounterId);
            data = ScaleEncounter(data);

            Debug.Log($"[Encounters] Starting: {encounterId} ({data.Enemies.Count} enemies)");
            GameManager.Instance.EnterBattle(encounterId);
        }
    }
}
