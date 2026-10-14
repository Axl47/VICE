param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Schedule I',
    [string]$DotnetPath = 'dotnet',
    [switch]$RebuildArt,
    [string]$BlenderPath = 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe',
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$portableSdk = Join-Path $repoRoot '.tools\dotnet-sdk-8.0.425\dotnet.exe'
if ($DotnetPath -eq 'dotnet' -and (Test-Path -LiteralPath $portableSdk)) { $DotnetPath = $portableSdk }
$env:DOTNET_CLI_HOME = Join-Path $repoRoot '.tools\cli-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
if ($RebuildArt) {
    & $BlenderPath --background --python-exit-code 1 --python (Join-Path $PSScriptRoot 'Build-LSDStudio.py')
    if ($LASTEXITCODE -ne 0) { throw 'Blender studio generation failed.' }
    & $BlenderPath --background --python-exit-code 1 --python (Join-Path $PSScriptRoot 'Render-LSDIcons.py')
    if ($LASTEXITCODE -ne 0) { throw 'Blender icon rendering failed.' }
    & $BlenderPath --background --python-exit-code 1 --python (Join-Path $PSScriptRoot 'Check-LSDStudio.py')
    if ($LASTEXITCODE -ne 0) { throw 'Studio model contract checks failed.' }
    $unityLog = Join-Path $repoRoot 'artifacts\lsd-studio-unity.log'
    New-Item -ItemType Directory -Path (Split-Path $unityLog -Parent) -Force | Out-Null
    $unityArguments = @('-batchmode','-nographics','-quit','-projectPath',('"'+(Join-Path $repoRoot 'assets\LSDStudio')+'"'),
        '-executeMethod','BuildStudio.Build','-logFile',('"'+$unityLog+'"'))
    $unityProcess = Start-Process -FilePath $UnityPath -ArgumentList $unityArguments -WindowStyle Hidden -PassThru
    $unityProcess.WaitForExit()
    if ($unityProcess.ExitCode -ne 0 -or -not (Select-String -LiteralPath $unityLog -Pattern 'LSD_STUDIO_BUNDLE_BUILT' -Quiet)) {
        throw "Studio bundle build failed. See $unityLog"
    }
}
# Compatibility entry point: the workshop and treatment now ship as one VICE module.
& (Join-Path $PSScriptRoot 'Build-VICE.ps1') -Modules Substances -GameDir $GameDir -DotnetPath $DotnetPath
