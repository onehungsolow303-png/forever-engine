using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ForeverEngine.Demo.Atmosphere
{
    /// <summary>
    /// Bootstraps cinematic atmosphere on scene load: URP post-processing volume,
    /// procedural skybox, exponential fog, and directional light upgrade.
    /// Attach to any persistent GameObject (e.g. via WorldBootstrap).
    /// </summary>
    public class AtmosphereSetup : UnityEngine.MonoBehaviour
    {
        [Header("Post-Processing")]
        public float BloomIntensity = 0.8f;
        public float BloomThreshold = 1.1f;
        public float VignetteIntensity = 0.3f;
        public float ContrastBoost = 15f;
        public float SaturationBoost = 10f;

        [Header("Fog")]
        public Color FogColor = new Color(0.45f, 0.52f, 0.65f, 1f);
        public float FogDensity = 0.012f;

        [Header("Skybox")]
        public Color SkyZenith = new Color(0.08f, 0.10f, 0.22f);
        public Color SkyHorizon = new Color(0.55f, 0.35f, 0.18f);
        public Color GroundColor = new Color(0.15f, 0.12f, 0.10f);

        [Header("Lighting")]
        public Color SunColor = new Color(1f, 0.92f, 0.75f);
        public float SunIntensity = 1.4f;
        public Color AmbientSky = new Color(0.35f, 0.38f, 0.55f);
        public Color AmbientEquator = new Color(0.25f, 0.22f, 0.20f);
        public Color AmbientGround = new Color(0.12f, 0.10f, 0.08f);

        private Volume _volume;

        private void Awake()
        {
            SetupPostProcessing();
            SetupSkybox();
            SetupFog();
            SetupLighting();
        }

        private void SetupPostProcessing()
        {
            // Create a global URP Volume
            var volumeGO = new GameObject("PostProcessVolume");
            volumeGO.transform.SetParent(transform);
            _volume = volumeGO.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 100;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _volume.profile = profile;

            // Bloom — soft glow on bright surfaces
            var bloom = profile.Add<Bloom>();
            bloom.active = true;
            bloom.intensity.Override(BloomIntensity);
            bloom.threshold.Override(BloomThreshold);
            bloom.scatter.Override(0.65f);

            // Tonemapping — ACES cinematic curve
            var tonemap = profile.Add<Tonemapping>();
            tonemap.active = true;
            tonemap.mode.Override(TonemappingMode.ACES);

            // Color Grading — warm shadows, cool highlights
            var colorGrading = profile.Add<ColorAdjustments>();
            colorGrading.active = true;
            colorGrading.contrast.Override(ContrastBoost);
            colorGrading.saturation.Override(SaturationBoost);
            colorGrading.postExposure.Override(0.3f);

            // Split Toning — warm shadows, cool highlights for fantasy look
            var splitTone = profile.Add<SplitToning>();
            splitTone.active = true;
            splitTone.shadows.Override(new Color(0.6f, 0.4f, 0.25f)); // Warm amber shadows
            splitTone.highlights.Override(new Color(0.4f, 0.5f, 0.7f)); // Cool blue highlights
            splitTone.balance.Override(-20f);

            // Vignette — subtle darkening at edges
            var vignette = profile.Add<Vignette>();
            vignette.active = true;
            vignette.intensity.Override(VignetteIntensity);
            vignette.smoothness.Override(0.4f);
            vignette.color.Override(new Color(0.05f, 0.02f, 0.08f));

            // Film Grain — very subtle texture
            var grain = profile.Add<FilmGrain>();
            grain.active = true;
            grain.intensity.Override(0.15f);
            grain.type.Override(FilmGrainLookup.Medium1);

            Debug.Log("[AtmosphereSetup] Post-processing volume created");
        }

        private void SetupSkybox()
        {
            // Don't override the existing skybox material — URP manages its own
            // rendering pipeline and replacing the skybox with a built-in procedural
            // shader can cause pink/magenta rendering artifacts.
            // Instead, just tint the existing skybox if present, or set camera background.
            var cam = Camera.main;
            if (cam != null)
            {
                // If no skybox exists, use a solid gradient color
                if (RenderSettings.skybox == null)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = SkyZenith;
                }
            }

            // Set ambient reflection to match our lighting mood
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
            Debug.Log("[AtmosphereSetup] Skybox configured");
        }

        private void SetupFog()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogDensity = FogDensity;
            Debug.Log("[AtmosphereSetup] Fog enabled");
        }

        private void SetupLighting()
        {
            // Upgrade directional light
            var lights = FindObjectsByType<Light>();
            foreach (var light in lights)
            {
                if (light.type == LightType.Directional)
                {
                    light.color = SunColor;
                    light.intensity = SunIntensity;
                    light.shadows = LightShadows.Soft;
                    light.shadowStrength = 0.6f;
                    light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
                    break;
                }
            }

            // Ambient lighting — gradient mode for rich atmosphere
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;

            Debug.Log("[AtmosphereSetup] Lighting configured");
        }

        /// <summary>
        /// Switch to night atmosphere. Caller can be any day/night driver.
        /// </summary>
        public void SetNightMode(bool night)
        {
            if (night)
            {
                RenderSettings.fogColor = new Color(0.08f, 0.08f, 0.15f);
                RenderSettings.fogDensity = FogDensity * 1.5f;
                RenderSettings.ambientSkyColor = new Color(0.05f, 0.05f, 0.12f);
                RenderSettings.ambientEquatorColor = new Color(0.06f, 0.05f, 0.08f);
            }
            else
            {
                RenderSettings.fogColor = FogColor;
                RenderSettings.fogDensity = FogDensity;
                RenderSettings.ambientSkyColor = AmbientSky;
                RenderSettings.ambientEquatorColor = AmbientEquator;
            }
        }
    }
}
