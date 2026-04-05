using UnityEngine;

namespace ForeverEngine.MonoBehaviour.Audio
{
    public class AudioManager : UnityEngine.MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }
        [SerializeField] private AudioConfig _config;
        [SerializeField] private AudioSource _sfxSource;
        [SerializeField] private AudioSource _musicSource;
        [SerializeField] private AudioSource _ambientSource;

        private void Awake()
        {
            Instance = this;
            if (_sfxSource == null) { _sfxSource = gameObject.AddComponent<AudioSource>(); _sfxSource.playOnAwake = false; }
            if (_musicSource == null) { _musicSource = gameObject.AddComponent<AudioSource>(); _musicSource.loop = true; _musicSource.playOnAwake = false; }
            if (_ambientSource == null) { _ambientSource = gameObject.AddComponent<AudioSource>(); _ambientSource.loop = true; _ambientSource.playOnAwake = false; }
        }

        public void PlaySFX(AudioClip clip) { if (clip == null || _config == null) return; _sfxSource.volume = _config.MasterVolume * _config.SFXVolume; _sfxSource.PlayOneShot(clip); }
        public void PlayRandomSFX(AudioClip[] clips) { if (clips == null || clips.Length == 0) return; PlaySFX(clips[Random.Range(0, clips.Length)]); }
        public void PlayHit() => PlayRandomSFX(_config?.HitSounds);
        public void PlayMiss() => PlayRandomSFX(_config?.MissSounds);
        public void PlayFootstep() => PlayRandomSFX(_config?.FootstepSounds);
        public void PlayDeath() => PlayRandomSFX(_config?.DeathSounds);

        public void PlayMusic(AudioClip clip) { if (_musicSource == null || _config == null) return; if (_musicSource.clip == clip && _musicSource.isPlaying) return; _musicSource.clip = clip; _musicSource.volume = _config.MasterVolume * _config.MusicVolume; _musicSource.Play(); }
        public void PlayExplorationMusic() => PlayMusic(_config?.ExplorationMusic);
        public void PlayCombatMusic() => PlayMusic(_config?.CombatMusic);
        public void PlayAmbient(AudioClip clip) { if (_ambientSource == null || _config == null) return; _ambientSource.clip = clip; _ambientSource.volume = _config.MasterVolume * _config.AmbientVolume; _ambientSource.Play(); }
        public void StopMusic() => _musicSource?.Stop();
        public void StopAmbient() => _ambientSource?.Stop();
        public void SetMasterVolume(float vol) { if (_config != null) _config.MasterVolume = Mathf.Clamp01(vol); AudioListener.volume = vol; }
    }
}
