param(
    [string]$GameDir='C:\Program Files (x86)\Steam\steamapps\common\Schedule I',
    [string]$PackageDir=''
)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
if(-not $PackageDir){$PackageDir=Join-Path $repoRoot 'artifacts\VICE.Enforcement-0.1.0'}
$gameRoot=(Resolve-Path -LiteralPath $GameDir).Path
if(-not(Test-Path -LiteralPath (Join-Path $gameRoot 'Schedule I.exe'))){throw 'GameDir is not a Schedule I installation.'}
if(Get-Process -Name 'Schedule I','ScheduleI' -ErrorAction SilentlyContinue){throw 'Close Schedule I before installing VICE Enforcement.'}
$files=@('Mods\VICE.Enforcement.dll','UserLibs\VICE.Core.dll')
foreach($relative in $files){if(-not(Test-Path -LiteralPath (Join-Path $PackageDir $relative))){throw "Build the VICE package first; missing $relative"}}
$manifest=Get-Content -LiteralPath (Join-Path $PackageDir 'hashes.json') -Raw | ConvertFrom-Json
foreach($item in $manifest){if((Get-FileHash -LiteralPath (Join-Path $PackageDir $item.File)).Hash -ne $item.SHA256){throw "Package hash mismatch: $($item.File)"}}
$backup=Join-Path $repoRoot ('artifacts\install-backups\VICE.Enforcement-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$conflicts=@('Personal.BetterPolice.dll','BetterPolice.dll','NACops.dll','NACops-IL2Cpp.dll','Personal.NACops.dll','VICE.Core.dll')
$previous=@($files)+@($conflicts | ForEach-Object {'Mods\'+$_})
foreach($relative in $previous){
    $target=Join-Path $gameRoot $relative
    if(Test-Path -LiteralPath $target){
        $saved=Join-Path $backup $relative
        New-Item -ItemType Directory -Path (Split-Path $saved -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $target -Destination $saved
    }
}
foreach($name in $conflicts){
    $target=Join-Path $gameRoot ('Mods\'+$name)
    if(Test-Path -LiteralPath $target){
        $disabled=$target+'.disabled'
        if(Test-Path -LiteralPath $disabled){
            $saved=Join-Path $backup ('Mods\'+$name+'.disabled')
            New-Item -ItemType Directory -Path (Split-Path $saved -Parent) -Force | Out-Null
            Copy-Item -LiteralPath $disabled -Destination $saved
        }
        Move-Item -LiteralPath $target -Destination $disabled -Force
    }
}
foreach($relative in $files){
    $target=Join-Path $gameRoot $relative
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PackageDir $relative) -Destination $target -Force
    if((Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath (Join-Path $PackageDir $relative)).Hash){throw "Installed hash mismatch: $relative"}
}
$defaults=Join-Path $PackageDir 'UserData\VICE.Enforcement'
foreach($file in (Get-ChildItem -LiteralPath $defaults -Recurse -File)){
    $relative=$file.FullName.Substring($defaults.Length+1)
    $target=Join-Path $gameRoot ('UserData\VICE.Enforcement\'+$relative)
    if(-not(Test-Path -LiteralPath $target)){
        New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $target
    }
}
Write-Output "Installed VICE.Enforcement.dll and shared UserLibs\VICE.Core.dll. Previous police modules are disabled; backup: $backup"
