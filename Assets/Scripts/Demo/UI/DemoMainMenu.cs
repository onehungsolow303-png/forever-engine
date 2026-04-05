using UnityEngine;

namespace ForeverEngine.Demo.UI
{
    public class DemoMainMenu : UnityEngine.MonoBehaviour
    {
        private void OnGUI()
        {
            // Title
            var titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 36, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(0, Screen.height * 0.2f, Screen.width, 50), "SHATTERED KINGDOM", titleStyle);

            var subStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(0, Screen.height * 0.2f + 50, Screen.width, 30), "A Forever Engine Demo", subStyle);

            // Buttons
            float btnW = 200, btnH = 40;
            float x = Screen.width / 2 - btnW / 2;
            float y = Screen.height * 0.5f;

            if (GUI.Button(new Rect(x, y, btnW, btnH), "New Game"))
            {
                // Ensure GameManager exists
                if (GameManager.Instance == null)
                {
                    var go = new GameObject("GameManager");
                    go.AddComponent<GameManager>();
                }
                GameManager.Instance.NewGame(Random.Range(1, 99999));
            }

            if (GUI.Button(new Rect(x, y + 50, btnW, btnH), "Continue"))
            {
                var sm = ForeverEngine.MonoBehaviour.SaveLoad.SaveManager.Instance;
                if (sm != null) sm.Load("quicksave");
            }

            if (GUI.Button(new Rect(x, y + 100, btnW, btnH), "Quit"))
            {
                Application.Quit();
            }
        }
    }
}
