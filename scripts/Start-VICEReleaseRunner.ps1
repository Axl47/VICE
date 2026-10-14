param(
    [Parameter(Mandatory)][string]$RunnerDirectory,
    [Parameter(Mandatory)][string]$CompanionArchive,
    [string]$AssetSourceDirectory = (Split-Path $PSScriptRoot -Parent)
)
$ErrorActionPreference = 'Stop'
$runner = (Resolve-Path -LiteralPath $RunnerDirectory).Path
if (-not (Test-Path -LiteralPath (Join-Path $runner '.runner'))) { throw 'Register this runner with GitHub first; see docs/vice-release.md.' }
if (-not (Test-Path -LiteralPath $CompanionArchive -PathType Leaf)) { throw 'Companion archive not found.' }
$env:VICE_TV_COMPANION_ARCHIVE = (Resolve-Path -LiteralPath $CompanionArchive).Path
$env:VICE_ASSET_SOURCE_DIR = (Resolve-Path -LiteralPath $AssetSourceDirectory).Path
# The bundled SDK supplies .NET 8 for tests as well as the compiler. Game mods
# target .NET 6; the installed loader supplies their runtime dependencies.
$sdk = Join-Path $env:VICE_ASSET_SOURCE_DIR '.tools\dotnet-sdk-8.0.425'
if (Test-Path -LiteralPath (Join-Path $sdk 'dotnet.exe')) {
    $env:DOTNET_ROOT = $sdk
    $env:PATH = $sdk + ';' + $env:PATH
}
# Also allow the runner to find the PowerShell 7 that launched this script.
$env:PATH = $PSHOME + ';' + $env:PATH
Push-Location $runner
try {
    & (Join-Path $runner 'run.cmd')
    if ($LASTEXITCODE -ne 0) { throw "GitHub runner exited with code $LASTEXITCODE." }
} finally { Pop-Location }
