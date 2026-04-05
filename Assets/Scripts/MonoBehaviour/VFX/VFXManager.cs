using UnityEngine;
using System.Collections.Generic;

namespace ForeverEngine.MonoBehaviour.VFX
{
    public class VFXManager : UnityEngine.MonoBehaviour
    {
        public static VFXManager Instance { get; private set; }
        [SerializeField] private VFXConfig _config;
        private Dictionary<GameObject, Queue<GameObject>> _pools = new();

        private void Awake() => Instance = this;

        public void SpawnAt(GameObject prefab, Vector3 position, float duration = 2f)
        {
            if (prefab == null) return;
            var instance = GetFromPool(prefab);
            instance.transform.position = position;
            instance.SetActive(true);
            StartCoroutine(ReturnAfter(prefab, instance, duration));
        }

        public void PlayHit(Vector3 pos) => SpawnAt(_config?.HitEffect, pos);
        public void PlayCritHit(Vector3 pos) => SpawnAt(_config?.CriticalHitEffect, pos);
        public void PlayMiss(Vector3 pos) => SpawnAt(_config?.MissEffect, pos, 1f);
        public void PlayDeath(Vector3 pos) => SpawnAt(_config?.DeathEffect, pos, 3f);

        public void ShowDamageNumber(Vector3 pos, int damage, bool critical = false)
        {
            if (_config?.DamageNumberPrefab == null) return;
            var instance = GetFromPool(_config.DamageNumberPrefab);
            instance.transform.position = pos + Vector3.up * 0.5f;
            instance.SetActive(true);
            var label = instance.GetComponentInChildren<TextMesh>();
            if (label != null) { label.text = damage.ToString(); label.color = critical ? Color.yellow : Color.white; }
            StartCoroutine(FloatAndReturn(_config.DamageNumberPrefab, instance));
        }

        private GameObject GetFromPool(GameObject prefab)
        {
            if (!_pools.TryGetValue(prefab, out var pool)) { pool = new Queue<GameObject>(); _pools[prefab] = pool; }
            if (pool.Count > 0) { var p = pool.Dequeue(); if (p != null) return p; }
            return Instantiate(prefab, transform);
        }

        private void ReturnToPool(GameObject prefab, GameObject instance) { instance.SetActive(false); if (_pools.TryGetValue(prefab, out var pool)) pool.Enqueue(instance); }

        private System.Collections.IEnumerator ReturnAfter(GameObject prefab, GameObject instance, float delay) { yield return new WaitForSeconds(delay); ReturnToPool(prefab, instance); }
        private System.Collections.IEnumerator FloatAndReturn(GameObject prefab, GameObject instance) { float t = 0f; Vector3 s = instance.transform.position; while (t < 1.5f) { t += Time.deltaTime; instance.transform.position = s + Vector3.up * t * 0.5f; yield return null; } ReturnToPool(prefab, instance); }
    }
}
