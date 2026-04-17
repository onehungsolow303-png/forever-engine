using UnityEngine;

namespace ForeverEngine.Demo.UI
{
    /// <summary>
    /// Escape-key pause menu with Resume, Save, Load, and Quit.
    /// Pauses game time while open.
    /// </summary>
    public class PauseMenu : UnityEngine.MonoBehaviour
    {
        public static PauseMenu Instance { get; private set; }
        public bool IsOpen { get; private set; }

        private float _savedTimeScale;

        private void Awake() => Instance = this;

        private void Update()
        {
            // Don't open pause during active combat turns or dialogue
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (IsOpen)
                    Resume();
                else
                    Open();
            }

            // Quicksave / Quickload removed: server is now authoritative for state persistence.
        }

        public void Open()
        {
            // Don't pause during BattleHUD's battle-over screen
            var bm = Battle.BattleManager.Instance;
            if (bm != null && bm.BattleOver) return;

            IsOpen = true;
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        public void Resume()
        {
            IsOpen = false;
            Time.timeScale = _savedTimeScale > 0f ? _savedTimeScale : 1f;
        }

        private void OnGUI()
        {
            if (!IsOpen) return;

            // Dim overlay
            GUI.color = new Color(0, 0, 0, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float panelW = 260;
            float panelH = 280;
            float px = Screen.width / 2f - panelW / 2f;
            float py = Screen.height / 2f - panelH / 2f;

            UITheme.DrawPanel(new Rect(px, py, panelW, panelH));

            GUI.Label(new Rect(px + 10, py + 15, panelW - 20, 30),
                "PAUSED", UITheme.Bold(UITheme.FontLarge, UITheme.TextHeader, TextAnchor.MiddleCenter));

            float btnW = 200;
            float btnH = 36;
            float bx = px + (panelW - btnW) / 2f;
            float by = py + 60;
            float spacing = 44;

            // Resume
            if (GUI.Button(new Rect(bx, by, btnW, btnH), "Resume", UITheme.Button()))
                Resume();
            by += spacing;

            // Save/Load removed: server is now authoritative for state persistence.
            by += spacing * 2;

            // Quit to Menu
            if (GUI.Button(new Rect(bx, by, btnW, btnH), "Quit to Menu", UITheme.Button()))
            {
                Resume();
                Time.timeScale = 1f;
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
            }
        }
    }
}
