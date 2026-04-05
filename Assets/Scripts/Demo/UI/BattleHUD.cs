using UnityEngine;
using ForeverEngine.Demo.Battle;

namespace ForeverEngine.Demo.UI
{
    public class BattleHUD : UnityEngine.MonoBehaviour
    {
        private Vector2 _logScroll;

        private void OnGUI()
        {
            var bm = BattleManager.Instance;
            if (bm == null) return;

            // Turn info
            GUI.Box(new Rect(10, 10, 200, 80), "");
            GUI.Label(new Rect(20, 15, 180, 20), $"<b>Round {bm.RoundNumber}</b>");
            if (bm.CurrentTurn != null)
                GUI.Label(new Rect(20, 35, 180, 20), $"Turn: {bm.CurrentTurn.Name}");
            if (bm.CurrentTurn != null && bm.CurrentTurn.IsPlayer)
                GUI.Label(new Rect(20, 55, 180, 20), $"Moves: {bm.CurrentTurn.MovementRemaining} | {(bm.CurrentTurn.HasAction ? "Action ready" : "Action used")}");

            // Combatant list
            GUI.Box(new Rect(10, 100, 200, 20 + bm.Combatants.Count * 22), "");
            for (int i = 0; i < bm.Combatants.Count; i++)
            {
                var c = bm.Combatants[i];
                string marker = c == bm.CurrentTurn ? ">" : " ";
                Color col = c.IsPlayer ? Color.cyan : (c.IsAlive ? Color.red : Color.gray);
                var style = new GUIStyle(GUI.skin.label) { normal = { textColor = col }, fontSize = 11 };
                GUI.Label(new Rect(20, 105 + i * 22, 180, 20), $"{marker}{c.Name} HP:{c.HP}/{c.MaxHP}", style);
            }

            // Combat log
            int logY = Screen.height - 160;
            GUI.Box(new Rect(10, logY, 350, 150), "Combat Log");
            _logScroll = GUI.BeginScrollView(new Rect(10, logY + 20, 350, 130), _logScroll, new Rect(0, 0, 320, bm.Log.Count * 18));
            for (int i = 0; i < bm.Log.Count; i++)
                GUI.Label(new Rect(5, i * 18, 320, 18), bm.Log[i]);
            GUI.EndScrollView();

            // Player controls
            if (bm.CurrentTurn != null && bm.CurrentTurn.IsPlayer && !bm.BattleOver)
            {
                GUI.Label(new Rect(Screen.width/2 - 200, Screen.height - 30, 400, 20), "WASD: Move | Click enemy: Attack | Space: End Turn");
            }

            // Battle over
            if (bm.BattleOver)
            {
                GUI.Box(new Rect(Screen.width/2 - 100, Screen.height/2 - 40, 200, 80), "");
                GUI.Label(new Rect(Screen.width/2 - 80, Screen.height/2 - 30, 160, 20), bm.PlayerWon ? "<b>Victory!</b>" : "<b>Defeated...</b>");
                if (bm.PlayerWon)
                    GUI.Label(new Rect(Screen.width/2 - 80, Screen.height/2 - 10, 160, 20), $"Gold: +{GameManager.Instance?.LastBattleGoldEarned ?? 0}");
                if (GUI.Button(new Rect(Screen.width/2 - 50, Screen.height/2 + 15, 100, 25), "Continue"))
                    bm.EndBattle();
            }
        }
    }
}
