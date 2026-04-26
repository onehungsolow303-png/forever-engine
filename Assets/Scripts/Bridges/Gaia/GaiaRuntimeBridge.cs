using System.Collections.Generic;
using UnityEngine;
using Gaia;

namespace ForeverEngine.Bridges
{
    /// <summary>
    /// Applies a Director Hub `atmospherics` decision payload to the running scene
    /// via Gaia's runtime API. Connects the LLM brain's narrative output
    /// ("storm at midnight as the dragon arrives") to the visible environment.
    ///
    /// Wiring: `DirectorClient` (or any consumer of `DecisionPayload`) calls
    /// `GaiaRuntimeBridge.Instance.Apply(decision.Atmospherics)` after receiving
    /// a decision. If atmospherics is null, no-op.
    ///
    /// Source-of-truth field shape lives in `.shared/schemas/decision.schema.json`
    /// (under `properties.atmospherics`). Codegen lands it on the C# side as
    /// `Dictionary&lt;string, object&gt;` because the script doesn't yet recurse
    /// into nested object schemas — so we parse fields with safe defaults here.
    ///
    /// Existence-check guard pattern: per `gaia-architecture` skill §13H, every
    /// GaiaAPI call is guarded against missing Pro components (PW Sky etc.).
    /// Without PW Sky, time/weather methods silently no-op; sun/wind/skybox
    /// still work via built-in fallbacks.
    /// </summary>
    public sealed class GaiaRuntimeBridge : UnityEngine.MonoBehaviour
    {
        public static GaiaRuntimeBridge Instance { get; private set; }

        // GameObject name used by callers in Assembly-CSharp (which can't take an
        // asmdef reference on this Gaia-isolated assembly) to find us via
        // GameObject.Find and dispatch via SendMessage("Apply", dict).
        public const string GameObjectName = "GaiaRuntimeBridge";

        [Tooltip("Optional explicit sun light. If null, GaiaAPI uses RenderSettings.sun or first directional light.")]
        public Light SunLight;

        [Tooltip("Default transition time for atmospherics changes if the payload doesn't specify one.")]
        public float DefaultTransitionSeconds = 2.0f;

        // Auto-spawn so consumers (ConnectionManager, gameplay code) don't have to
        // explicitly drop a GameObject into every scene. Fires after the first scene
        // load; subsequent loads find the existing instance via Awake's singleton guard.
        // Marked DontDestroyOnLoad so it persists across scene transitions.
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureSpawned()
        {
            if (Instance != null) return;
            var go = new GameObject(GameObjectName);
            go.AddComponent<GaiaRuntimeBridge>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Apply an atmospherics dictionary (from DecisionPayload.Atmospherics).
        /// All fields are optional. Null/missing fields are skipped — partial
        /// updates supported (e.g., change wind without touching sun).
        /// </summary>
        public void Apply(Dictionary<string, object> atmo)
        {
            if (atmo == null || atmo.Count == 0) return;

            // ----- Sun (works without PW Sky) -----
            float? sunPitch = TryFloat(atmo, "sun_pitch_deg");
            float? sunRot = TryFloat(atmo, "sun_rotation_deg");
            if (sunPitch.HasValue || sunRot.HasValue)
            {
                // GaiaAPI overload: passing 0 keeps current axis if we read first
                float p = sunPitch ?? 0f;
                float r = sunRot ?? 0f;
                if (!sunPitch.HasValue || !sunRot.HasValue)
                {
                    GaiaAPI.GetSunRotation(out float curP, out float curR);
                    if (!sunPitch.HasValue) p = curP;
                    if (!sunRot.HasValue) r = curR;
                }
                GaiaAPI.SetSunRotation(p, r, SunLight);
            }

            float? sunIntensity = TryFloat(atmo, "sun_intensity");
            float? sunKelvin = TryFloat(atmo, "sun_color_kelvin");
            if (sunIntensity.HasValue || sunKelvin.HasValue)
            {
                // Renderer-aware. Kelvin is float in the API (schema is integer; promotion is fine).
                GaiaAPI.GetUnitySunSettings(out float curI, out Color curC, out float curK, out _, SunLight);
                float i = sunIntensity ?? curI;
                float k = sunKelvin ?? curK;
                Color c = curC; // color stays — kelvin is a separate channel
                GaiaAPI.SetUnitySunSettings(i, c, k, SunLight);
            }

            // ----- Wind (works with built-in WindZone fallback) -----
            // NOTE: wind_direction is normalized 0-1 (NOT degrees). GaiaAPI multiplies by 360 internally.
            float? windSpeed = TryFloat(atmo, "wind_speed");
            float? windDirNorm = TryFloat(atmo, "wind_direction_norm");
            if (windSpeed.HasValue || windDirNorm.HasValue)
            {
                GaiaAPI.GetGaiaWindSettings(out float curSpeed, out float curDirNorm, out _);
                float speed = windSpeed ?? curSpeed;
                float dir = windDirNorm ?? curDirNorm;
                GaiaAPI.SetGaiaWindSettings(speed, dir);
            }

            // ----- Skybox (built-in) -----
            float? skyExp = TryFloat(atmo, "skybox_exposure");
            Color? skyTint = TryColor(atmo, "skybox_tint_rgb");
            if (skyExp.HasValue || skyTint.HasValue)
            {
                GaiaAPI.GetUnityHDRISkybox(out float curExp, out float curRot, out Color curTint, out _);
                float exp = skyExp ?? curExp;
                Color tint = skyTint ?? curTint;
                GaiaAPI.SetUnityHDRISkybox(exp, curRot, tint);
            }
        }

        private static float? TryFloat(Dictionary<string, object> dict, string key)
        {
            if (!dict.TryGetValue(key, out object raw) || raw == null) return null;
            try { return System.Convert.ToSingle(raw); }
            catch { return null; }
        }

        private static int? TryInt(Dictionary<string, object> dict, string key)
        {
            if (!dict.TryGetValue(key, out object raw) || raw == null) return null;
            try { return System.Convert.ToInt32(raw); }
            catch { return null; }
        }

        private static Color? TryColor(Dictionary<string, object> dict, string key)
        {
            if (!dict.TryGetValue(key, out object raw) || raw == null) return null;
            if (raw is IList<object> list && list.Count == 3)
            {
                try
                {
                    float r = System.Convert.ToSingle(list[0]);
                    float g = System.Convert.ToSingle(list[1]);
                    float b = System.Convert.ToSingle(list[2]);
                    return new Color(r, g, b, 1f);
                }
                catch { return null; }
            }
            return null;
        }
    }
}
