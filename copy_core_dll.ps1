# copy_core_dll.ps1 — Build ForeverEngine.Core and copy to Unity Plugins
$ErrorActionPreference = 'Stop'

$coreDir = "C:\Dev\ForeverEngine.Core.Repo"
$unityPlugins = "C:\Dev\Forever engine\Assets\Plugins"
$buildOutput = "$coreDir\ForeverEngine.Core\bin\Release\netstandard2.1"

Write-Host "Building ForeverEngine.Core (Release)..."
Push-Location $coreDir
dotnet build ForeverEngine.Core/ForeverEngine.Core.csproj -c Release --nologo -v q
Pop-Location

if (-not (Test-Path $unityPlugins)) {
    New-Item -ItemType Directory -Path $unityPlugins | Out-Null
}

# Copy all DLLs from build output (Core + dependencies)
$dlls = Get-ChildItem "$buildOutput\*.dll"
foreach ($dll in $dlls) {
    Copy-Item $dll.FullName "$unityPlugins\$($dll.Name)" -Force
    Write-Host "  Copied $($dll.Name)"
}

Write-Host "Done. $($dlls.Count) DLL(s) copied to $unityPlugins"
