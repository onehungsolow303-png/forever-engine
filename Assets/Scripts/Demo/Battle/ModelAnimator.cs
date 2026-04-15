using System;
using UnityEngine;

namespace ForeverEngine.Demo.Battle
{
    /// <summary>
    /// Animations for spawned combat models.
    /// Automatically uses skeletal animations (Animator or legacy Animation component) when
    /// the model was imported from a GLB with embedded clips ("Idle", "Attack", "Hit", "Death").
    /// Falls back to procedural transform-based animations when no skeletal rig is present.
    /// </summary>
    public class ModelAnimator : UnityEngine.MonoBehaviour
    {
        // === Skeletal animation detection ===
        private Animator _skelAnimator;       // modern Mecanim Animator
        private Animation _legacyAnimation;   // legacy Animation component (common in GLB imports)
        private bool _hasSkeletal;

        // Clip names that must be present in the rig for skeletal mode to activate
        private const string CLIP_IDLE   = "Idle";
        private const string CLIP_ATTACK = "Attack";
        private const string CLIP_HIT    = "Hit";
        private const string CLIP_DEATH  = "Death";

        // Tracks whether the skeletal attack is waiting to fire onComplete
        private Action _skelAttackOnComplete;

        // === State ===
        private enum AnimState { Idle, Attack, Hit, Death }
        private AnimState _state = AnimState.Idle;

        private Vector3 _basePosition;
        /// <summary>Read the logical base position (without bob offset) for lerp calculations.</summary>
        public Vector3 BasePosition => _basePosition;
        private Quaternion _baseRotation;
        private bool _baseSet;
        private bool _deathComplete;

        // Attack state (procedural)
        private Vector3 _attackTargetPos;
        private Action _attackOnComplete;
        private float _attackTimer;
        private const float ATTACK_DURATION = 0.3f;
        private const float ATTACK_LUNGE_DIST = 0.4f;

        // Hit state (procedural)
        private float _hitTimer;
        private const float HIT_DURATION = 0.2f;
        private const float HIT_RECOIL_DIST = 0.3f;

        // Death state (procedural)
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
        /// Lunge toward targetPos over 0.3s then snap back (procedural), or play the
        /// skeletal "Attack" clip when available. onComplete fires when the animation ends.
        /// </summary>
        public void PlayAttack(Vector3 targetPos, Action onComplete = null)
        {
            if (_state == AnimState.Death) return;
            _state = AnimState.Attack;

            if (_hasSkeletal)
            {
                _skelAttackOnComplete = onComplete;
                PlaySkeletalClip(CLIP_ATTACK);
            }
            else
            {
                _attackTargetPos = targetPos;
                _attackOnComplete = onComplete;
                _attackTimer = 0f;
            }
        }

        /// <summary>Recoil backward 0.3 units over 0.2s then return to base (procedural),
        /// or play the skeletal "Hit" clip when available.</summary>
        public void PlayHit()
        {
            if (_state == AnimState.Death) return;
            _state = AnimState.Hit;

            if (_hasSkeletal)
            {
                PlaySkeletalClip(CLIP_HIT);
            }
            else
            {
                _hitTimer = 0f;
            }
        }

        /// <summary>Topple 90° around X axis and sink 0.3 units over 0.6s (procedural),
        /// or play the skeletal "Death" clip when available.</summary>
        public void PlayDeath()
        {
            if (_state == AnimState.Death) return;
            _state = AnimState.Death;

            if (_hasSkeletal)
            {
                PlaySkeletalClip(CLIP_DEATH);
                _deathComplete = true; // skeletal handles it; suppress procedural
            }
            else
            {
                _deathTimer = 0f;
                _deathComplete = false;
            }
        }

        // === MonoBehaviour ===

        private void Start()
        {
            if (!_baseSet)
                SetBasePosition(transform.position);

            DetectSkeletalAnimations();
        }

