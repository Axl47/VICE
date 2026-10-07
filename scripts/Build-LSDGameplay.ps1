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
    & $BlenderPath --background --python (Join-Path $PSScriptRoot 'Build-LSDStudio.py')
    if ($LASTEXITCODE -ne 0) { throw 'Blender studio generation failed.' }
    & $BlenderPath --background --python (Join-Path $PSScriptRoot 'Render-LSDIcons.py')
    if ($LASTEXITCODE -ne 0) { throw 'Blender icon rendering failed.' }
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
if (-not (Test-Path -LiteralPath (Join-Path $repoRoot 'src\LSDGameplay\Assets\studio.bundle'))) { throw 'Studio assets missing. Build with -RebuildArt.' }
$nuget = Join-Path $repoRoot 'NuGet.local.config'
foreach ($project in @('src\LSDGameplay\LSDGameplay.csproj','tests\LSDGameplay\LSDGameplay.Tests.csproj')) {
    $arguments = @('build',(Join-Path $repoRoot $project),'-c','Release','--nologo','-warnaserror',"-p:GameDir=$GameDir")
    if (Test-Path -LiteralPath $nuget) { $arguments += @('--configfile',$nuget) }
    & $DotnetPath @arguments
    if ($LASTEXITCODE -ne 0) { throw "Workshop compilation failed: $project" }
}
& $DotnetPath run --project (Join-Path $repoRoot 'tests\LSDGameplay\LSDGameplay.Tests.csproj') -c Release --no-build --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Workshop persistence/rule checks failed.' }
& (Join-Path $PSScriptRoot 'Build-LSDPreview.ps1') -GameDir $GameDir -DotnetPath $DotnetPath
if (-not $?) { throw 'Integrated visual treatment checks failed.' }
$package = Join-Path $repoRoot 'artifacts\LSDGameplay-0.2.3'
$mods = Join-Path $package 'Mods'
New-Item -ItemType Directory -Path $mods -Force | Out-Null
foreach ($project in @('LSDGameplay','LSDPreview')) {
    Copy-Item -LiteralPath (Join-Path $repoRoot "src\$project\bin\Release\net6.0\Personal.$project.dll") -Destination $mods -Force
}
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs\lsd-workshop.md') -Destination (Join-Path $package 'README.md') -Force
$hashes = Get-ChildItem -LiteralPath $mods -File | ForEach-Object { (Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()+'  Mods/'+$_.Name }
$hashes += (Get-FileHash -LiteralPath (Join-Path $package 'README.md')).Hash.ToLowerInvariant()+'  README.md'
Set-Content -LiteralPath (Join-Path $package 'SHA256SUMS.txt') -Value $hashes -Encoding utf8
$zip = Join-Path $repoRoot 'artifacts\LSDGameplay-0.2.3.zip'
Compress-Archive -LiteralPath $mods,(Join-Path $package 'README.md'),(Join-Path $package 'SHA256SUMS.txt') -DestinationPath $zip -Force
Write-Output "Built workshop and checked visual treatment: $zip"
