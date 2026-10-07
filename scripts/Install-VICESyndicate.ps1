param(
    [string]$GameDir='C:\Program Files (x86)\Steam\steamapps\common\Schedule I',
    [string]$PackageDir=''
)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
if(-not $PackageDir){$PackageDir=Join-Path $repoRoot 'artifacts\VICE.Syndicate-0.1.0'}
$gameRoot=(Resolve-Path -LiteralPath $GameDir).Path
if(-not(Test-Path -LiteralPath (Join-Path $gameRoot 'Schedule I.exe'))){throw 'GameDir is not a Schedule I installation.'}
if(Get-Process -Name 'Schedule I','ScheduleI' -ErrorAction SilentlyContinue){throw 'Close Schedule I before installing VICE Syndicate.'}
$conflicts=Get-ChildItem -LiteralPath (Join-Path $gameRoot 'Mods') -Filter '*.dll' | Where-Object Name -like '*CartelEnforcer*'
if($conflicts){throw 'Remove the original Cartel Enforcer DLL before installing VICE Syndicate.'}
$manifest=Get-Content -LiteralPath (Join-Path $PackageDir 'hashes.json') -Raw | ConvertFrom-Json
$packageRoot=(Resolve-Path -LiteralPath $PackageDir).Path.TrimEnd('\')+'\'
foreach($item in $manifest){
    $source=[IO.Path]::GetFullPath((Join-Path $packageRoot $item.File))
    if(-not $source.StartsWith($packageRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Invalid package manifest path.'}
    if((Get-FileHash -LiteralPath $source).Hash -ne $item.SHA256){throw "Package hash mismatch: $($item.File)"}
}
$backup=Join-Path $repoRoot ('artifacts\install-backups\VICE.Syndicate-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
foreach($relative in @('Mods\VICE.Syndicate.dll','UserLibs\VICE.Core.dll')){
    $source=Join-Path $PackageDir $relative
    $target=Join-Path $gameRoot $relative
    if(Test-Path -LiteralPath $target){
        $saved=Join-Path $backup $relative
        New-Item -ItemType Directory -Path (Split-Path $saved -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $target -Destination $saved
    }
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $target -Force
    if((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $target).Hash){throw "Installed hash mismatch: $relative"}
}
$defaults=Join-Path $PackageDir 'UserData\VICE.Syndicate'
foreach($file in (Get-ChildItem -LiteralPath $defaults -Recurse -File)){
    $relative=$file.FullName.Substring($defaults.Length+1)
    $target=Join-Path $gameRoot ('UserData\VICE.Syndicate\'+$relative)
    if(-not(Test-Path -LiteralPath $target)){
        New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $target
    }
}
Write-Output 'Installed VICE.Syndicate.dll and shared UserLibs\VICE.Core.dll. Existing Syndicate configuration was preserved.'
