#Requires -Version 5.1
<#
.SYNOPSIS
    Deploys the locally-baked props.bin + related macro files to the 5950X
    server and restarts the scheduled task. Replaces the ad-hoc
    scp/ssh/curl chain we've been copy-pasting.

.DESCRIPTION
    Usage: .\scripts\deploy_props_bin.ps1
    Optional:
        -Server 192.168.1.110
        -User bp303
        -LocalRoot "C:/Dev/.shared/baked/planet/layer_0"
        -RemoteRoot "C:/Dev/.shared/baked/planet/layer_0"
        -HealthUrl "http://192.168.1.110:7901/health"
        -TaskName "ForeverEngineServer"

    Exits non-zero on any step failure. No silent partials.

    Every step is idempotent -- re-running after a transient failure is safe.
#>

[CmdletBinding()]
param(
    [string]$Server     = "192.168.1.110",
    [string]$User       = "bp303",
    [string]$LocalRoot  = "C:/Dev/.shared/baked/planet/layer_0",
    [string]$RemoteRoot = "C:/Dev/.shared/baked/planet/layer_0",
    [string]$HealthUrl  = "http://192.168.1.110:7901/health",
    [string]$TaskName   = "ForeverEngineServer",
    [string]$KeyPath    = "$env:USERPROFILE/.ssh/id_ed25519_5950x",
    [int]$HealthTimeoutSec = 30,
    [int]$FreshUptimeThresholdSec = 60
)

# SSH/SCP wrappers. Use -i $KeyPath when the key exists on disk; the Windows
# ssh-agent service is often disabled, so relying on agent-loaded keys is
# unreliable.
$sshArgs = @()
$scpArgs = @()
if (Test-Path $KeyPath) {
    $sshArgs += @("-i", $KeyPath)
    $scpArgs += @("-i", $KeyPath)
} else {
    Write-Host "[deploy_props_bin]   (no key at $KeyPath -- relying on ssh-agent)"
}
$sshArgs += @("-o", "BatchMode=yes", "-o", "ConnectTimeout=10")
$scpArgs += @("-o", "BatchMode=yes", "-o", "ConnectTimeout=10", "-r", "-p")

function Invoke-Ssh([string[]]$cmdArgs) {
    & ssh @sshArgs "$User@$Server" @cmdArgs
    return $LASTEXITCODE
}

$ErrorActionPreference = "Stop"
$startStamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
Write-Host "[deploy_props_bin] ===== $startStamp =====" -ForegroundColor Cyan

# ---------------------------------------------------------------------------
# Step 1: Validate local artifacts.
# ---------------------------------------------------------------------------
function Assert-LocalArtifactsReady {
    Write-Host "[deploy_props_bin] Step 1/5: validating local artifacts"
    if (-not (Test-Path $LocalRoot)) {
        throw "[deploy_props_bin] Local baked root missing: $LocalRoot. Run FullPipelineRebuild first."
    }

    $indexPath = Join-Path $LocalRoot "index.json"
    if (-not (Test-Path $indexPath)) {
        throw "[deploy_props_bin] Local index.json missing at $indexPath. Bake didn't complete."
    }

    $propsFiles = Get-ChildItem -Path $LocalRoot -Recurse -Filter "props.bin"
    if ($propsFiles.Count -eq 0) {
        throw "[deploy_props_bin] No props.bin files under $LocalRoot. Bake produced nothing."
    }

    $totalBytes = ($propsFiles | Measure-Object -Sum Length).Sum
    Write-Host "[deploy_props_bin]   found $($propsFiles.Count) props.bin, $([int]($totalBytes / 1024)) KiB"
    return $totalBytes
}

