using UnityEngine;

namespace ForeverEngine.Debugging
{
    /// <summary>
    /// Tiny OnGUI overlay: current FPS, instantaneous frame time, and the
    /// max frame time over a sliding 60-frame window. Auto-spawns in every
    /// scene via RuntimeInitializeOnLoadMethod. Toggle with F11.
    ///
    /// Purpose: diagnose client-side lag on the 5950X dedicated-server
    /// deployment. Constant low FPS = systemic load; 60 FPS with spikes =
    /// bursty main-thread work (likely network dispatch or mesh build).
    /// </summary>
    public class FrameTimeHud : UnityEngine.MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("FrameTimeHud");
            go.AddComponent<FrameTimeHud>();
            DontDestroyOnLoad(go);
        }

        private const int WindowFrames = 60;
        private readonly float[] _frameTimes = new float[WindowFrames];
        private int _idx;
        private bool _visible = true;
        private GUIStyle _style;

        private void Update()
        {
            _frameTimes[_idx] = Time.unscaledDeltaTime;
            _idx = (_idx + 1) % WindowFrames;
            if (Input.GetKeyDown(KeyCode.F11))
                _visible = !_visible;
        }

        private void OnGUI()
        {
            if (!_visible) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    normal = { textColor = Color.yellow },
                    fontStyle = FontStyle.Bold,
                };
            }

            float sum = 0f, max = 0f;
            int populated = 0;
            for (int i = 0; i < WindowFrames; i++)
            {
                float t = _frameTimes[i];
                if (t <= 0f) continue;
                sum += t;
                if (t > max) max = t;
                populated++;
            }
            if (populated == 0) return;

            float avgMs = (sum / populated) * 1000f;
            float maxMs = max * 1000f;
            float curMs = Time.unscaledDeltaTime * 1000f;
            float fps = 1f / Time.unscaledDeltaTime;

            string text =
                $"FPS: {fps:F0}\n" +
                $"cur:  {curMs:F1} ms\n" +
                $"avg60:{avgMs:F1} ms\n" +
                $"max60:{maxMs:F1} ms";

            GUI.Box(new Rect(10, 10, 170, 96), GUIContent.none);
            GUI.Label(new Rect(18, 14, 160, 90), text, _style);
        }
    }
}
