using UnityEngine;
using ForeverEngine.AI.Inference;

namespace ForeverEngine.Genres.FPS
{
    public enum CombatRole { Rusher, Sniper, Flanker, Support }

    public class FPSAICombatant : IntelligentBehavior
    {
        [SerializeField] private CombatRole _role;
        [SerializeField] private float _detectionRange = 30f;
        [SerializeField] private float _attackRange = 20f;
        [SerializeField] private float _health = 100f;

        private Transform _target;

        public CombatRole Role => _role;
        public float Health => _health;
        public bool IsAlive => _health > 0;

        public void TakeDamage(float amount) { _health -= amount; if (_health <= 0) OnDeath(); }

        protected override float[] GetModelInput()
        {
            if (_target == null) return new float[8];
            float dist = Vector3.Distance(transform.position, _target.position);
            return new float[] { dist / _detectionRange, _health / 100f, (float)_role / 3f, transform.position.x, transform.position.y, transform.position.z, _target.position.x, _target.position.z };
        }

        protected override void ApplyModelOutput(float[] output)
        {
            if (output == null || output.Length < 2) { FallbackBehavior(); return; }
            // output[0] = move direction, output[1] = should attack
        }

        protected override void FallbackBehavior()
        {
            if (_target == null) { Patrol(); return; }
            float dist = Vector3.Distance(transform.position, _target.position);

            switch (_role)
            {
                case CombatRole.Rusher: if (dist > 3f) MoveToward(_target.position); break;
                case CombatRole.Sniper: if (dist < 15f) MoveAway(_target.position); break;
                case CombatRole.Flanker: Flank(_target.position); break;
                case CombatRole.Support: if (dist > _attackRange) MoveToward(_target.position); break;
            }
        }

        private void Patrol() { } // Placeholder
        private void MoveToward(Vector3 pos) => transform.position = Vector3.MoveTowards(transform.position, pos, 3f * Time.deltaTime);
        private void MoveAway(Vector3 pos) => transform.position = Vector3.MoveTowards(transform.position, pos, -2f * Time.deltaTime);
        private void Flank(Vector3 pos) { var perp = Vector3.Cross(pos - transform.position, Vector3.up).normalized; transform.position += perp * 2f * Time.deltaTime; }
        private void OnDeath() => gameObject.SetActive(false);

        public void SetTarget(Transform t) => _target = t;
    }
}
