param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Schedule I',
    [string]$DotnetPath = '',
    [string]$NugetConfig = ''
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
foreach ($project in @('src\VICE.Lifestyle\VICE.Lifestyle.csproj', 'tests\VICE.Core\VICE.Core.Tests.csproj', 'tests\VICE.Lifestyle\VICE.Lifestyle.Tests.csproj')) {
    $arguments = @('build', (Join-Path $repoRoot $project), '-c', 'Release', '--nologo', '-m:1', '-v', 'minimal', '-warnaserror', "-p:GameDir=$GameDir")
    if ($NugetConfig) { $arguments += @('--configfile', $NugetConfig) }
    & $DotnetPath @arguments
    if ($LASTEXITCODE -ne 0) { throw "VICE Lifestyle compilation failed: $project" }
}
& $DotnetPath run --project (Join-Path $repoRoot 'tests\VICE.Core\VICE.Core.Tests.csproj') -c Release --no-build --no-restore
if ($LASTEXITCODE -ne 0) { throw 'VICE Core lifecycle and data migration checks failed.' }
& $DotnetPath run --project (Join-Path $repoRoot 'tests\VICE.Lifestyle\VICE.Lifestyle.Tests.csproj') -c Release --no-build --no-restore
if ($LASTEXITCODE -ne 0) { throw 'VICE Lifestyle state and transfer checks failed.' }
& (Join-Path $PSScriptRoot 'Check-NativeTargets.ps1') -GameDir $GameDir -SourceDir (Join-Path $repoRoot 'src\VICE.Lifestyle')
[xml]$projectXml = Get-Content -LiteralPath (Join-Path $repoRoot 'src\VICE.Lifestyle\VICE.Lifestyle.csproj') -Raw
$version = [string]$projectXml.Project.PropertyGroup.Version
$package = Join-Path $repoRoot "artifacts\VICE.Lifestyle-$version"
$mods = Join-Path $package 'Mods'
$libs = Join-Path $package 'UserLibs'
New-Item -ItemType Directory -Path $mods, $libs -Force | Out-Null
$dll = Join-Path $mods 'VICE.Lifestyle.dll'
Copy-Item -LiteralPath (Join-Path $repoRoot 'src\VICE.Lifestyle\bin\Release\net6.0\VICE.Lifestyle.dll') -Destination $dll -Force
$core = Join-Path $libs 'VICE.Core.dll'
Copy-Item -LiteralPath (Join-Path $repoRoot 'src\VICE.Core\bin\Release\net6.0\VICE.Core.dll') -Destination $core -Force
$guide = Join-Path $package 'README.md'
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs\vice-lifestyle-video-sharing.md') -Destination $guide -Force
$hash = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash.ToLowerInvariant()
$coreHash = (Get-FileHash -LiteralPath $core -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath (Join-Path $package 'sha256.txt') -Value @("$hash  Mods/VICE.Lifestyle.dll", "$coreHash  UserLibs/VICE.Core.dll") -Encoding utf8
$zip = "$package.zip"
Compress-Archive -LiteralPath $mods, $libs, $guide, (Join-Path $package 'sha256.txt') -DestinationPath $zip -Force
Write-Output "Built and checked VICE Lifestyle: $zip"
