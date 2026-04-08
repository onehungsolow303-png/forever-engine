#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ForeverEngine.EditorTools
{
    /// <summary>
    /// Auto-creates Assets/Resources/DialoguePanelSettings.asset with the
    /// Unity Default Runtime Theme attached, so DialoguePanel.cs can load
    /// it via Resources.Load at runtime instead of falling back to a
    /// programmatic instance with no theme stylesheet (which renders the
    /// panel as a 1-pixel-tall black bar because the built-in
    /// Label/Button/ScrollView styles aren't applied).
    ///
    /// Runs once on Editor load via [InitializeOnLoad], skips if the asset
    /// already exists. Also exposes a menu item so the user can recreate
    /// it manually if needed.
    ///
    /// Spec: dialogue UI rendering fix from the playtest finding "panel
    /// shows but UI is out of view / collapsed."
    /// </summary>
    [InitializeOnLoad]
    public static class DialoguePanelSettingsCreator
    {
        private const string AssetPath = "Assets/Resources/DialoguePanelSettings.asset";

        static DialoguePanelSettingsCreator()
        {
            // Direct call instead of EditorApplication.delayCall because
            // delayCall doesn't fire in batchmode -executeMethod runs
            // (Unity exits before the next editor tick). EnsureExists
            // wraps everything in a try/catch so an early-init failure
            // doesn't break the editor or the batchmode test runner.
            try
            {
                EnsureExists();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning(
                    $"[DialoguePanelSettingsCreator] could not run on init: {e.Message}");
            }
        }

        [MenuItem("Forever Engine/Recreate DialoguePanelSettings")]
        public static void RecreateMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<PanelSettings>(AssetPath) != null)
            {
                AssetDatabase.DeleteAsset(AssetPath);
            }
            EnsureExists();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<PanelSettings>(AssetPath);
        }

        private static void EnsureExists()
        {
            if (AssetDatabase.LoadAssetAtPath<PanelSettings>(AssetPath) != null)
            {
                return; // already created
            }

            // Make sure Resources/ exists
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ConstantPhysicalSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.sortingOrder = 100;

            // Find the Unity Default Runtime Theme TSS. It ships as a built-in
            // Unity asset under the UI Toolkit module. Try a few known paths,
            // then fall back to scanning for ANY ThemeStyleSheet in the project.
            var theme = TryLoadDefaultTheme();
            if (theme != null)
            {
                settings.themeStyleSheet = theme;
            }
            else
            {
                Debug.LogWarning(
                    "[DialoguePanelSettingsCreator] could not locate Unity Default Runtime Theme; " +
                    "the asset is being created without a theme stylesheet. UI elements may " +
                    "render with default styles. Manual fix: open the asset in Inspector and " +
                    "drag the default theme into the Theme Style Sheet field.");
            }

            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[DialoguePanelSettingsCreator] created {AssetPath}" +
                      (theme != null ? $" with theme {theme.name}" : " without theme"));
        }

        private static ThemeStyleSheet TryLoadDefaultTheme()
        {
            // Path 1: the canonical Unity built-in theme path used in 2022+
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(
                "Packages/com.unity.ui/PackageResources/StyleSheets/Default/UnityDefaultRuntimeTheme.tss"
            );
            if (theme != null) return theme;

            // Path 2: alternate location used in some Unity 6 layouts
            theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(
                "Packages/com.unity.modules.uielements/UIElementsPackageResources/StyleSheets/Default/UnityDefaultRuntimeTheme.tss"
            );
            if (theme != null) return theme;

            // Path 3: scan ALL ThemeStyleSheet assets in the project. Picks
            // the first one whose name matches "Default" or "Runtime", or
            // any TSS at all as a last resort.
            var guids = AssetDatabase.FindAssets("t:ThemeStyleSheet");
            ThemeStyleSheet anyTheme = null;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var t = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(path);
                if (t == null) continue;
                if (anyTheme == null) anyTheme = t;
                if (path.Contains("UnityDefaultRuntimeTheme") || path.Contains("Default")) return t;
            }
            return anyTheme;
        }
    }
}
#endif
