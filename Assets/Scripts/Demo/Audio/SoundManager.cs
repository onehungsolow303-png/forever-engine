using ForeverEngine.MonoBehaviour.Audio;
using UnityEngine;

namespace ForeverEngine.Demo.Audio
{
    /// <summary>
    /// Centralized sound effect manager. All game events call here.
    /// Clips are resolved from an <see cref="AudioConfig"/> SO:
    ///   1. Inspector-assigned _config field (preferred for scene prefabs).
    ///   2. Resources.Load fallback ("AudioConfig") populated by AudioPopulator.
    /// Falls back to a Debug.Log when no clip is found.
    /// </summary>
    public class SoundManager : UnityEngine.MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        [Header("Audio Config")]
        [SerializeField] private AudioConfig _config;

        [Header("Combat — override clips (optional)")]
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

            // Ensure config is loaded — fall back to Resources if not set in Inspector
            if (_config == null)
                _config = Resources.Load<AudioConfig>("AudioConfig");

            if (_config == null)
                Debug.LogWarning("[SoundManager] AudioConfig not found. Run 'Forever Engine → Populate Audio Config' in the Editor.");

            _source = GetComponent<AudioSource>();
            if (_source == null)
            {
                _source = gameObject.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.spatialBlend = 0f; // 2D sound
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────

        /// <summary>Returns a random clip from an array, or null if array is empty/null.</summary>
        private static AudioClip Random(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return null;
            return clips[UnityEngine.Random.Range(0, clips.Length)];
        }

        private void Play(AudioClip clip, string fallbackLog)
        {
            if (clip != null)
                _source.PlayOneShot(clip);
            else
                Debug.Log($"[SFX] {fallbackLog}");
        }

        // ── Combat ────────────────────────────────────────────────────────
        public void PlayHit()
        {
            // Inspector override first, then config array, then log
            var clip = hitSound ?? Random(_config?.HitSounds);
            Play(clip, "Hit");
        }

        public void PlayCrit()
        {
            // No dedicated crit array — reuse hit sounds at higher pitch
            var clip = critSound ?? Random(_config?.HitSounds);
            if (clip != null)
            {
                float savedPitch = _source.pitch;
                _source.pitch = 1.4f;
                _source.PlayOneShot(clip);
                _source.pitch = savedPitch;
            }
            else
            {
                Debug.Log("[SFX] Critical Hit!");
            }
        }

        public void PlayMiss()
        {
            var clip = missSound ?? Random(_config?.MissSounds);
            Play(clip, "Miss");
        }

        public void PlayDeath()
        {
            var clip = deathSound ?? Random(_config?.DeathSounds);
            Play(clip, "Death");
        }

        public void PlayVictory() => Play(victorySound, "Victory!");
        public void PlayDefeat()  => Play(defeatSound,  "Defeated...");

        // ── Spells ────────────────────────────────────────────────────────
        public void PlaySpellCast() => Play(spellCastSound, "Spell Cast");
        public void PlayHeal()      => Play(healSound,      "Heal");

        // ── Exploration ───────────────────────────────────────────────────
        public void PlayStairs() => Play(stairsSound, "Stairs transition");

        public void PlayDoor()
        {
            var clip = doorSound ?? _config?.DoorOpenSound;
            Play(clip, "Door");
        }

        public void PlayLoot() => Play(lootSound, "Loot found");

        // ── UI ────────────────────────────────────────────────────────────
        public void PlayButton()
        {
            var clip = buttonSound ?? Random(_config?.UIClickSounds);
            Play(clip, "Button click");
        }

        public void PlayMenuOpen()
        {
            var clip = menuOpenSound ?? Random(_config?.UIClickSounds);
            Play(clip, "Menu open");
        }
    }
}

