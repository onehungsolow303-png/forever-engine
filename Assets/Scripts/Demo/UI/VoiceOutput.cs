using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

namespace ForeverEngine.Demo.UI
{
    /// <summary>
    /// Text-to-speech wrapper that shells out to PowerShell + System.Speech
    /// for NPC narration. Plays through the default Windows audio device.
    /// Zero external dependencies — uses only what ships with Windows 10+.
    ///
    /// DialoguePanel calls Speak(text) when an NPC response arrives. The
    /// text is pre-processed:
    ///   - *action descriptions in asterisks* are stripped (visual only)
    ///   - the result is written to a temp file (no shell-escaping hell)
    ///   - PowerShell loads the file and feeds it to SpeechSynthesizer
    ///
    /// A new Speak() call cancels the previous one in flight, so back-to-
    /// back NPC turns don't talk over each other.
    ///
    /// Voice quality is whatever Windows SAPI provides — robotic by default.
    /// Once verified, this can be swapped to a higher-quality engine (Piper,
    /// ElevenLabs, OpenAI TTS) without changing the integration architecture
    /// — just change what Speak() does internally.
    /// </summary>
    public class VoiceOutput : UnityEngine.MonoBehaviour
    {
        public static VoiceOutput Instance { get; private set; }

        /// <summary>If false, Speak() returns immediately without doing anything.</summary>
        public bool MuteEnabled { get; set; } = false;

        public bool IsAvailable { get; private set; }

        private Process _currentProcess;
        private string _currentTempFile;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            IsAvailable = true;
#else
            IsAvailable = false;
#endif
        }

        public void Speak(string text)
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

            try
            {
                // Write text to a temp file so we don't have to escape it
                // through PowerShell's command-line quoting.
                _currentTempFile = Path.Combine(
                    Path.GetTempPath(),
                    $"fe_tts_{System.Guid.NewGuid():N}.txt");
                File.WriteAllText(_currentTempFile, spoken, Encoding.UTF8);

                // PowerShell command: load System.Speech, read the temp
                // file as UTF-8, speak it synchronously, then delete the
                // file. Rate 0 = normal speed; -2 = slower; +2 = faster.
                string psCommand =
                    "Add-Type -AssemblyName System.Speech; " +
                    "$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer; " +
                    "$synth.Rate = 0; " +
                    $"$text = [System.IO.File]::ReadAllText('{_currentTempFile}', " +
                    "[System.Text.Encoding]::UTF8); " +
                    "$synth.Speak($text); " +
                    $"Remove-Item '{_currentTempFile}' -ErrorAction SilentlyContinue";

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
                _currentProcess = Process.Start(psi);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning($"[VoiceOutput] speak failed: {e.Message}");
                CleanupTempFile();
            }
        }

        public void StopSpeaking()
        {
            try
            {
                if (_currentProcess != null && !_currentProcess.HasExited)
                    _currentProcess.Kill();
            }
            catch { /* swallow */ }
            _currentProcess = null;
            CleanupTempFile();
        }

        public void ToggleMute()
        {
            MuteEnabled = !MuteEnabled;
            if (MuteEnabled) StopSpeaking();
        }

        private void CleanupTempFile()
        {
            if (string.IsNullOrEmpty(_currentTempFile)) return;
            try { if (File.Exists(_currentTempFile)) File.Delete(_currentTempFile); } catch { }
            _currentTempFile = null;
        }

        private void OnDestroy()
        {
            StopSpeaking();
        }
    }
}
