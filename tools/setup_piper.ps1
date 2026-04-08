# Piper TTS bootstrap script for Forever Engine
#
# Downloads the Piper Windows binary and a set of voice models so the
# DialoguePanel's NPC narration uses neural TTS instead of robotic SAPI.
#
# Run from the Forever engine project root:
#     powershell -ExecutionPolicy Bypass -File tools/setup_piper.ps1
#
# Idempotent — skips files that already exist. Total download ~250 MB.
# Voices land in tools/piper/voices/, the binary in tools/piper/piper/.
# Both are .gitignored so checking in 250 MB of binaries doesn't happen.
#
# Required by: Assets/Scripts/Demo/UI/VoiceOutput.cs (Piper backend)

$ErrorActionPreference = "Stop"

$piperVersion = "2023.11.14-2"
$piperUrl = "https://github.com/rhasspy/piper/releases/download/$piperVersion/piper_windows_amd64.zip"
$baseDir = Join-Path $PSScriptRoot "piper"
$voicesDir = Join-Path $baseDir "voices"

New-Item -ItemType Directory -Force -Path $baseDir | Out-Null
New-Item -ItemType Directory -Force -Path $voicesDir | Out-Null

# 1. Piper binary
$piperExe = Join-Path $baseDir "piper\piper.exe"
if (Test-Path $piperExe) {
    Write-Host "[setup_piper] piper.exe already installed, skipping download"
} else {
    Write-Host "[setup_piper] downloading piper $piperVersion..."
    $zipPath = Join-Path $baseDir "piper.zip"
    Invoke-WebRequest -Uri $piperUrl -OutFile $zipPath
    Expand-Archive -Path $zipPath -DestinationPath $baseDir -Force
    Remove-Item $zipPath
    Write-Host "[setup_piper] piper installed at $piperExe"
}

# 2. Voice models. Each entry is (name, hf_path).
# en_US-lessac-medium = default narrator (clear American)
# en_US-ryan-medium  = Old Garth (mature American male)
# en_US-amy-medium   = Thalia (warm American female)
# en_GB-alan-medium  = Sir Aldric (formal British male)
$voices = @(
    @{ Name = "en_US-lessac-medium"; Path = "en/en_US/lessac/medium" },
    @{ Name = "en_US-ryan-medium";   Path = "en/en_US/ryan/medium" },
    @{ Name = "en_US-amy-medium";    Path = "en/en_US/amy/medium" },
    @{ Name = "en_GB-alan-medium";   Path = "en/en_GB/alan/medium" }
)

$hfBase = "https://huggingface.co/rhasspy/piper-voices/resolve/main"

foreach ($v in $voices) {
    $onnxPath = Join-Path $voicesDir "$($v.Name).onnx"
    $jsonPath = Join-Path $voicesDir "$($v.Name).onnx.json"

    if ((Test-Path $onnxPath) -and (Test-Path $jsonPath)) {
        Write-Host "[setup_piper] $($v.Name) already installed, skipping"
        continue
    }

    Write-Host "[setup_piper] downloading $($v.Name)..."
    Invoke-WebRequest -Uri "$hfBase/$($v.Path)/$($v.Name).onnx"      -OutFile $onnxPath
    Invoke-WebRequest -Uri "$hfBase/$($v.Path)/$($v.Name).onnx.json" -OutFile $jsonPath
}

Write-Host ""
Write-Host "[setup_piper] done. VoiceOutput.cs will auto-detect Piper at startup."
Write-Host "[setup_piper] To verify, in Unity check the console for 'Piper detected at ...'."
