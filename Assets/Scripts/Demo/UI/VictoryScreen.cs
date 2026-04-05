using UnityEngine;

namespace ForeverEngine.Demo.UI
{
    public class VictoryScreen : UnityEngine.MonoBehaviour
    {
        public static bool Show;

        private void OnGUI()
        {
            if (!Show) return;

            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");

            var titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 48, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, normal = { textColor = Color.yellow } };
            GUI.Label(new Rect(0, Screen.height * 0.2f, Screen.width, 60), "THE ROT KING IS SLAIN", titleStyle);

            var subStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(0, Screen.height * 0.35f, Screen.width, 30), "The curse begins to lift. The kingdom may yet recover.");

            var gm = GameManager.Instance;
            if (gm != null)
            {
                var p = gm.Player;
                GUI.Label(new Rect(0, Screen.height * 0.45f, Screen.width, 20), $"Level: {p.Level} | Gold: {p.Gold} | Days Survived: {p.DayCount}");
                GUI.Label(new Rect(0, Screen.height * 0.48f, Screen.width, 20), $"Hexes Explored: {p.ExploredHexes.Count} | Locations Found: {p.DiscoveredLocations.Count}");
            }

            if (GUI.Button(new Rect(Screen.width/2 - 75, Screen.height * 0.6f, 150, 40), "Main Menu"))
            {
                Show = false;
                GameManager.Instance?.GameComplete();
            }
        }
    }
}
