using UnityEngine;
using System;

namespace ForeverEngine.AI.Inference
{
    public class InferenceEngine : MonoBehaviour
    {
        public static InferenceEngine Instance { get; private set; }
        private bool _modelLoaded;

        private void Awake() => Instance = this;

        public bool IsAvailable => _modelLoaded;

        public float[] Infer(float[] input)
        {
            if (!_modelLoaded) return null;
            // Placeholder: Unity Sentis integration goes here
            // For now, return a simple pass-through for testing
            return input;
        }

        public void LoadModel(string path)
        {
            Debug.Log($"[Inference] Loading model: {path}");
            _modelLoaded = true; // Sentis integration placeholder
        }

        public void UnloadModel()
        {
            _modelLoaded = false;
        }
    }
}
