using UnityEngine;
using Unity.InferenceEngine;
using System;
using System.IO;

namespace ForeverEngine.AI.Inference
{
    /// <summary>
    /// Singleton MonoBehaviour that wraps Unity Sentis (Inference Engine 2.5)
    /// for ONNX model loading and GPU-accelerated neural network inference.
    /// </summary>
    public class InferenceEngine : UnityEngine.MonoBehaviour
    {
        public static InferenceEngine Instance { get; private set; }

        [SerializeField]
        [Tooltip("Backend used for inference. GPUCompute is fastest when available.")]
        private BackendType _backend = BackendType.GPUCompute;

        private Model _model;
        private Worker _worker;
        private bool _modelLoaded;

        /// <summary>
        /// Optional reference to a ModelAsset dragged into the Inspector.
        /// If set, the model loads automatically on Awake (before LoadModel calls).
        /// </summary>
        [SerializeField]
        [Tooltip("Optional: drag a .onnx/.sentis asset here to auto-load on Awake.")]
        private ModelAsset _modelAsset;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Auto-load from inspector-assigned asset if present
            if (_modelAsset != null)
            {
                try
                {
                    _model = ModelLoader.Load(_modelAsset);
                    _worker = new Worker(_model, _backend);
                    _modelLoaded = true;
                    Debug.Log($"[InferenceEngine] Auto-loaded ModelAsset (backend: {_backend})");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[InferenceEngine] Failed to auto-load ModelAsset: {e.Message}");
                    _modelLoaded = false;
                }
            }
        }

        /// <summary>
        /// True when a model is loaded and the worker is ready for inference.
        /// </summary>
        public bool IsAvailable => _modelLoaded && _worker != null;

        /// <summary>
        /// Loads a .sentis model from StreamingAssets by relative path.
        /// Example: LoadModel("models/combat_brain.sentis")
        /// </summary>
        public void LoadModel(string path)
        {
            UnloadModel();
            try
            {
                string fullPath = Path.Combine(Application.streamingAssetsPath, path);
                if (!File.Exists(fullPath))
                {
                    Debug.LogWarning($"[InferenceEngine] Model file not found: {fullPath}");
                    return;
                }

                _model = ModelLoader.Load(fullPath);
                if (_model == null)
                {
                    Debug.LogError($"[InferenceEngine] ModelLoader.Load returned null for: {path}");
                    return;
                }

                _worker = new Worker(_model, _backend);
                _modelLoaded = true;
                Debug.Log($"[InferenceEngine] Model loaded: {path} (backend: {_backend}, " +
                          $"inputs: {_model.inputs.Count}, outputs: {_model.outputs.Count})");
            }
            catch (Exception e)
            {
                Debug.LogError($"[InferenceEngine] Failed to load model '{path}': {e.Message}");
                _modelLoaded = false;
            }
        }

        /// <summary>
        /// Loads a model from a ModelAsset ScriptableObject (e.g. imported .onnx).
        /// </summary>
        public void LoadModel(ModelAsset asset)
        {
            UnloadModel();
            try
            {
                _model = ModelLoader.Load(asset);
                if (_model == null)
                {
                    Debug.LogError("[InferenceEngine] ModelLoader.Load returned null for ModelAsset");
                    return;
                }

                _worker = new Worker(_model, _backend);
                _modelLoaded = true;
                Debug.Log($"[InferenceEngine] ModelAsset loaded (backend: {_backend}, " +
                          $"inputs: {_model.inputs.Count}, outputs: {_model.outputs.Count})");
            }
            catch (Exception e)
            {
                Debug.LogError($"[InferenceEngine] Failed to load ModelAsset: {e.Message}");
                _modelLoaded = false;
            }
        }

        /// <summary>
        /// Runs synchronous inference on a flat float array.
        /// Returns the model output as float[], or the input as-is if unavailable.
        /// </summary>
        public float[] Infer(float[] input)
        {
            if (!IsAvailable || input == null || input.Length == 0)
                return input;

            try
            {
                // Create input tensor — shape (1, inputLength) for batch-of-1
                using var inputTensor = new Tensor<float>(new TensorShape(1, input.Length), input);

                // Schedule non-blocking inference then immediately read back (sync path)
                _worker.Schedule(inputTensor);

                // Peek at the default (first) output
                var outputRef = _worker.PeekOutput() as Tensor<float>;
                if (outputRef == null)
                {
                    Debug.LogWarning("[InferenceEngine] Output tensor was null or not Tensor<float>");
                    return input;
                }

                // ReadbackAndClone copies GPU data to CPU so we can read it
                using var cpuOutput = outputRef.ReadbackAndClone();
                return cpuOutput.DownloadToArray();
            }
            catch (Exception e)
            {
                Debug.LogError($"[InferenceEngine] Inference failed: {e.Message}");
                return input;
            }
        }

        /// <summary>
        /// Disposes the worker and releases the model.
        /// </summary>
        public void UnloadModel()
        {
            _worker?.Dispose();
            _worker = null;
            _model = null;
            _modelLoaded = false;
        }

        private void OnDestroy()
        {
            UnloadModel();
            if (Instance == this) Instance = null;
        }
    }
}
