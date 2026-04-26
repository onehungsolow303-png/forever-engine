#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ForeverEngine.Bridges;

namespace ForeverEngine.Editor.Tests
{
    /// <summary>
    /// Editor smoke test for GaiaRuntimeBridge.
    ///
    /// Invocation:
    ///   - In editor: Forever Engine -> Tests -> Gaia Runtime Bridge Smoke
    ///   - Batchmode: -executeMethod ForeverEngine.Editor.Tests.GaiaRuntimeBridgeSmokeTest.Run -quit
    ///
    /// What it tests:
    ///   1. Bridge can be instantiated as a MonoBehaviour
    ///   2. Apply(null) and Apply(empty) are safe no-ops
    ///   3. Partial dictionary (sun only) doesn't NRE
    ///   4. Full dictionary (all 9 fields) doesn't NRE
    ///   5. Existence-check guards survive when PW Sky is missing
    ///
    /// What it does NOT test:
    ///   - Visual correctness (must be eyeballed in scene)
    ///   - End-to-end Director Hub -> Engine roundtrip (needs services up)
    /// </summary>
    public static class GaiaRuntimeBridgeSmokeTest
    {
        [MenuItem("Forever Engine/Tests/Gaia Runtime Bridge Smoke")]
        public static void Run()
        {
            int pass = 0, fail = 0;
            void OK(string msg) { Debug.Log($"[BridgeTest] PASS: {msg}"); pass++; }
            void FAIL(string msg) { Debug.LogError($"[BridgeTest] FAIL: {msg}"); fail++; }

            // Setup
            var go = new GameObject("BridgeTest_Temp");
            try
            {
                var bridge = go.AddComponent<GaiaRuntimeBridge>();
                if (bridge == null) { FAIL("AddComponent returned null"); return; }
                OK("Bridge instantiated as MonoBehaviour");

                // Awake doesn't fire on AddComponent in edit-mode (only at scene load
                // / play-mode start). In production, Awake runs and sets Instance.
                // SendMessage("Awake") triggers ShouldRunBehaviour assertion in edit
                // mode — use reflection instead to invoke the private method directly.
                var awake = typeof(GaiaRuntimeBridge).GetMethod("Awake",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                awake?.Invoke(bridge, null);
                if (GaiaRuntimeBridge.Instance == bridge) OK("Instance singleton set after manual Awake");
                else FAIL($"Instance not set ({GaiaRuntimeBridge.Instance})");

                // Test 1: null is safe no-op
                try { bridge.Apply(null); OK("Apply(null) is safe no-op"); }
                catch (System.Exception ex) { FAIL($"Apply(null) threw: {ex.Message}"); }

                // Test 2: empty dict is safe no-op
                try { bridge.Apply(new Dictionary<string, object>()); OK("Apply(empty dict) is safe no-op"); }
                catch (System.Exception ex) { FAIL($"Apply(empty) threw: {ex.Message}"); }

                // Test 3: partial dict (sun only)
                try
                {
                    bridge.Apply(new Dictionary<string, object> {
                        { "sun_pitch_deg", -45.0 },
                        { "sun_rotation_deg", 180.0 },
                    });
                    OK("Apply(partial sun-only) doesn't throw");
                }
                catch (System.Exception ex) { FAIL($"Apply(sun-only) threw: {ex.Message}"); }

                // Test 4: wind only
                try
                {
                    bridge.Apply(new Dictionary<string, object> {
                        { "wind_speed", 0.5 },
                        { "wind_direction_norm", 0.25 },
                    });
                    OK("Apply(wind-only) doesn't throw");
                }
                catch (System.Exception ex) { FAIL($"Apply(wind-only) threw: {ex.Message}"); }

                // Test 5: full payload
                try
                {
                    bridge.Apply(new Dictionary<string, object> {
                        { "sun_pitch_deg", 10.0 },
                        { "sun_rotation_deg", 270.0 },
                        { "sun_intensity", 0.5 },
                        { "sun_color_kelvin", 2800 },
                        { "wind_speed", 1.0 },
                        { "wind_direction_norm", 0.5 },
                        { "skybox_exposure", 0.4 },
                        { "skybox_tint_rgb", new List<object> { 0.5, 0.5, 0.6 } },
                        { "transition_seconds", 2.0 },
                    });
                    OK("Apply(full stormy-dusk payload) doesn't throw");
                }
                catch (System.Exception ex) { FAIL($"Apply(full payload) threw: {ex.Message}"); }

                // Test 6: malformed types (string where number expected) should be silently skipped
                try
                {
                    bridge.Apply(new Dictionary<string, object> {
                        { "sun_pitch_deg", "not_a_number" },
                        { "wind_speed", null },
                    });
                    OK("Apply(malformed values) silently skips bad fields");
                }
                catch (System.Exception ex) { FAIL($"Apply(malformed) threw: {ex.Message}"); }

                // Test 7: malformed skybox_tint_rgb (wrong length)
                try
                {
                    bridge.Apply(new Dictionary<string, object> {
                        { "skybox_tint_rgb", new List<object> { 0.5, 0.5 } }, // only 2 items
                    });
                    OK("Apply(malformed tint, length 2) silently skips");
                }
                catch (System.Exception ex) { FAIL($"Apply(malformed tint) threw: {ex.Message}"); }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            Debug.Log($"[BridgeTest] === DONE: {pass} pass, {fail} fail ===");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(fail > 0 ? 1 : 0);
            }
        }
    }
}
#endif
