param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Schedule I',
    [string]$OriginalMoreGuns = 'C:\Games\Backup\Schedule I\Mods\MoreGuns.dll',
    [string]$DotnetPath = 'dotnet',
    [string]$NugetConfig = ''
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$env:DOTNET_CLI_HOME = Join-Path $repoRoot '.tools\cli-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
if (-not (Test-Path -LiteralPath (Join-Path $GameDir 'MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll'))) { throw 'Launch the beta with MelonLoader once to generate the interop assemblies.' }
$sdks = & $DotnetPath --list-sdks
if ($LASTEXITCODE -ne 0 -or -not ($sdks -match '^(8|9|[1-9][0-9])\.')) { throw 'A .NET SDK 8 or newer is required. Set -DotnetPath to a portable SDK if necessary.' }
& (Join-Path $PSScriptRoot 'Prepare-Assets.ps1') -OriginalDll $OriginalMoreGuns -GameDir $GameDir
& (Join-Path $PSScriptRoot 'Check-Compatibility.ps1') -GameDir $GameDir
$arguments = @('build', (Join-Path $repoRoot 'ScheduleOne.Mods.sln'), '-c', 'Release', '--nologo', '-m:1', '-v', 'minimal', "-p:GameDir=$GameDir")
if ($NugetConfig) { $arguments += @('--configfile', $NugetConfig) }
& $DotnetPath @arguments
if ($LASTEXITCODE -ne 0) { throw 'Mod compilation failed.' }
& $DotnetPath run --project (Join-Path $repoRoot 'tests\Algorithms\Algorithms.csproj') -c Release --no-build --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Algorithm checks failed.' }
& $DotnetPath run --project (Join-Path $repoRoot 'tests\GasPurchases\GasPurchases.Tests.csproj') -c Release --no-build --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Saved-purchase checks failed.' }
& $DotnetPath run --project (Join-Path $repoRoot 'tests\MoreGuns\MoreGuns.Tests.csproj') -c Release --no-build --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Weapon spin-up checks failed.' }
& (Join-Path $PSScriptRoot 'Package.ps1')
Write-Output 'Built and checked all six mods.'
