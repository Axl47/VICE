param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Schedule I',
    [string]$OriginalMoreGuns = 'C:\Games\Backup\Schedule I\Mods\MoreGuns.dll',
    [string]$DotnetPath = '',
    [string]$NugetConfig = '',
    [switch]$Install
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if (-not $DotnetPath) {
    $DotnetPath = Join-Path $repoRoot '.tools\dotnet-sdk-8.0.425\dotnet.exe'
    if (-not (Test-Path -LiteralPath $DotnetPath)) { $DotnetPath = 'dotnet' }
}
if (-not $NugetConfig -and (Test-Path -LiteralPath (Join-Path $repoRoot 'NuGet.local.config'))) {
    $NugetConfig = Join-Path $repoRoot 'NuGet.local.config'
}
$env:DOTNET_CLI_HOME = Join-Path $repoRoot '.tools\cli-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
if (-not (Test-Path -LiteralPath (Join-Path $GameDir 'MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll'))) {
    throw 'Launch the IL2CPP beta with MelonLoader once to generate the interop assemblies.'
}
if (-not (Test-Path -LiteralPath (Join-Path $GameDir 'Mods\S1API.Il2Cpp.MelonLoader.dll'))) {
    throw 'VICE Lifestyle requires the installed IL2CPP S1API furniture and lifecycle APIs.'
}
if (-not (Test-Path -LiteralPath (Join-Path $repoRoot '.research\assets\moreguns.bundle'))) {
    & (Join-Path $PSScriptRoot 'Prepare-Assets.ps1') -OriginalDll $OriginalMoreGuns -GameDir $GameDir
}
foreach ($module in @('Commerce', 'Arsenal', 'Lifestyle', 'Interface')) {
    $arguments = @('build', (Join-Path $repoRoot "src\VICE.$module\VICE.$module.csproj"), '-c', 'Release', '--nologo', '-m:1', '-v', 'minimal', '-warnaserror', "-p:GameDir=$GameDir")
    if ($NugetConfig) { $arguments += @('--configfile', $NugetConfig) }
    & $DotnetPath @arguments
    if ($LASTEXITCODE -ne 0) { throw "VICE $module compilation failed." }
    & (Join-Path $PSScriptRoot 'Check-NativeTargets.ps1') -GameDir $GameDir -SourceDir (Join-Path $repoRoot "src\VICE.$module")
}
foreach ($test in @('Algorithms\Algorithms.csproj', 'GasPurchases\GasPurchases.Tests.csproj', 'MoreGuns\MoreGuns.Tests.csproj', 'VICE.Lifestyle\VICE.Lifestyle.Tests.csproj', 'VICE.Modules\VICE.Modules.Tests.csproj')) {
    $project = Join-Path $repoRoot ('tests\' + $test)
    $arguments = @('build', $project, '-c', 'Release', '--nologo', '-m:1', '-v', 'minimal', '-warnaserror', "-p:GameDir=$GameDir")
    if ($NugetConfig) { $arguments += @('--configfile', $NugetConfig) }
    & $DotnetPath @arguments
    if ($LASTEXITCODE -ne 0) { throw "Test compilation failed: $test" }
    & $DotnetPath run --project $project -c Release --no-build --no-restore -- $repoRoot
    if ($LASTEXITCODE -ne 0) { throw "Checks failed: $test" }
}
& (Join-Path $PSScriptRoot 'Package-VICEModules.ps1')
& (Join-Path $PSScriptRoot 'Test-VICEModulesInstall.ps1')
if ($Install) { & (Join-Path $PSScriptRoot 'Install-VICEModules.ps1') -GameDir $GameDir }
Write-Output 'Built and checked VICE Commerce, Arsenal, Lifestyle and Interface. Enforcement, Syndicate and Substances use their separate build workflows.'
