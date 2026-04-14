using ForeverEngine.MonoBehaviour.VFX;
using UnityEngine;

namespace ForeverEngine.Demo.Battle
{
    public class DamagePopup : UnityEngine.MonoBehaviour
    {
        private float _timer;
        private TextMesh _tm;

        private void Start() => _tm = GetComponent<TextMesh>();

        private void Update()
        {
            _timer += Time.deltaTime;
            transform.position += Vector3.up * Time.deltaTime * 1.5f;
            if (_tm != null)
            {
                Color c = _tm.color;
                c.a = Mathf.Lerp(1f, 0f, _timer);
                _tm.color = c;
            }
            var cam = Camera.main;
            if (cam != null) transform.forward = cam.transform.forward;
            if (_timer >= 1f) Destroy(gameObject);
        }
    }

    public class HitFlash : UnityEngine.MonoBehaviour
    {
        private float _timer = -1f;
        private Renderer _mr;
        private Color _originalColor;
        private static readonly Color FLASH_COLOR = Color.white;
        private const float FLASH_DURATION = 0.15f;

        public void Trigger()
        {
            if (_mr == null) _mr = GetComponentInChildren<Renderer>();
            if (_mr == null) return;
            _originalColor = _mr.material.color;
            _mr.material.color = FLASH_COLOR;
            _timer = FLASH_DURATION;
        }

        private void Update()
        {
            if (_timer < 0f) return;
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                if (_mr != null) _mr.material.color = _originalColor;
                _timer = -1f;
            }
        }
    }

    public static class BattleEffectsHelper
    {
        public static void ShowDamage(GameObject model, int amount, bool isCrit)
        {
            if (model == null) return;
            var go = new GameObject("DmgNum");
            go.transform.position = model.transform.position + Vector3.up * 1.5f;
            var tm = go.AddComponent<TextMesh>();
            tm.text = amount.ToString();
            tm.characterSize = 0.15f;
            tm.fontSize = 64;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;

            if (isCrit) tm.color = Color.yellow;
            else if (amount >= 15) tm.color = Color.red;
            else if (amount >= 8) tm.color = new Color(1f, 0.6f, 0f);
            else tm.color = Color.white;

            go.AddComponent<DamagePopup>();
        }

        public static void ShowHitFlash(GameObject model)
        {
            if (model == null) return;
            var flash = model.GetComponent<HitFlash>();
            if (flash == null) flash = model.AddComponent<HitFlash>();
            flash.Trigger();
        }

        public static void SpawnHitVFX(Vector3 position)
        {
            // Try VFXConfig prefab first
            var config = Resources.Load<VFXConfig>("VFXConfig");
            if (config != null && config.HitEffect != null)
            {
                var go = Object.Instantiate(config.HitEffect, position, Quaternion.identity);
                Object.Destroy(go, 2f);
                return;
            }

            // Procedural fallback: 8 white particles, 0.3s lifetime, sphere burst
            var ps = SpawnProceduralBurst(
                position,
                count: 8,
                color: Color.white,
                lifetime: 0.3f,
                speed: 3f,
                size: 0.08f);
            Object.Destroy(ps.gameObject, 0.6f);
        }

        public static void SpawnDeathVFX(Vector3 position)
        {
            // Try VFXConfig prefab first
            var config = Resources.Load<VFXConfig>("VFXConfig");
            if (config != null && config.DeathEffect != null)
            {
                var go = Object.Instantiate(config.DeathEffect, position, Quaternion.identity);
                Object.Destroy(go, 3f);
                return;
            }

            // Procedural fallback: 20 dark-red particles, 0.8s lifetime, sphere burst
            var ps = SpawnProceduralBurst(
                position,
                count: 20,
                color: new Color(0.55f, 0.05f, 0.05f),
                lifetime: 0.8f,
                speed: 4f,
                size: 0.12f);
            Object.Destroy(ps.gameObject, 1.5f);
        }

        private static ParticleSystem SpawnProceduralBurst(
            Vector3 position, int count, Color color, float lifetime, float speed, float size)
        {
            var go = new GameObject("BattleVFX");
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();

            // Stop auto-play so we can configure first
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.maxParticles = count + 4;
            main.loop = false;
            main.playOnAwake = false;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            var burst = new ParticleSystem.Burst(0f, (short)count);
            emission.SetBursts(new[] { burst });

            ps.Play();
            return ps;
        }
    }
}
