using UnityEngine;

namespace ForeverEngine.Demo.Battle
{
    public class DamagePopup : MonoBehaviour
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

    public class HitFlash : MonoBehaviour
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
    }
}
