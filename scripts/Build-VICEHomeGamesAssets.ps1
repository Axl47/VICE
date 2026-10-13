param(
    [switch]$Rebuild,
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$source = Join-Path $repo 'assets\VICEHomeGames'
$bundle = Join-Path $repo 'src\VICE.Lifestyle\Assets\homegames.bundle'
$contractPath = Join-Path $repo 'src\VICE.Lifestyle\Assets\homegames.contract.json'
$accepted = Get-Content -LiteralPath (Join-Path $source 'validation\unity\accepted-artwork-sha256.json') -Raw | ConvertFrom-Json
foreach ($entry in $accepted.sha256.PSObject.Properties) {
    $path = Join-Path $source $entry.Name
    if (-not (Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) {
        throw "Accepted PR #7 artwork changed: $($entry.Name). Do not regenerate or substitute review probes."
    }
}
function Test-CurrentBundle {
    if (-not (Test-Path -LiteralPath $contractPath) -or -not (Test-Path -LiteralPath $bundle)) { return $false }
    $contract = Get-Content -LiteralPath $contractPath -Raw | ConvertFrom-Json
    if ($contract.schema -ne 1) { return $false }
    foreach ($entry in @($contract.bundle) + @($contract.sources)) {
        $path = Join-Path $repo $entry.path
        if (-not (Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256) { return $false }
    }
    return $true
}
if (-not $Rebuild -and (Test-CurrentBundle)) { Write-Output 'Verified current PR #7 home-games bundle and inputs.'; return }
if (-not (Test-Path -LiteralPath $UnityPath)) { throw 'Unity 2022.3.62f2 is required to build the home-games bundle.' }
$project = Join-Path $repo '.tools\vice-home-games-unity'
foreach ($folder in @('Assets\Editor','Assets\Models','Assets\Models\Textures','Packages','ProjectSettings')) {
    New-Item -ItemType Directory -Path (Join-Path $project $folder) -Force | Out-Null
}
foreach ($name in @('PoolTable','PoolAccessories','DartsBoard','Dart')) {
    Copy-Item -LiteralPath (Join-Path $source "$name.fbx") -Destination (Join-Path $project "Assets\Models\$name.fbx") -Force
}
Copy-Item -Path (Join-Path $source 'Textures\*.png') -Destination (Join-Path $project 'Assets\Models\Textures') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Unity\BuildHomeGames.cs') -Destination (Join-Path $project 'Assets\Editor\BuildHomeGames.cs') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Unity\HomeGamesPresentation.cs') -Destination (Join-Path $project 'Assets\Editor\HomeGamesPresentation.cs') -Force
[IO.File]::WriteAllText((Join-Path $project 'Packages\manifest.json'),'{"dependencies":{"com.unity.render-pipelines.universal":"14.0.12","com.unity.modules.assetbundle":"1.0.0","com.unity.modules.imageconversion":"1.0.0","com.unity.modules.physics":"1.0.0"}}')
[IO.File]::WriteAllText((Join-Path $project 'ProjectSettings\ProjectVersion.txt'),"m_EditorVersion: 2022.3.62f2`n")
New-Item -ItemType Directory -Path (Join-Path $repo 'artifacts') -Force | Out-Null
$log = Join-Path $repo 'artifacts\vice-home-games-unity.log'
$unityArguments = @('-batchmode','-quit','-projectPath',('"'+$project+'"'),'-executeMethod','BuildHomeGames.Build',
    '-homeGamesRepository',('"'+$repo+'"'),'-logFile',('"'+$log+'"'))
$process = Start-Process -FilePath $UnityPath -ArgumentList $unityArguments -WindowStyle Hidden -PassThru
while (-not $process.WaitForExit(30000)) { Write-Output "Unity is importing/building home-games artwork; log: $log" }
if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -Pattern 'VICE_HOME_GAMES_BUNDLE_BUILT' -Quiet)) {
    throw "Home-games bundle build failed. See $log"
}
if (-not (Test-CurrentBundle)) { throw 'Home-games bundle/input hash verification failed after Unity build.' }
Write-Output "Built and verified accepted PR #7 models: $bundle"
