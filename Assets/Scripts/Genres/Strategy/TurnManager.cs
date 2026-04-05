using UnityEngine;
using System.Collections.Generic;

namespace ForeverEngine.Genres.Strategy
{
    public enum TurnPhase { Movement, Action, End }

    public class TurnManager : MonoBehaviour
    {
        public static TurnManager Instance { get; private set; }
        public int CurrentTurn { get; private set; }
        public int CurrentPlayerIndex { get; private set; }
        public TurnPhase Phase { get; private set; }

        private List<int> _playerOrder = new();
        public int PlayerCount => _playerOrder.Count;

        public event System.Action<int, int> OnTurnChanged; // playerIndex, turnNumber
        public event System.Action<TurnPhase> OnPhaseChanged;

        private void Awake() => Instance = this;

        public void Initialize(int playerCount) { _playerOrder.Clear(); for (int i = 0; i < playerCount; i++) _playerOrder.Add(i); CurrentTurn = 1; CurrentPlayerIndex = 0; Phase = TurnPhase.Movement; }

        public void NextPhase()
        {
            Phase = Phase switch { TurnPhase.Movement => TurnPhase.Action, TurnPhase.Action => TurnPhase.End, _ => TurnPhase.Movement };
            OnPhaseChanged?.Invoke(Phase);
            if (Phase == TurnPhase.End) EndTurn();
        }

        public void EndTurn()
        {
            CurrentPlayerIndex = (CurrentPlayerIndex + 1) % _playerOrder.Count;
            if (CurrentPlayerIndex == 0) CurrentTurn++;
            Phase = TurnPhase.Movement;
            OnTurnChanged?.Invoke(CurrentPlayerIndex, CurrentTurn);
        }

        public bool IsPlayerTurn(int playerId) => _playerOrder[CurrentPlayerIndex] == playerId;
    }
}
