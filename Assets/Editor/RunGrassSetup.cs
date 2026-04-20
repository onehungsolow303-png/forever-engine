using UnityEditor;
using UnityEngine;

public static class RunGrassSetup
{
    [MenuItem("Forever Engine/Grass: Create + Populate")]
    public static void Run()
    {
        CreateGrassConfigAsset.Create();
        AssetDatabase.Refresh();
        PopulateGrassConfig.Populate();
        Debug.Log("[RunGrassSetup] Done.");
    }
}
