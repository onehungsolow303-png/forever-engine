using System.Collections;
using UnityEngine;
using ForeverEngine.MonoBehaviour.Camera;

namespace ForeverEngine.Demo.Overworld
{
    /// <summary>
    /// Bootstrapper for the Overworld3D scene. Placed on a GameObject in the scene,
    /// it wires OverworldManager (game logic) to the 3D renderer and perspective camera.
    /// Waits for OverworldManager to finish generating tiles before initializing.
    /// </summary>
    public class Overworld3DSetup : UnityEngine.MonoBehaviour
    {
        [SerializeField] private OverworldPrefabMapper _prefabMap;

        private IEnumerator Start()
        {
            var om = OverworldManager.Instance;
            if (om == null)
            {
                Debug.LogError("[Overworld3DSetup] No OverworldManager found!");
                yield break;
            }

            // Wait for OverworldManager.Start() to finish generating tiles
            while (om.Tiles == null)
                yield return null;

            // Find or create the 3D renderer
            var renderer3D = FindAnyObjectByType<Overworld3DRenderer>();
            if (renderer3D == null)
            {
                var go = new GameObject("Overworld3DRenderer");
                renderer3D = go.AddComponent<Overworld3DRenderer>();
            }

            // Load prefab map — try serialized field first, then Resources
            if (_prefabMap == null)
                _prefabMap = Resources.Load<OverworldPrefabMapper>("OverworldPrefabMap");
            if (_prefabMap != null)
                Debug.Log($"[Overworld3DSetup] Loaded prefab map with {_prefabMap.ForestScatter?.Length ?? 0} forest scatter prefabs");
            else
                Debug.LogWarning("[Overworld3DSetup] No OverworldPrefabMap found — run Forever Engine > Populate Overworld Prefabs");

            renderer3D.SetPrefabMap(_prefabMap);
            renderer3D.Initialize(om.Tiles);

            // Wire the perspective camera to follow the player model
            var camCtrl = FindAnyObjectByType<PerspectiveCameraController>();
            if (camCtrl != null && renderer3D.PlayerTransform != null)
            {
                camCtrl.FollowTarget = renderer3D.PlayerTransform;
                camCtrl.SnapToTarget();
            }
            else if (camCtrl == null)
            {
                Debug.LogWarning("[Overworld3DSetup] No PerspectiveCameraController in scene.");
            }

            // Register the 3D renderer on OverworldManager so Update() uses it
            om.Set3DRenderer(renderer3D);

            // Bootstrap atmosphere (post-processing, skybox, fog, lighting)
            if (FindAnyObjectByType<AtmosphereSetup>() == null)
            {
                var atmosGO = new GameObject("Atmosphere");
                atmosGO.AddComponent<AtmosphereSetup>();
            }

            Debug.Log("[Overworld3DSetup] 3D overworld wired successfully.");
        }
    }
}
