using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace ForeverEngine.AI.Director
{
    public class AIDirector : MonoBehaviour
    {
        public static AIDirector Instance { get; private set; }
        [SerializeField] private DirectorConfig _config;

        private PacingController _pacing = new();
        private DramaManager _drama = new();
        private float _timeSinceLastEvent;
        private List<DirectorEvent> _history = new();

        public PacingController Pacing => _pacing;
        public DramaManager Drama => _drama;

        private void Awake() => Instance = this;

        private void Update()
        {
            _timeSinceLastEvent += Time.deltaTime;
            _pacing.Update(Time.deltaTime);
            _drama.Update(Time.deltaTime);

            if (_config == null || _config.PossibleActions == null) return;
            if (_timeSinceLastEvent < (_config.MinCalmBeforeEvent)) return;

            var best = _config.PossibleActions
                .Where(a => a.IsReady(Time.time))
                .OrderByDescending(a => ScoreAction(a))
                .FirstOrDefault();

            if (best != null && ScoreAction(best) > _config.MinActionThreshold)
            {
                ExecuteAction(best);
                _timeSinceLastEvent = 0;
            }
        }

        private float ScoreAction(DirectorAction action)
        {
            float score = 0;
            score += action.PacingWeight * (1f - Mathf.Abs(_pacing.CurrentIntensity - action.TargetIntensity));
            score += action.DramaWeight * _drama.DramaNeed;
            score += action.TimeWeight * Mathf.Clamp01(_timeSinceLastEvent / 60f);
            return score;
        }

        private void ExecuteAction(DirectorAction action)
        {
            action.LastUsedTime = Time.time;
            _history.Add(new DirectorEvent { ActionId = action.Id, Time = Time.time, Score = ScoreAction(action) });
            action.OnExecute?.Invoke();
            Debug.Log($"[Director] Executing: {action.Id} (score: {ScoreAction(action):F2})");
        }

        public List<DirectorEvent> GetHistory() => new(_history);
    }

    [System.Serializable]
    public struct DirectorEvent { public string ActionId; public float Time; public float Score; }
}
