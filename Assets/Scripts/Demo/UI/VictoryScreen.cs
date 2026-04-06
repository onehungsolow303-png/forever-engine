using UnityEngine;

namespace ForeverEngine.Demo.UI
{
    public class VictoryScreen : UnityEngine.MonoBehaviour
    {
        public static bool Show;

        private void OnGUI()
        {
            if (!Show) return;

            float s = Screen.height / 1080f;

            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");

            var titleStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(48 * s), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, normal = { textColor = Color.yellow } };
            GUI.Label(new Rect(0, Screen.height * 0.2f, Screen.width, 60 * s), "THE ROT KING IS SLAIN", titleStyle);

            var subStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(18 * s), alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(0, Screen.height * 0.35f, Screen.width, 30 * s), "The curse begins to lift. The kingdom may yet recover.", subStyle);

            var gm = GameManager.Instance;
            if (gm != null)
            {
                var infoStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(14 * s), alignment = TextAnchor.MiddleCenter };
                var p = gm.Player;
                GUI.Label(new Rect(0, Screen.height * 0.45f, Screen.width, 20 * s), $"Level: {p.Level} | Gold: {p.Gold} | Days Survived: {p.DayCount}", infoStyle);
                GUI.Label(new Rect(0, Screen.height * 0.48f, Screen.width, 20 * s), $"Hexes Explored: {p.ExploredHexes.Count} | Locations Found: {p.DiscoveredLocations.Count}", infoStyle);
            }

            float bw = 150 * s, bh = 40 * s;
            if (GUI.Button(new Rect(Screen.width/2 - bw/2, Screen.height * 0.6f, bw, bh), "Main Menu"))
            {
                Show = false;
                GameManager.Instance?.GameComplete();
            }
        }
    }
}
