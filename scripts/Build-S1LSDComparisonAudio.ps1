param([string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe', [string]$PythonPath = 'C:\Python314\python.exe')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$projectPath = Join-Path $repoRoot 'assets/S1LSDComparisonAudio'
$logPath = Join-Path $repoRoot 'artifacts/s1lsd-audio-unity.log'
New-Item -ItemType Directory -Force -Path (Split-Path $logPath -Parent) | Out-Null
$arguments = @('-batchmode','-nographics','-quit','-projectPath',('"'+$projectPath+'"'),
    '-executeMethod','BuildComparisonAudio.Build','-logFile',('"'+$logPath+'"'))
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $logPath -Pattern 'COMPARISON_AUDIO_BUNDLE_BUILT' -Quiet)) {
    throw "Comparison audio authoring failed. See $logPath"
}
$assetPath = Join-Path $repoRoot 'src/S1LSDComparison/Assets/comparison-audio.bundle'
$containerPath = Join-Path $repoRoot 'scripts/Build-S1LSDComparisonAudioContainer.py'
& $PythonPath $containerPath
if ($LASTEXITCODE -ne 0) { throw 'Comparison audio runtime container adaptation failed.' }
$generatorPath = Join-Path $projectPath 'Assets/Editor/BuildComparisonAudio.cs'
$manifest = [ordered]@{
    unityVersion = '2022.3.62f2'; bundleSha256 = (Get-FileHash -LiteralPath $assetPath).Hash.ToLowerInvariant()
    generatorSha256 = (Get-FileHash -LiteralPath $generatorPath).Hash.ToLowerInvariant()
    containerGeneratorSha256 = (Get-FileHash -LiteralPath $containerPath).Hash.ToLowerInvariant()
    mixers = 3; effects = @('SFX Reverb','Echo','Chorus','Lowpass'); exposedParametersPerMixer = 29
    containsAudioClips = $false; origin = 'Authored comparison mixers; no game audio assets'
}
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $repoRoot 'src/S1LSDComparison/Assets/audio.json') -Encoding utf8
Write-Output "Built comparison audio: $assetPath"
