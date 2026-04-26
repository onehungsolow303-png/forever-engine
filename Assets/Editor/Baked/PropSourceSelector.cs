#if UNITY_EDITOR
using ForeverEngine.Core.World.Baked;
using UnityEditor;
using UnityEngine;

namespace ForeverEngine.Procedural.Editor
{
    // Single dispatch point for the bake tools' prop source. Two paths:
    //
    //   Gaia-authored (default): walk the active Terrain's prefab-instance children
    //   via GaiaPlacementExtractor. Gaia has already placed + physics-settled props
    //   per its fitness rules, so we just record what's on the terrain.
    //
    //   Synthetic (fallback): PropPlacementSampler — deterministic placer using
    //   BakedElevationSynth + slope filter. Useful when Gaia hasn't been run yet
    //   (e.g. CI / test fixtures / batchmode bakes that don't open scenes).
    //
    // Toggle persists in EditorPrefs so a deliberate flip survives domain reload.
    // If Gaia-authored is selected but the terrain has no spawned children, we
    // log a loud warning and fall back to synthetic — never silently produce an
    // empty props.bin (that bug pattern cost us the entire 2026-04-23 morning).
    public static class PropSourceSelector
    {
        private const string PrefKey = "ForeverEngine.MacroBake.UseGaiaAuthored";

        public static bool UseGaiaAuthored
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        [MenuItem("Forever Engine/Bake/Toggle Prop Source (Gaia <-> Synthetic)")]
        public static void Toggle()
        {
            UseGaiaAuthored = !UseGaiaAuthored;
            Debug.Log($"[PropSource] Toggled. Now: {(UseGaiaAuthored ? "Gaia-authored" : "Synthetic")}");
        }

        [MenuItem("Forever Engine/Bake/Toggle Prop Source (Gaia <-> Synthetic)", true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked("Forever Engine/Bake/Toggle Prop Source (Gaia <-> Synthetic)", UseGaiaAuthored);
            return true;
        }

        public static BakedPropPlacement[] Sample(
            Terrain terrain,
            float worldMinX, float worldMinZ,
            float cellSizeMeters, int widthCells, int heightCells,
            float[] heights, byte[] biome,
            AssetPackBiomeCatalog catalog,
            int seed, byte layerId)
        {
            if (UseGaiaAuthored)
            {
                var gaia = GaiaPlacementExtractor.Extract(terrain.gameObject);
                if (gaia.Length > 0)
                {
                    Debug.Log($"[PropSource] Gaia-authored: {gaia.Length} placements from terrain '{terrain.name}'");
                    return gaia;
                }
                Debug.LogWarning(
                    $"[PropSource] Gaia returned 0 placements for terrain '{terrain.name}'. " +
                    "Terrain has no spawned prefab children — did you run the Biome Controller? " +
                    "Falling back to synthetic PropPlacementSampler.");
            }

            return PropPlacementSampler.Sample(
                worldMinX: worldMinX, worldMinZ: worldMinZ,
                cellSizeMeters: cellSizeMeters,
                widthCells: widthCells, heightCells: heightCells,
                heightmap: heights, biome: biome,
                catalog: catalog,
                seed: seed, layerId: layerId);
        }
    }
}
#endif
