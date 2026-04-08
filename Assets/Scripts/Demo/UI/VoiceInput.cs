using UnityEngine;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using UnityEngine.Windows.Speech;
#endif

namespace ForeverEngine.Demo.UI
{
    /// <summary>
    /// Speech-to-text wrapper around Unity's Windows DictationRecognizer.
    ///
    /// Used by DialoguePanel's mic button: click to start dictating, click
    /// again to stop. Recognized text gets pushed into the dialogue input
    /// field as the user speaks (partial results) and finalized when the
    /// user pauses long enough for the recognizer to commit a phrase.
    ///
    /// Windows-only. On non-Windows builds (or builds where the Windows
    /// Speech feature isn't installed), the methods become no-ops and the
    /// IsAvailable flag stays false so the UI can hide the mic button.
    ///
    /// Setup: requires Windows 10+ with the Speech features installed
    /// (most Windows installs have them by default). The first invocation
    /// will prompt the user for microphone permission via the Windows
    /// privacy dialog if they haven't granted it before.
    /// </summary>
    public class VoiceInput : UnityEngine.MonoBehaviour
    {
        public static VoiceInput Instance { get; private set; }

        public bool IsAvailable { get; private set; }
        public bool IsListening { get; private set; }

        /// <summary>Fires for in-progress hypotheses while the user is speaking.</summary>
        public event System.Action<string> OnPartialResult;

        /// <summary>Fires when a phrase is committed (user paused).</summary>
        public event System.Action<string> OnFinalResult;

        /// <summary>Fires on initialization or runtime errors. The string is human-readable.</summary>
        public event System.Action<string> OnError;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private DictationRecognizer _recognizer;
#endif

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            // We don't construct the DictationRecognizer here — that allocates
            // OS resources and prompts for mic permission. We construct it
            // lazily on the first StartListening() call so the cost is only
            // paid by users who actually click the mic button.
            IsAvailable = true;
#else
            IsAvailable = false;
#endif
        }

        public void StartListening()
        {
            if (IsListening) return;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                if (_recognizer == null)
                {
                    _recognizer = new DictationRecognizer();
                    _recognizer.DictationResult += HandleResult;
                    _recognizer.DictationHypothesis += HandleHypothesis;
                    _recognizer.DictationError += HandleError;
                    _recognizer.DictationComplete += HandleComplete;
                    // Auto-silence detection: stop after this many seconds
                    // of silence. Default is 20s which is too long for chat.
                    _recognizer.AutoSilenceTimeoutSeconds = 5f;
                    _recognizer.InitialSilenceTimeoutSeconds = 8f;
                }
                _recognizer.Start();
                IsListening = true;
                Debug.Log("[VoiceInput] listening");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[VoiceInput] failed to start: {e.Message}");
                OnError?.Invoke(e.Message);
                Cleanup();
            }
#else
            OnError?.Invoke("voice input unavailable on this platform");
#endif
        }

        public void StopListening()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (_recognizer != null && _recognizer.Status == SpeechSystemStatus.Running)
            {
                try { _recognizer.Stop(); } catch (System.Exception e) { Debug.LogWarning($"[VoiceInput] stop: {e.Message}"); }
            }
            IsListening = false;
#endif
        }

        public void Toggle()
        {
            if (IsListening) StopListening();
            else StartListening();
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private void HandleResult(string text, ConfidenceLevel confidence)
        {
            OnFinalResult?.Invoke(text);
        }

        private void HandleHypothesis(string text)
        {
            OnPartialResult?.Invoke(text);
        }

        private void HandleError(string message, int code)
        {
            Debug.LogWarning($"[VoiceInput] error {code}: {message}");
            OnError?.Invoke(message);
            Cleanup();
        }

        private void HandleComplete(DictationCompletionCause cause)
        {
            // Silence timeout / network failure / user stopped — all end here.
            if (cause != DictationCompletionCause.Complete && cause != DictationCompletionCause.PauseLimitExceeded)
                Debug.Log($"[VoiceInput] dictation ended: {cause}");
            IsListening = false;
        }

        private void Cleanup()
        {
            if (_recognizer != null)
            {
                try
                {
                    _recognizer.DictationResult -= HandleResult;
                    _recognizer.DictationHypothesis -= HandleHypothesis;
                    _recognizer.DictationError -= HandleError;
                    _recognizer.DictationComplete -= HandleComplete;
                    _recognizer.Dispose();
                }
                catch { /* swallow */ }
                _recognizer = null;
            }
            IsListening = false;
        }
#endif

        private void OnDestroy()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            Cleanup();
#endif
        }
    }
}
