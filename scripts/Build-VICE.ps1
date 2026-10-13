param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Schedule I',
    [ValidateSet('Commerce', 'Arsenal', 'Lifestyle', 'Interface', 'Enforcement', 'Syndicate')]
    [string[]]$Modules = @(),
    [ValidateSet('YouTube')][string[]]$AddOns = @(),
    [switch]$All,
    [string]$OriginalMoreGuns = 'C:\Games\Backup\Schedule I\Mods\MoreGuns.dll',
    [string]$DotnetPath = '',
    [string]$NugetConfig = '',
    [string]$OutputDirectory = '',
    [switch]$Install
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if ($All -and $Modules.Count) { throw 'Use either -All or -Modules.' }
if ($All) { $Modules = @('Commerce','Arsenal','Lifestyle','Interface','Enforcement','Syndicate') }
$names = @('Commerce','Arsenal','Lifestyle','Interface','Enforcement','Syndicate')
$Modules = @($names | Where-Object { $_ -in $Modules } | Sort-Object)
$AddOns = @($AddOns | Sort-Object -Unique)
if (-not $DotnetPath) {
    $DotnetPath = Join-Path $repoRoot '.tools\dotnet-sdk-8.0.425\dotnet.exe'
    if (-not (Test-Path -LiteralPath $DotnetPath)) { $DotnetPath = 'dotnet' }
}
if (-not $NugetConfig -and (Test-Path -LiteralPath (Join-Path $repoRoot 'NuGet.local.config'))) { $NugetConfig = Join-Path $repoRoot 'NuGet.local.config' }
$env:DOTNET_CLI_HOME = Join-Path $repoRoot '.tools\cli-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
function Build-Project([string]$relative) {
    $arguments = @('build', (Join-Path $repoRoot $relative), '-c', 'Release', '--nologo', '-m:1', '-v', 'minimal', '-warnaserror', "-p:GameDir=$GameDir")
    if ($NugetConfig) { $arguments += @('--configfile', $NugetConfig) }
    & $DotnetPath @arguments
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $relative" }
}
function Run-Checks([string]$relative, [string[]]$extra = @()) {
    Build-Project $relative
    & $DotnetPath run --project (Join-Path $repoRoot $relative) -c Release --no-build --no-restore -- @extra
    if ($LASTEXITCODE -ne 0) { throw "Checks failed: $relative" }
}
if (($Modules.Count -or $AddOns.Count) -and -not (Test-Path -LiteralPath (Join-Path $GameDir 'MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll'))) {
    throw 'Launch the IL2CPP game with MelonLoader once to generate its interop assemblies.'
}
if (($Modules -contains 'Lifestyle' -or $AddOns -contains 'YouTube') -and -not (Test-Path -LiteralPath (Join-Path $GameDir 'Mods\S1API.Il2Cpp.MelonLoader.dll'))) {
    throw 'VICE Lifestyle requires the IL2CPP S1API dependency.'
}
if ($Modules -contains 'Arsenal' -and -not (Test-Path -LiteralPath (Join-Path $repoRoot '.research\assets\moreguns.bundle'))) {
    & (Join-Path $PSScriptRoot 'Prepare-Assets.ps1') -OriginalDll $OriginalMoreGuns -GameDir $GameDir
}
# Every invocation compiles and checks this same Core before selected modules.
Build-Project 'src\VICE.Core\VICE.Core.csproj'
Run-Checks 'tests\VICE.Core\VICE.Core.Tests.csproj'
$buildModules = @($Modules)
if ($AddOns -contains 'YouTube' -and $buildModules -notcontains 'Lifestyle') { $buildModules += 'Lifestyle' }
foreach ($module in $buildModules) {
    Build-Project "src\VICE.$module\VICE.$module.csproj"
    if ($module -in @('Commerce','Arsenal','Lifestyle','Interface')) {
        & (Join-Path $PSScriptRoot 'Check-NativeTargets.ps1') -GameDir $GameDir -SourceDir (Join-Path $repoRoot "src\VICE.$module")
    }
    switch ($module) {
        'Commerce' { Run-Checks 'tests\Algorithms\Algorithms.csproj'; Run-Checks 'tests\GasPurchases\GasPurchases.Tests.csproj' }
        'Arsenal' { Run-Checks 'tests\MoreGuns\MoreGuns.Tests.csproj' }
        'Lifestyle' { Run-Checks 'tests\VICE.Lifestyle\VICE.Lifestyle.Tests.csproj' }
        'Enforcement' {
            Run-Checks 'tests\BetterPolice\BetterPolice.Tests.csproj'
            & (Join-Path $PSScriptRoot 'Check-VICEEnforcement.ps1') -GameDir $GameDir
        }
        'Syndicate' { Run-Checks 'tests\VICE.Syndicate\VICE.Syndicate.Tests.csproj' @($GameDir, (Join-Path $repoRoot 'src\VICE.Syndicate\bin\Release\net6.0\VICE.Syndicate.dll')) }
    }
}
foreach ($addOn in $AddOns) {
    Build-Project "src\VICE.$addOn\VICE.$addOn.csproj"
    Run-Checks "tests\VICE.$addOn\VICE.$addOn.Tests.csproj"
}
$ordinary = @($buildModules | Where-Object { $_ -in @('Commerce','Arsenal','Lifestyle','Interface') })
if ($ordinary.Count) { Run-Checks 'tests\VICE.Modules\VICE.Modules.Tests.csproj' (@($repoRoot) + $ordinary) }
$package = & (Join-Path $PSScriptRoot 'Package-VICE.ps1') -Modules $Modules -AddOns $AddOns -OutputDirectory $OutputDirectory
& (Join-Path $PSScriptRoot 'Test-VICEPackage.ps1') -PackageDir $package.DirectoryPath -ExpectedModules $Modules -ExpectedAddOns $AddOns
& (Join-Path $PSScriptRoot 'Test-VICEInstall.ps1') -PackageDir $package.DirectoryPath
if ($Install) { & (Join-Path $PSScriptRoot 'Install-VICE.ps1') -GameDir $GameDir -PackageDir $package.DirectoryPath }
Write-Output $package
