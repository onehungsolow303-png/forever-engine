using UnityEngine;

namespace ForeverEngine.Demo.Audio
{
    /// <summary>
    /// Centralized sound effect manager. All game events call here.
    /// Currently logs events — replace with AudioSource.PlayOneShot()
    /// once real audio clips are assigned.
    /// </summary>
    public class SoundManager : UnityEngine.MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        [Header("Combat")]
        [SerializeField] private AudioClip hitSound;
        [SerializeField] private AudioClip critSound;
        [SerializeField] private AudioClip missSound;
        [SerializeField] private AudioClip deathSound;
        [SerializeField] private AudioClip victorySound;
        [SerializeField] private AudioClip defeatSound;

        [Header("Spells")]
        [SerializeField] private AudioClip spellCastSound;
        [SerializeField] private AudioClip healSound;

        [Header("Exploration")]
        [SerializeField] private AudioClip stairsSound;
        [SerializeField] private AudioClip doorSound;
        [SerializeField] private AudioClip lootSound;

        [Header("UI")]
        [SerializeField] private AudioClip buttonSound;
        [SerializeField] private AudioClip menuOpenSound;

        private AudioSource _source;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _source = GetComponent<AudioSource>();
            if (_source == null)
            {
                _source = gameObject.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.spatialBlend = 0f; // 2D sound
            }
        }

        private void Play(AudioClip clip, string fallbackLog)
        {
            if (clip != null)
                _source.PlayOneShot(clip);
            else
                Debug.Log($"[SFX] {fallbackLog}");
        }

        // ── Combat ────────────────────────────────────────────────────────
        public void PlayHit() => Play(hitSound, "Hit");
        public void PlayCrit() => Play(critSound, "Critical Hit!");
        public void PlayMiss() => Play(missSound, "Miss");
        public void PlayDeath() => Play(deathSound, "Death");
        public void PlayVictory() => Play(victorySound, "Victory!");
        public void PlayDefeat() => Play(defeatSound, "Defeated...");

        // ── Spells ────────────────────────────────────────────────────────
        public void PlaySpellCast() => Play(spellCastSound, "Spell Cast");
        public void PlayHeal() => Play(healSound, "Heal");

        // ── Exploration ───────────────────────────────────────────────────
        public void PlayStairs() => Play(stairsSound, "Stairs transition");
        public void PlayDoor() => Play(doorSound, "Door");
        public void PlayLoot() => Play(lootSound, "Loot found");

        // ── UI ────────────────────────────────────────────────────────────
        public void PlayButton() => Play(buttonSound, "Button click");
        public void PlayMenuOpen() => Play(menuOpenSound, "Menu open");
    }
}
