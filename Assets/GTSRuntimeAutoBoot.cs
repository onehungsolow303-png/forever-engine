using UnityEngine;
using ProceduralWorlds.GTS;

/// <summary>
/// Auto-applies PW's documented GTS runtime globals once per session, before
/// any GTS-shaded terrain renders. The GTSProfile.SetRuntimeData() pass at
/// bake time captures all shader globals (height/UV/snow/rain vector arrays +
/// textures) into a GTSRuntime ScriptableObject; SetGTSRuntimeData
/// MonoBehaviour writes them via Shader.SetGlobalX on Start. We hook
/// RuntimeInitializeOnLoadMethod(AfterSceneLoad) so it works in both
/// editor play-mode and standalone builds.
///
/// Why this file is at Assets/ root (not under Assets/Scripts/):
///   ForeverEngine.asmdef covers Assets/Scripts/**, and GTS Core has NO
///   asmdef so it lives in Assembly-CSharp. asmdef code cannot reference
///   types in Assembly-CSharp (gaia skill Bug #17). Placing this file
///   outside any asmdef puts it in Assembly-CSharp where it can directly
///   reference ProceduralWorlds.GTS types.
///
/// See gaia skill Bug #24 for the editor-only-gates discovery that motivated
/// using SetGTSRuntimeData over the naive AddGTSToTerrain runtime port.
/// </summary>
public static class GTSRuntimeAutoBoot
{
    // Hardcoded biome key — Coniferous Forest Medium is the only baked biome.
    // Resources.Load path matches what GaiaHeadlessPipeline.ApplyGTSToBakedTerrains
    // writes via the moved Resources/GTSProfiles/ output. Lift to per-biome
    // registry when a 2nd biome lands.
    private const string BiomeKey = "Coniferous_Forest_Medium";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        // Idempotent: if the bootstrap GO already exists from a prior scene
        // load (DontDestroyOnLoad keeps it alive across scene swaps), skip.
        if (GameObject.Find("__GTSRuntime") != null) return;

        var runtime = Resources.Load<GTSRuntime>(
            $"GTSProfiles/{BiomeKey}_GTS_GTSData/{BiomeKey}_GTS_Runtime");
        if (runtime == null)
        {
            // No GTS bake artifacts in Resources — silent no-op so non-GTS
            // scenes (main menu, dungeon scenes) don't log noise.
            return;
        }

        var go = new GameObject("__GTSRuntime");
        Object.DontDestroyOnLoad(go);
        var setter = go.AddComponent<SetGTSRuntimeData>();
        setter.runtimeData = runtime;
        // Force-set immediately so terrains spawned in the same frame use the
        // correct globals. Start() would otherwise fire one frame late.
        setter.SetGTSGlobalData();
        Debug.Log($"[GTSRuntimeAutoBoot] applied '{runtime.name}'");
    }
}