# ---------------------------------------------------------------------------
# Step 2: scp to 5950X. We use scp -r with the raw Windows path -- the remote
# side also runs Windows so forward-slash paths work in C:/ notation.
# ---------------------------------------------------------------------------
function Copy-ToServer {
    Write-Host "[deploy_props_bin] Step 2/5: scp to $User@$Server"

    # Ensure remote parent directory exists. ssh + mkdir -p is idempotent.
    $remoteParent = Split-Path $RemoteRoot -Parent
    $mkdirCmd = "if not exist `"$remoteParent`" mkdir `"$remoteParent`""
    & ssh @sshArgs "$User@$Server" "cmd /c $mkdirCmd"
    if ($LASTEXITCODE -ne 0) {
        throw "[deploy_props_bin] Remote mkdir failed (exit $LASTEXITCODE)."
    }

    # scp the entire layer_0 directory. -r recursive, -p preserves mtime so
    # integrity checks by timestamp work.
    $localSpec = $LocalRoot.TrimEnd('/', '\')
    $remoteParent2 = (Split-Path $RemoteRoot -Parent).Replace('\', '/')
    $remoteSpec = "${User}@${Server}:$remoteParent2"
    & scp @scpArgs $localSpec $remoteSpec
    if ($LASTEXITCODE -ne 0) {
        throw "[deploy_props_bin] scp failed (exit $LASTEXITCODE)."
    }
    Write-Host "[deploy_props_bin]   scp done"
}

# ---------------------------------------------------------------------------
# Step 3: verify remote size matches local.
# ---------------------------------------------------------------------------
function Assert-RemoteSizeMatches([long]$expectedBytes) {
    Write-Host "[deploy_props_bin] Step 3/5: verifying remote size"

    # PowerShell over ssh via -EncodedCommand to bypass quote-mangling through
    # the ssh + cmd layer. Sum sizes of all remote props.bin files.
    $inner = "(Get-ChildItem -Path '$RemoteRoot' -Recurse -Filter 'props.bin' | Measure-Object -Sum Length).Sum"
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($inner))
    $remoteCmd = "powershell -NoProfile -EncodedCommand $encoded"
    $remoteOut = & ssh @sshArgs "$User@$Server" $remoteCmd
    if ($LASTEXITCODE -ne 0) {
        throw "[deploy_props_bin] Remote size check failed (exit $LASTEXITCODE)."
    }

    $remoteBytes = [long]($remoteOut.Trim())
    if ($remoteBytes -ne $expectedBytes) {
        throw "[deploy_props_bin] Size mismatch: local=$expectedBytes remote=$remoteBytes. scp incomplete."
    }
    Write-Host "[deploy_props_bin]   remote props.bin total = $remoteBytes bytes (match)"
}

# ---------------------------------------------------------------------------
# Step 4: restart scheduled task.
# ---------------------------------------------------------------------------
function Restart-ServerTask {
    Write-Host "[deploy_props_bin] Step 4/5: restarting scheduled task '$TaskName'"
    # schtasks /End tolerates already-stopped (returns 0 or "task not running").
    & ssh @sshArgs "$User@$Server" "schtasks /End /TN `"$TaskName`""
    # Don't hard-fail if End returned non-zero -- task may not have been running.
    & ssh @sshArgs "$User@$Server" "schtasks /Run /TN `"$TaskName`""
    if ($LASTEXITCODE -ne 0) {
        throw "[deploy_props_bin] schtasks /Run failed (exit $LASTEXITCODE)."
    }
    Write-Host "[deploy_props_bin]   schtasks /Run issued"
}

# ---------------------------------------------------------------------------
# Step 5: poll /health until uptime reports fresh.
# ---------------------------------------------------------------------------
function Wait-ForFreshHealth {
    Write-Host "[deploy_props_bin] Step 5/5: polling $HealthUrl for fresh uptime (<= ${FreshUptimeThresholdSec}s)"

    $deadline = (Get-Date).AddSeconds($HealthTimeoutSec)
    while ((Get-Date) -lt $deadline) {
        try {
            $resp = Invoke-WebRequest -Uri $HealthUrl -TimeoutSec 3 -UseBasicParsing -ErrorAction Stop
            if ($resp.StatusCode -eq 200) {
                $body = $resp.Content | ConvertFrom-Json
                $uptimeStr = $body.uptime
                if ($uptimeStr -match '^(\d+):(\d+):(\d+)') {
                    $h = [int]$matches[1]; $m = [int]$matches[2]; $s = [int]$matches[3]
                    $uptimeSec = $h * 3600 + $m * 60 + $s
                    Write-Host "[deploy_props_bin]   /health uptime=$uptimeStr (${uptimeSec}s)"
                    if ($uptimeSec -le $FreshUptimeThresholdSec) {
                        Write-Host "[deploy_props_bin]   server reports fresh uptime -- restart confirmed" -ForegroundColor Green
                        return
                    }
                }
            }
        } catch {
            # Expected during the brief restart window.
        }
        Start-Sleep -Seconds 2
    }
    throw "[deploy_props_bin] /health never reported fresh uptime within ${HealthTimeoutSec}s."
}

# ---------------------------------------------------------------------------
# Run.
# ---------------------------------------------------------------------------
try {
    $totalBytes = Assert-LocalArtifactsReady
    Copy-ToServer
    Assert-RemoteSizeMatches -expectedBytes $totalBytes
    Restart-ServerTask
    Wait-ForFreshHealth
    $endStamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "[deploy_props_bin] ===== DONE $endStamp =====" -ForegroundColor Green
    exit 0
} catch {
    Write-Host "[deploy_props_bin] FAIL: $_" -ForegroundColor Red
    exit 1
}
