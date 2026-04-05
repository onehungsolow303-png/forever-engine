using UnityEngine;

namespace ForeverEngine.Demo.UI
{
    public class LootScreen : UnityEngine.MonoBehaviour
    {
        public static bool Show;
        public static int GoldEarned;
        public static int XPEarned;
        public static string[] ItemsFound;

        private void OnGUI()
        {
            if (!Show) return;

            GUI.Box(new Rect(Screen.width/2 - 120, Screen.height/2 - 80, 240, 180), "Loot");

            GUI.Label(new Rect(Screen.width/2 - 100, Screen.height/2 - 55, 200, 20), $"Gold: +{GoldEarned}");
            GUI.Label(new Rect(Screen.width/2 - 100, Screen.height/2 - 35, 200, 20), $"XP: +{XPEarned}");

            if (ItemsFound != null)
            {
                for (int i = 0; i < ItemsFound.Length && i < 4; i++)
                    GUI.Label(new Rect(Screen.width/2 - 100, Screen.height/2 - 15 + i * 20, 200, 20), $"  {ItemsFound[i]}");
            }

            if (GUI.Button(new Rect(Screen.width/2 - 50, Screen.height/2 + 65, 100, 30), "Continue"))
            {
                Show = false;
            }
        }
    }
}
