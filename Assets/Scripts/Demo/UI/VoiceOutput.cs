using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

namespace ForeverEngine.Demo.UI
{
    /// <summary>
    /// Text-to-speech wrapper for NPC narration.
    ///
    /// Two backends, picked at runtime:
    ///   1. **Piper** (preferred) — neural TTS, distinct voice per NPC, much
    ///      higher quality than SAPI. Looks for `tools/piper/piper.exe` and
    ///      voice models under `tools/piper/voices/{name}.onnx` relative to
    ///      the project root. If found, this backend is used.
    ///   2. **Windows SAPI** (fallback) — robotic but zero-dependency. Used
    ///      when Piper isn't installed so the demo still has narration even
    ///      on a fresh checkout.
    ///
    /// DialoguePanel calls Speak(text, voiceModel) when an NPC response
    /// arrives. The voiceModel selects a Piper voice (e.g. "en_US-ryan-medium"
    /// for Old Garth). When using SAPI fallback, voiceModel is ignored.
    ///
    /// Pre-processing applied to both backends:
    ///   - *action descriptions in asterisks* are stripped (visual only)
    ///   - whitespace collapsed
    ///
    /// A new Speak() call cancels the previous one in flight, so back-to-
    /// back NPC turns don't talk over each other.
    ///
    /// To install Piper, run `tools/setup_piper.ps1`. Without it, the demo
    /// quietly falls back to SAPI.
    /// </summary>
    public class VoiceOutput : UnityEngine.MonoBehaviour
    {
        public static VoiceOutput Instance { get; private set; }

        /// <summary>If false, Speak() returns immediately without doing anything.</summary>
        public bool MuteEnabled { get; set; } = false;

        public bool IsAvailable { get; private set; }

        /// <summary>True when Piper backend is wired up. False when falling back to SAPI.</summary>
        public bool UsingPiper { get; private set; }

        // The default voice for narration when no specific NPC voice is provided
        // (e.g. system messages or NPCs without a VoiceModel mapping).
        public const string DefaultVoiceModel = "en_US-lessac-medium";

        // Cached, resolved Piper paths. Set in Awake; null when Piper isn't installed.
        private string _piperExePath;
        private string _piperVoicesDir;

        // Synth process (piper.exe). Killed by StopSpeaking() so a new
        // Speak() call doesn't pile up audio.
        private Process _synthProcess;

        // Player process (PowerShell SoundPlayer or PowerShell SAPI). Killed
        // by StopSpeaking() so playback halts when the player closes the dialog
        // or sends a new line.
        private Process _playerProcess;

        // Temp WAV file we wrote for the current playback. Cleaned up when
        // playback ends or another StopSpeaking() / Speak() interrupts.
        private string _currentWavFile;
        private string _currentTempTextFile;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            IsAvailable = true;
            ResolvePiperPaths();
#else
            IsAvailable = false;
#endif
        }

        /// <summary>
        /// Look for Piper at the conventional `tools/piper/piper.exe` location
        /// relative to the project root. Application.dataPath in the editor
        /// points at Assets/, so go up one. In a player build dataPath points
        /// at the build's _Data folder; piper would have to ship alongside
        /// the executable in that case (StreamingAssets is the right answer
        /// long-term, but for the editor playtest workflow this is enough).
        /// </summary>
        private void ResolvePiperPaths()
        {
            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string exePath = Path.Combine(projectRoot, "tools", "piper", "piper", "piper.exe");
                string voicesDir = Path.Combine(projectRoot, "tools", "piper", "voices");

                if (File.Exists(exePath) && Directory.Exists(voicesDir))
                {
                    _piperExePath = exePath;
                    _piperVoicesDir = voicesDir;
                    UsingPiper = true;
                    UnityEngine.Debug.Log($"[VoiceOutput] Piper detected at {exePath}");
                }
                else
                {
                    UsingPiper = false;
                    UnityEngine.Debug.Log("[VoiceOutput] Piper not installed; falling back to Windows SAPI");
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning($"[VoiceOutput] Piper detection failed: {e.Message}");
                UsingPiper = false;
            }
        }

        /// <summary>
        /// Speak text via the active backend. voiceModel selects a Piper voice
        /// (the .onnx file basename, no extension); ignored when using SAPI.
        /// Pass null/empty to use the default narrator voice.
        /// </summary>
        public void Speak(string text, string voiceModel = null)
        {
            if (!IsAvailable || MuteEnabled || string.IsNullOrEmpty(text)) return;

            // Strip *action descriptions* in asterisks. Those are stage
            // directions for the visual narrative, not lines the NPC says.
            string spoken = System.Text.RegularExpressions.Regex.Replace(text, @"\*[^*]+\*", " ");
            // Collapse repeated whitespace
            spoken = System.Text.RegularExpressions.Regex.Replace(spoken, @"\s+", " ").Trim();
            if (string.IsNullOrEmpty(spoken)) return;

            // Cancel any in-flight playback so back-to-back NPC turns don't overlap
            StopSpeaking();

            if (UsingPiper)
                SpeakWithPiper(spoken, voiceModel);
            else
                SpeakWithSapi(spoken);
        }

        // Convenience overload — old call sites that pass only the text keep
        // working without picking a voice. Routes to the default narrator.
        public void Speak(string text) => Speak(text, null);

        // ── Piper backend ─────────────────────────────────────────────────

