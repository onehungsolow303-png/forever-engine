using UnityEngine;

namespace ForeverEngine.Genres.FPS
{
    public enum HitZone { Head, Body, Limb }

    public class HitDetection : MonoBehaviour
    {
        [SerializeField] private float _headMultiplier = 2.0f;
        [SerializeField] private float _bodyMultiplier = 1.0f;
        [SerializeField] private float _limbMultiplier = 0.7f;

        public float CalculateDamage(float baseDamage, HitZone zone) => CalculateDamage(baseDamage, zone, _headMultiplier, _bodyMultiplier, _limbMultiplier);

        public static float CalculateDamage(float baseDamage, HitZone zone, float headMult = 2f, float bodyMult = 1f, float limbMult = 0.7f)
        {
            return zone switch { HitZone.Head => baseDamage * headMult, HitZone.Body => baseDamage * bodyMult, HitZone.Limb => baseDamage * limbMult, _ => baseDamage };
        }

        public static HitZone DetermineZone(Vector3 hitPoint, Transform target)
        {
            float relativeHeight = (hitPoint.y - target.position.y) / (target.localScale.y * 2f);
            if (relativeHeight > 0.75f) return HitZone.Head;
            if (relativeHeight > 0.3f) return HitZone.Body;
            return HitZone.Limb;
        }
    }
}
