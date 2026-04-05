using UnityEngine;
using System.Collections.Generic;

namespace ForeverEngine.MonoBehaviour.Animation
{
    public class SpriteAnimator : UnityEngine.MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Sprite[] _spriteSheet;
        private Dictionary<string, AnimClip> _clips = new();
        private AnimState _current;

        public void RegisterClip(AnimClip clip) => _clips[clip.Name] = clip;

        public void Play(string clipName)
        {
            if (!_clips.TryGetValue(clipName, out var clip)) return;
            if (_current?.Clip.Name == clipName && !_current.Finished) return;
            _current = new AnimState(clip);
        }

        private void Update()
        {
            if (_current == null) return;
            _current.Advance(Time.deltaTime);
            if (_spriteSheet != null && _current.CurrentFrame < _spriteSheet.Length && _spriteRenderer != null)
                _spriteRenderer.sprite = _spriteSheet[_current.CurrentFrame];
        }

        public string CurrentClipName => _current?.Clip.Name;
        public int CurrentFrame => _current?.CurrentFrame ?? 0;
        public bool IsFinished => _current?.Finished ?? true;
    }
}
