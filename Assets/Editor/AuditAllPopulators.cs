#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ForeverEngine.Editor
{
    public static class AuditAllPopulators
    {
        [MenuItem("Forever Engine/Audit/Run All Populators")]
        public static void Run()
        {
            Debug.Log("[Audit] === Running all populators ===");

            SafeRun("CreateMissingAssets.Create",          () => CreateMissingAssets.Create());
            SafeRun("OverworldPrefabPopulator.Populate",   () => OverworldPrefabPopulator.Populate());
            SafeRun("RoomCatalogPopulator.Populate",       () => RoomCatalogPopulator.Populate());
            SafeRun("AudioPopulator.Populate",             () => AudioPopulator.Populate());
            SafeRun("PopulateBiomePropCatalog.Populate",   () => PopulateBiomePropCatalog.Populate());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Audit] === All populators finished ===");
        }

        private static void SafeRun(string name, System.Action fn)
        {
            Debug.Log($"[Audit] --- {name} ---");
            try { fn(); Debug.Log($"[Audit] {name} OK"); }
            catch (System.Exception e) { Debug.LogError($"[Audit] {name} FAILED: {e.Message}\n{e.StackTrace}"); }
        }
    }
}
#endif
