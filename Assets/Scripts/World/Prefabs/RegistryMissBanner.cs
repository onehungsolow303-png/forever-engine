using UnityEngine;

namespace ForeverEngine.Procedural
{
    /// <summary>
    /// Draws the out-of-sync banner when PrefabRegistryValidator decides the
    /// miss rate exceeds threshold. Installed lazily on a hidden DontDestroyOnLoad
    /// GameObject so existing bootstrap code doesn't need to know about it.
    /// </summary>
    public class RegistryMissBanner : UnityEngine.MonoBehaviour
    {
        private static RegistryMissBanner _instance;

        public static void EnsureInstalled()
        {
            if (_instance != null) return;
            var go = new GameObject("[PrefabRegistryMissBanner]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<RegistryMissBanner>();
        }

        private void OnGUI()
        {
            if (!PrefabRegistryValidator.BannerActive) return;

            var style = new GUIStyle(GUI.skin.box)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
            };
            style.normal.textColor = Color.yellow;

            int hits = PrefabRegistryValidator.Hits;
            int misses = PrefabRegistryValidator.Misses;
            int total = hits + misses;
            float missRate = total == 0 ? 0f : (float)misses / total;

            var msg =
                $"PrefabRegistry OUT-OF-SYNC — {misses}/{total} prop GUIDs missing ({missRate:P1}). " +
                $"Run FullPipelineRebuild + deploy_props_bin.ps1.";
            var rect = new Rect(10, 10, Screen.width - 20, 50);
            GUI.Box(rect, msg, style);
        }
    }
}
