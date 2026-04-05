using UnityEngine;

namespace ForeverEngine.MonoBehaviour.Audio
{
    [CreateAssetMenu(fileName = "AudioConfig", menuName = "Forever Engine/Audio Config")]
    public class AudioConfig : ScriptableObject
    {
        [Header("Volume")] [Range(0,1)] public float MasterVolume = 1f; [Range(0,1)] public float SFXVolume = 0.8f; [Range(0,1)] public float MusicVolume = 0.5f; [Range(0,1)] public float AmbientVolume = 0.3f;
        [Header("Clips")] public AudioClip[] HitSounds; public AudioClip[] MissSounds; public AudioClip[] FootstepSounds; public AudioClip[] DeathSounds; public AudioClip[] UIClickSounds;
        public AudioClip DoorOpenSound; public AudioClip LevelUpSound; public AudioClip QuestCompleteSound;
        [Header("Music")] public AudioClip ExplorationMusic; public AudioClip CombatMusic; public AudioClip MenuMusic;
        [Header("Ambient")] public AudioClip DungeonAmbient; public AudioClip CaveAmbient; public AudioClip ForestAmbient;
    }
}