        private void SpeakWithPiper(string spoken, string voiceModel)
        {
            // Pick the voice file. If the requested one doesn't exist on
            // disk we fall back to the default narrator rather than crashing.
            string requested = string.IsNullOrEmpty(voiceModel) ? DefaultVoiceModel : voiceModel;
            string voicePath = Path.Combine(_piperVoicesDir, requested + ".onnx");
            if (!File.Exists(voicePath))
            {
                UnityEngine.Debug.LogWarning(
                    $"[VoiceOutput] Voice '{requested}' not installed, using default");
                voicePath = Path.Combine(_piperVoicesDir, DefaultVoiceModel + ".onnx");
                if (!File.Exists(voicePath))
                {
                    UnityEngine.Debug.LogWarning(
                        $"[VoiceOutput] Default voice missing too — falling back to SAPI");
                    SpeakWithSapi(spoken);
                    return;
                }
            }

            try
            {
                // Output WAV path. We hand it to piper via --output_file and
                // then to the SoundPlayer for playback. Cleaned up by
                // CleanupTempFiles() once playback ends.
                _currentWavFile = Path.Combine(
                    Path.GetTempPath(),
                    $"fe_tts_{System.Guid.NewGuid():N}.wav");

                var psi = new ProcessStartInfo
                {
                    FileName = _piperExePath,
                    Arguments = $"--model \"{voicePath}\" --output_file \"{_currentWavFile}\" --quiet",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                _synthProcess = Process.Start(psi);
                // Pipe the text in via stdin, then close stdin so piper
                // knows there's nothing more coming.
                _synthProcess.StandardInput.Write(spoken);
                _synthProcess.StandardInput.Close();

                // Hook the Exited event so we kick off playback as soon as
                // synthesis finishes. Without EnableRaisingEvents the Exited
                // event never fires.
                _synthProcess.EnableRaisingEvents = true;
                _synthProcess.Exited += (s, e) =>
                {
                    // Bounce back to main thread by checking on next frame —
                    // but Process.Exited fires on a thread-pool thread and
                    // Unity's main-thread requirement is for AudioSource etc.
                    // Process.Start is fine off-thread, so we can launch the
                    // player here directly.
                    if (File.Exists(_currentWavFile))
                        StartWavPlayer(_currentWavFile);
                };
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning($"[VoiceOutput] piper failed: {e.Message}");
                CleanupTempFiles();
                // If piper itself blew up, try SAPI as a last resort so the
                // player still hears something this turn.
                SpeakWithSapi(spoken);
            }
        }

        /// <summary>
        /// Spawn a tiny PowerShell process to play the WAV via System.Media.SoundPlayer.
        /// Async (PlaySync runs in the child process and we kill the child to interrupt).
        /// </summary>
        private void StartWavPlayer(string wavPath)
        {
            try
            {
                // PlaySync blocks the PowerShell process for the WAV duration,
                // which is exactly what we want — killing _playerProcess
                // immediately stops audio.
                string playerCmd =
                    $"$p = New-Object Media.SoundPlayer '{wavPath}'; " +
                    "$p.PlaySync(); " +
                    $"Remove-Item '{wavPath}' -ErrorAction SilentlyContinue";

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{playerCmd}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                _playerProcess = Process.Start(psi);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning($"[VoiceOutput] WAV playback failed: {e.Message}");
                CleanupTempFiles();
            }
        }

        // ── SAPI fallback backend ─────────────────────────────────────────

        private void SpeakWithSapi(string spoken)
        {
            try
            {
                _currentTempTextFile = Path.Combine(
                    Path.GetTempPath(),
                    $"fe_tts_{System.Guid.NewGuid():N}.txt");
                File.WriteAllText(_currentTempTextFile, spoken, Encoding.UTF8);

                string psCommand =
                    "Add-Type -AssemblyName System.Speech; " +
                    "$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer; " +
                    "$synth.Rate = 0; " +
                    $"$text = [System.IO.File]::ReadAllText('{_currentTempTextFile}', " +
                    "[System.Text.Encoding]::UTF8); " +
                    "$synth.Speak($text); " +
                    $"Remove-Item '{_currentTempTextFile}' -ErrorAction SilentlyContinue";

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCommand}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                _playerProcess = Process.Start(psi);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning($"[VoiceOutput] SAPI speak failed: {e.Message}");
                CleanupTempFiles();
            }
        }

        // ── Common ────────────────────────────────────────────────────────

        public void StopSpeaking()
        {
            // Kill the synth first so it doesn't write a stale WAV after we
            // delete the file path it was given.
            try
            {
                if (_synthProcess != null && !_synthProcess.HasExited)
                    _synthProcess.Kill();
            }
            catch { /* swallow */ }
            _synthProcess = null;

            try
            {
                if (_playerProcess != null && !_playerProcess.HasExited)
                    _playerProcess.Kill();
            }
            catch { /* swallow */ }
            _playerProcess = null;

            CleanupTempFiles();
        }

        public void ToggleMute()
        {
            MuteEnabled = !MuteEnabled;
            if (MuteEnabled) StopSpeaking();
        }

        private void CleanupTempFiles()
        {
            if (!string.IsNullOrEmpty(_currentWavFile))
            {
                try { if (File.Exists(_currentWavFile)) File.Delete(_currentWavFile); } catch { }
                _currentWavFile = null;
            }
            if (!string.IsNullOrEmpty(_currentTempTextFile))
            {
                try { if (File.Exists(_currentTempTextFile)) File.Delete(_currentTempTextFile); } catch { }
                _currentTempTextFile = null;
            }
        }

        private void OnDestroy()
        {
            StopSpeaking();
        }
    }
}
