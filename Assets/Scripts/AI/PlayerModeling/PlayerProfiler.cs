using UnityEngine;

namespace ForeverEngine.AI.PlayerModeling
{
    public class PlayerProfiler : MonoBehaviour
    {
        public static PlayerProfiler Instance { get; private set; }
        public static PlayerProfile CurrentProfile => Instance?._profile;

        [SerializeField] private float _updateInterval = 30f;
        private PlayerProfile _profile = new();
        private float _timer;

        private int _meleeAttacks, _rangedAttacks, _combatTime, _exploreTime;
        private int _itemsCollected, _dialoguesStarted;

        private void Awake() => Instance = this;

        public void RecordMeleeAttack() => _meleeAttacks++;
        public void RecordRangedAttack() => _rangedAttacks++;
        public void RecordCombatTime(float seconds) => _combatTime += (int)seconds;
        public void RecordExploreTime(float seconds) => _exploreTime += (int)seconds;
        public void RecordItemCollected() => _itemsCollected++;
        public void RecordDialogue() => _dialoguesStarted++;

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < _updateInterval) return;
            _timer = 0f;
            UpdateProfile();
        }

        private void UpdateProfile()
        {
            int totalAttacks = _meleeAttacks + _rangedAttacks;
            if (totalAttacks > 0)
                _profile.Playstyle.MeleeVsRanged = Mathf.Lerp(_profile.Playstyle.MeleeVsRanged, (float)_meleeAttacks / totalAttacks, 0.3f);

            int totalTime = _combatTime + _exploreTime;
            if (totalTime > 0)
            {
                _profile.Playstyle.CombatPreference = Mathf.Lerp(_profile.Playstyle.CombatPreference, (float)_combatTime / totalTime, 0.3f);
                _profile.Playstyle.ExplorationPreference = Mathf.Lerp(_profile.Playstyle.ExplorationPreference, (float)_exploreTime / totalTime, 0.3f);
            }

            _profile.ArchetypeTags.Clear();
            _profile.ArchetypeTags.Add(_profile.GetPrimaryArchetype());
        }

        public void ResetProfile() => _profile = new PlayerProfile();
    }
}