        private void Update()
        {
            if (!_baseSet) return;

            if (_hasSkeletal)
            {
                UpdateSkeletal();
            }
            else
            {
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
        }

        // === Skeletal animation helpers ===

        /// <summary>
        /// Detect whether this model has usable skeletal animation clips.
        /// Checks both the modern Animator (Mecanim) and the legacy Animation component,
        /// since Unity's GLB importer may produce either depending on import settings.
        /// Skeletal mode only activates when the expected clip names are present.
        /// </summary>
        private void DetectSkeletalAnimations()
        {
            // --- Try modern Animator (Mecanim) ---
            var anim = GetComponentInChildren<Animator>();
            if (anim != null && anim.runtimeAnimatorController != null)
            {
                // Verify the controller actually contains the clips we need
                var clips = anim.runtimeAnimatorController.animationClips;
                bool hasAttack = false, hasHit = false, hasDeath = false;
                foreach (var clip in clips)
                {
                    if (clip.name == CLIP_ATTACK) hasAttack = true;
                    else if (clip.name == CLIP_HIT)    hasHit    = true;
                    else if (clip.name == CLIP_DEATH)  hasDeath  = true;
                }
                if (hasAttack && hasHit && hasDeath)
                {
                    _skelAnimator = anim;
                    _hasSkeletal  = true;
                    return;
                }
            }

            // --- Try legacy Animation component ---
            var legAnim = GetComponentInChildren<Animation>();
            if (legAnim != null)
            {
                bool hasAttack = legAnim.GetClip(CLIP_ATTACK) != null;
                bool hasHit    = legAnim.GetClip(CLIP_HIT)    != null;
                bool hasDeath  = legAnim.GetClip(CLIP_DEATH)  != null;
                if (hasAttack && hasHit && hasDeath)
                {
                    _legacyAnimation = legAnim;
                    _hasSkeletal     = true;
                    // Ensure all clips are set to the right wrap mode
                    SetLegacyWrapMode(CLIP_ATTACK, WrapMode.Once);
                    SetLegacyWrapMode(CLIP_HIT,    WrapMode.Once);
                    SetLegacyWrapMode(CLIP_DEATH,  WrapMode.Once);
                    SetLegacyWrapMode(CLIP_IDLE,   WrapMode.Loop);
                    return;
                }
            }

            // No usable skeletal rig found — stay in procedural mode
        }

        private void SetLegacyWrapMode(string clipName, WrapMode mode)
        {
            var clip = _legacyAnimation.GetClip(clipName);
            if (clip != null) clip.wrapMode = mode;
        }

        /// <summary>Play a named clip on whichever skeletal backend is active.</summary>
        private void PlaySkeletalClip(string clipName)
        {
            if (_skelAnimator != null)
                _skelAnimator.Play(clipName);
            else if (_legacyAnimation != null)
                _legacyAnimation.Play(clipName);
        }

        /// <summary>
        /// Per-frame update when skeletal mode is active.
        /// Handles the Attack-complete callback and Hit→Idle transition.
        /// Death and Idle are fire-and-forget (Death is Once, Idle loops).
        /// </summary>
        private void UpdateSkeletal()
        {
            switch (_state)
            {
                case AnimState.Attack:
                    if (IsSkeletalClipFinished(CLIP_ATTACK))
                    {
                        _state = AnimState.Idle;
                        PlaySkeletalClip(CLIP_IDLE);
                        var cb = _skelAttackOnComplete;
                        _skelAttackOnComplete = null;
                        cb?.Invoke();
                    }
                    break;

                case AnimState.Hit:
                    if (IsSkeletalClipFinished(CLIP_HIT))
                    {
                        _state = AnimState.Idle;
                        PlaySkeletalClip(CLIP_IDLE);
                    }
                    break;

                // Death and Idle need no per-frame intervention
            }
        }

        /// <summary>
        /// Returns true when a Once-mode clip has reached (or passed) its end.
        /// Works for both Mecanim and legacy Animation backends.
        /// </summary>
        private bool IsSkeletalClipFinished(string clipName)
        {
            if (_skelAnimator != null)
            {
                var info = _skelAnimator.GetCurrentAnimatorStateInfo(0);
                // State name may be the clip name directly; check normalized time
                return info.IsName(clipName) && info.normalizedTime >= 1f && !info.loop;
            }
            if (_legacyAnimation != null)
            {
                var state = _legacyAnimation[clipName];
                return state != null && !_legacyAnimation.IsPlaying(clipName);
            }
            return false;
        }

        // === Procedural animation updaters ===

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
