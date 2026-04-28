// One-shot batchmode pack importer.
// Unity 6's command-line `-importPackage` flag silently ignores all but the
// first occurrence in a single invocation (Phase A diagnosis 2026-04-27).
// This editor script works around that by calling AssetDatabase.ImportPackage
// serially via the importPackageCompleted callback chain — one Unity invocation
// imports many packs.
//
// Usage:
//   Edit the _packages list with full Windows paths to .unitypackage files.
//   Then invoke:
//     Unity.exe -batchmode -projectPath "..." \
//       -executeMethod ForeverEngine.Editor.BatchPackageImporter.RunAll \
//       -logFile <log>
//
// Note: -quit is intentionally OMITTED — RunAll calls EditorApplication.Exit
// itself once the chain completes (success, failure, or cancellation).
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ForeverEngine.Editor
{
    public static class BatchPackageImporter
    {
        // Phase B/C/D/E queue — edit and re-run for each batch. The Asset Store
        // cache is at %APPDATA%\Roaming\Unity\Asset Store-5.x. Hivemind sub-pack
        // URP installers live nested inside Assets/Hivemind/<SubPack>/URP/*.unitypackage.
        private static readonly List<string> _packages = new()
        {
            // 2026-04-28 PIPELINE PIVOT — import all Hivemind URP variants so URP content
            // is extracted alongside the existing HDRP(Default) extractions. After this
            // batch, the HDRP(Default) folders + the .unitypackage installers themselves
            // can be deleted (URP content stays in /URP/ subfolders post-import).
            @"C:\Dev\Forever engine\Assets\Hivemind\CastleOfEternalMists\URP\CastleOfEternalMistsURP.unitypackage",
            @"C:\Dev\Forever engine\Assets\Hivemind\CaveOfHiddenTomb\URP\CaveOfHiddenTombURP.unitypackage",
            @"C:\Dev\Forever engine\Assets\Hivemind\Dragon_Rise\URP\URP.unitypackage",
            @"C:\Dev\Forever engine\Assets\Hivemind\FantasyCemetery\URP\FantasyCemeteryURP.unitypackage",
            @"C:\Dev\Forever engine\Assets\Hivemind\GladitorArena\URP\GladiatorArenaURP.unitypackage",
            @"C:\Dev\Forever engine\Assets\Hivemind\GlimvaleStylizedFantasyOpenWorld\URP\GlimvaleOpenWorldURP.unitypackage",
            @"C:\Dev\Forever engine\Assets\Hivemind\HallowedDepths\URP\HallowedDepthsURP.unitypackage",
            @"C:\Dev\Forever engine\Assets\Hivemind\HouseForge_01\URP\HouseForgeURP.unitypackage",
            @"C:\Dev\Forever engine\Assets\Hivemind\MedievalFurnitureProps\URP\GothicCathedralURP.unitypackage",
            @"C:\Dev\Forever engine\Assets\Hivemind\MedievalFurnitureProps\URP\MedievalFurniturePropsURP.unitypackage",
            @"C:\Dev\Forever engine\Assets\Hivemind\ModularDungeon\URP\ModularDungeonURP.unitypackage",
            @"C:\Dev\Forever engine\Assets\Hivemind\ModularHouses\URP\ModularHousesURP.unitypackage",
            @"C:\Dev\Forever engine\Assets\Hivemind\ModularMedievalTown\URP\ModularMedievalTownURP.unitypackage",
            @"C:\Dev\Forever engine\Assets\Hivemind\ModularVikingVillage\URP\ModularVikingVillageURP.unitypackage",
            @"C:\Dev\Forever engine\Assets\Hivemind\ModularWoodenBuildings\URP\ModularWoodenBuildingsURP.unitypackage",
            @"C:\Dev\Forever engine\Assets\Hivemind\MountainTemple\URP\MountainTempleURP.unitypackage",
            @"C:\Dev\Forever engine\Assets\Hivemind\OlympusTemple\URP\OlympusURP.unitypackage",
            @"C:\Dev\Forever engine\Assets\Hivemind\OniValley\URP\StylizedNatureURP.unitypackage",
            @"C:\Dev\Forever engine\Assets\Hivemind\PirateIsland\URP\PirateIslandURP.unitypackage",
            @"C:\Dev\Forever engine\Assets\Hivemind\Sherwood\URP\URP_Hivemind_Sherwood.unitypackage",
            @"C:\Dev\Forever engine\Assets\Hivemind\StylizedTown\URP\StylizedTownURP.unitypackage",
            @"C:\Dev\Forever engine\Assets\Hivemind\TempleofCthulhu\URP\TempleOfCthulhuURP.unitypackage",
            @"C:\Dev\Forever engine\Assets\Hivemind\TownSmith\URP\TownSmithURP.unitypackage",
            @"C:\Dev\Forever engine\Assets\Hivemind\URP\MedievalDocks_URP.unitypackage",
            @"C:\Dev\Forever engine\Assets\Hivemind\URP\MedievalFantasyVillageURP.unitypackage",
            @"C:\Dev\Forever engine\Assets\Hivemind\URP\WarCampURP.unitypackage",
            @"C:\Dev\Forever engine\Assets\Hivemind\WitchHouse\URP\WitchHouseURP.unitypackage",
        };

        private static int _idx;
        private static int _failed;

        public static void RunAll()
        {
            Debug.Log($"[BatchPackageImporter] Starting — {_packages.Count} package(s) queued.");
            AssetDatabase.importPackageCompleted += OnCompleted;
            AssetDatabase.importPackageFailed += OnFailed;
            AssetDatabase.importPackageCancelled += OnCancelled;
            _idx = 0;
            _failed = 0;
            Next();
        }

        private static void Next()
        {
            if (_idx >= _packages.Count)
            {
                Debug.Log($"[BatchPackageImporter] DONE — {_packages.Count} attempted, {_failed} failed.");
                EditorApplication.Exit(_failed == 0 ? 0 : 1);
                return;
            }
            var path = _packages[_idx];
            _idx++;
            Debug.Log($"[BatchPackageImporter] [{_idx}/{_packages.Count}] Importing: {path}");
            AssetDatabase.ImportPackage(path, interactive: false);
        }

        private static void OnCompleted(string packageName)
        {
            Debug.Log($"[BatchPackageImporter] OK: {packageName}");
            EditorApplication.delayCall += Next;
        }

        private static void OnFailed(string packageName, string errorMessage)
        {
            Debug.LogError($"[BatchPackageImporter] FAIL: {packageName} — {errorMessage}");
            _failed++;
            EditorApplication.delayCall += Next;
        }

        private static void OnCancelled(string packageName)
        {
            Debug.LogWarning($"[BatchPackageImporter] CANCELLED: {packageName}");
            _failed++;
            EditorApplication.delayCall += Next;
        }
    }
}
