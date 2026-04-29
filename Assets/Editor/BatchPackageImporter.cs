// One-shot batchmode pack importer.
// Unity 6's command-line `-importPackage` flag silently ignores all but the
// first occurrence in a single invocation (Phase A diagnosis 2026-04-27).
// This editor script works around that by calling AssetDatabase.ImportPackage
// serially via the importPackageCompleted callback chain — one Unity invocation
// imports many packs.
//
// 2026-04-29 update: wrap the import loop with AssetDatabase.StartAssetEditing()
// / StopAssetEditing() to suppress mid-batch domain reloads. This is the same
// mechanism Asset Importer PRO (Apex Forge) uses internally — confirmed via
// DLL-string inspection. Without it, gaia Bug #46 vaporizes the
// importPackageCompleted callback after any pack triggers a script recompile,
// breaking the chain. With it, the AssetDatabase doesn't refresh between
// imports → no domain reload → callbacks stay live.
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
        // Active queue — edit and re-run for each batch.
        private static readonly List<string> _packages = new()
        {
            // 2026-04-29 PM — single missing pack from owned-asset audit:
            // WarCampURP.unitypackage (2.7 GB, nested URP variant of Medieval War Camp)
            // was extracted from the parent Hivemind pack via
            // scripts/extract_unitypackage.py. Now import the URP content.
            @"C:\Dev\Forever engine\Assets\Hivemind\URP\WarCampURP.unitypackage",
        };

        private static int _idx;
        private static int _failed;
        private static bool _editingStarted;

        public static void RunAll()
        {
            Debug.Log($"[BatchPackageImporter] Starting — {_packages.Count} package(s) queued.");
            AssetDatabase.importPackageCompleted += OnCompleted;
            AssetDatabase.importPackageFailed += OnFailed;
            AssetDatabase.importPackageCancelled += OnCancelled;
            _idx = 0;
            _failed = 0;
            // Suppress mid-batch domain reloads (gaia Bug #46 mitigation; AIP mechanism).
            AssetDatabase.StartAssetEditing();
            _editingStarted = true;
            Next();
        }

        private static void Next()
        {
            if (_idx >= _packages.Count)
            {
                Finish();
                return;
            }
            var path = _packages[_idx];
            _idx++;
            Debug.Log($"[BatchPackageImporter] [{_idx}/{_packages.Count}] Importing: {path}");
            AssetDatabase.ImportPackage(path, interactive: false);
        }

        private static void Finish()
        {
            if (_editingStarted)
            {
                AssetDatabase.StopAssetEditing();
                _editingStarted = false;
            }
            Debug.Log($"[BatchPackageImporter] DONE — {_packages.Count} attempted, {_failed} failed.");
            EditorApplication.Exit(_failed == 0 ? 0 : 1);
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
