using UnityEngine;
using System.Collections.Generic;

namespace ForeverEngine.AI.Director
{
    [CreateAssetMenu(menuName = "Forever Engine/Director Config")]
    public class DirectorConfig : ScriptableObject
    {
        public List<DirectorAction> PossibleActions = new();
        public float MinActionThreshold = 0.3f;
        public float MaxIntensityDuration = 180f;
        public float MinCalmBeforeEvent = 30f;
    }

    [System.Serializable]
    public class DirectorAction
    {
        public string Id;
        public float PacingWeight = 1f;
        public float DramaWeight = 1f;
        public float TimeWeight = 0.5f;
        public float TargetIntensity = 0.7f;
        public float Cooldown = 60f;
        [System.NonSerialized] public float LastUsedTime = -999f;
        [System.NonSerialized] public System.Action OnExecute;

        public bool IsReady(float currentTime) => currentTime - LastUsedTime >= Cooldown;
    }
}
