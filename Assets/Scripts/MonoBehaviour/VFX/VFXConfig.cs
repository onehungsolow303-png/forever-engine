using UnityEngine;

namespace ForeverEngine.MonoBehaviour.VFX
{
    [CreateAssetMenu(fileName = "VFXConfig", menuName = "Forever Engine/VFX Config")]
    public class VFXConfig : ScriptableObject
    {
        [Header("Combat")] public GameObject HitEffect; public GameObject CriticalHitEffect; public GameObject MissEffect; public GameObject DeathEffect; public GameObject HealEffect;
        [Header("Environment")] public GameObject TorchFlame; public GameObject MagicGlow; public GameObject DustCloud; public GameObject WaterSplash;
        [Header("UI")] public GameObject DamageNumberPrefab; public GameObject LevelUpEffect;
    }
}
