using System;
using UnityEngine;

namespace ForeverEngine.Demo.Battle
{
    /// <summary>
    /// Procedural transform-based animations for spawned combat models.
    /// No Animator controllers required — pure localPosition/rotation manipulation.
    /// Animations: idle bob, attack lunge, hit recoil, death topple.
    /// </summary>
    public class ModelAnimator : UnityEngine.MonoBehaviour
    {
        // === State ===
        private enum AnimState { Idle, Attack, Hit, Death }
        private AnimState _state = AnimState.Idle;

        private Vector3 _basePosition;
        private Quaternion _baseRotation;
        private bool _baseSet;
        private bool _deathComplete;

        // Attack state
        private Vector3 _attackTargetPos;
        private Action _attackOnComplete;
        private float _attackTimer;
        private const float ATTACK_DURATION = 0.3f;
        private const float ATTACK_LUNGE_DIST = 0.4f;

        // Hit state
        private float _hitTimer;
        private const float HIT_DURATION = 0.2f;
        private const float HIT_RECOIL_DIST = 0.3f;

        // Death state
        private float _deathTimer;
        private const float DEATH_DURATION = 0.6f;
        private const float DEATH_SINK = 0.3f;

        // === Public API ===

        /// <summary>Set the neutral resting position (call once after spawn).</summary>
        public void SetBasePosition(Vector3 worldPos)
        {
            _basePosition = worldPos;
            _baseRotation = transform.rotation;
            _baseSet = true;
        }

        /// <summary>
        /// Lunge toward targetPos over 0.3s then snap back.
        /// onComplete fires when the lunge snaps back.
        /// </summary>
        public void PlayAttack(Vector3 targetPos, Action onComplete = null)
        {
            if (_state == AnimState.Death) return;
            _attackTargetPos = targetPos;
            _attackOnComplete = onComplete;
            _attackTimer = 0f;
            _state = AnimState.Attack;
        }

        /// <summary>Recoil backward 0.3 units over 0.2s then return to base.</summary>
        public void PlayHit()
        {
            if (_state == AnimState.Death) return;
            // Allow hit to interrupt attack (e.g. counter-strike)
            _hitTimer = 0f;
            _state = AnimState.Hit;
        }

        /// <summary>Topple 90° around X axis and sink 0.3 units over 0.6s.</summary>
        public void PlayDeath()
        {
            if (_state == AnimState.Death) return;
            _deathTimer = 0f;
            _deathComplete = false;
            _state = AnimState.Death;
        }

        // === MonoBehaviour ===

        private void Start()
        {
            if (!_baseSet)
                SetBasePosition(transform.position);
        }

        private void Update()
        {
            if (!_baseSet) return;

            switch (_state)
            {
                case AnimState.Idle:
                    UpdateIdle();
                    break;
                case AnimState.Attack:
                    UpdateAttack();
                    break;
                case AnimState.Hit:
                    UpdateHit();
                    break;
                case AnimState.Death:
                    UpdateDeath();
                    break;
            }
        }

        // === Animation updaters ===

        private void UpdateIdle()
        {
            // Gentle vertical bob: sin(t*2) * 0.05
            float bob = Mathf.Sin(Time.time * 2f) * 0.05f;
            transform.position = _basePosition + Vector3.up * bob;
        }

        private void UpdateAttack()
        {
            _attackTimer += Time.deltaTime;
            float half = ATTACK_DURATION * 0.5f;

            // Direction toward target (flat, ignore Y)
            Vector3 toTarget = (_attackTargetPos - _basePosition);
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f)
                toTarget = toTarget.normalized * ATTACK_LUNGE_DIST;

            if (_attackTimer <= half)
            {
                // Lunge forward
                float t = _attackTimer / half;
                transform.position = _basePosition + toTarget * t;
            }
            else if (_attackTimer <= ATTACK_DURATION)
            {
                // Snap back
                float t = (_attackTimer - half) / half;
                transform.position = Vector3.Lerp(_basePosition + toTarget, _basePosition, t);
            }
            else
            {
                // Done
                transform.position = _basePosition;
                _state = AnimState.Idle;
                var cb = _attackOnComplete;
                _attackOnComplete = null;
                cb?.Invoke();
            }
        }

        private void UpdateHit()
        {
            _hitTimer += Time.deltaTime;
            float half = HIT_DURATION * 0.5f;

            // Recoil backward (away from attacker — use -forward as a simple proxy)
            Vector3 recoilDir = -transform.forward;
            recoilDir.y = 0f;
            if (recoilDir.sqrMagnitude > 0.0001f)
                recoilDir = recoilDir.normalized * HIT_RECOIL_DIST;

            if (_hitTimer <= half)
            {
                float t = _hitTimer / half;
                transform.position = _basePosition + recoilDir * t;
            }
            else if (_hitTimer <= HIT_DURATION)
            {
                float t = (_hitTimer - half) / half;
                transform.position = Vector3.Lerp(_basePosition + recoilDir, _basePosition, t);
            }
            else
            {
                transform.position = _basePosition;
                _state = AnimState.Idle;
            }
        }

        private void UpdateDeath()
        {
            if (_deathComplete) return;

            _deathTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_deathTimer / DEATH_DURATION);

            // Topple: rotate 90° around X (fall forward)
            Quaternion targetRot = _baseRotation * Quaternion.Euler(90f, 0f, 0f);
            transform.rotation = Quaternion.Slerp(_baseRotation, targetRot, t);

            // Sink downward 0.3 units
            transform.position = _basePosition + Vector3.down * (DEATH_SINK * t);

            if (t >= 1f)
                _deathComplete = true;
        }
    }
}
