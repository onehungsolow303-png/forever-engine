using UnityEngine;
using System.Collections.Generic;

namespace ForeverEngine.Genres.Adventure
{
    public enum PuzzleState { Locked, InProgress, Solved }

    [System.Serializable]
    public class PuzzleTrigger { public string Id; public string Type; public bool Activated; }

    public class PuzzleSystem : UnityEngine.MonoBehaviour
    {
        public static PuzzleSystem Instance { get; private set; }
        private Dictionary<string, PuzzleInstance> _puzzles = new();

        private void Awake() => Instance = this;

        public void RegisterPuzzle(string id, string[] requiredTriggers)
        {
            _puzzles[id] = new PuzzleInstance { Id = id, RequiredTriggers = new HashSet<string>(requiredTriggers), ActivatedTriggers = new HashSet<string>(), State = PuzzleState.Locked };
        }

        public void ActivateTrigger(string puzzleId, string triggerId)
        {
            if (!_puzzles.TryGetValue(puzzleId, out var puzzle)) return;
            if (puzzle.State == PuzzleState.Solved) return;
            puzzle.State = PuzzleState.InProgress;
            puzzle.ActivatedTriggers.Add(triggerId);
            if (puzzle.ActivatedTriggers.IsSupersetOf(puzzle.RequiredTriggers))
            {
                puzzle.State = PuzzleState.Solved;
                Debug.Log($"[Puzzle] {puzzleId} solved!");
                OnPuzzleSolved?.Invoke(puzzleId);
            }
        }

        public PuzzleState GetState(string puzzleId) => _puzzles.TryGetValue(puzzleId, out var p) ? p.State : PuzzleState.Locked;
        public event System.Action<string> OnPuzzleSolved;

        private class PuzzleInstance { public string Id; public HashSet<string> RequiredTriggers; public HashSet<string> ActivatedTriggers; public PuzzleState State; }
    }
}
