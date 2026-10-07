param(
    [string]$GameDir='C:\Program Files (x86)\Steam\steamapps\common\Schedule I',
    [switch]$Install
)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
$sdk=Join-Path $repoRoot '.tools\dotnet-sdk-8.0.425\dotnet.exe'
if(-not(Test-Path -LiteralPath $sdk)){$sdk='dotnet'}
$env:DOTNET_CLI_HOME=Join-Path $repoRoot '.tools\cli-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$config=Join-Path $repoRoot 'NuGet.local.config'
$tests=Join-Path $repoRoot 'tests\PersonalTV\PersonalTV.Tests.csproj'
$testArgs=@('build',$tests,'-c','Release','--nologo','-m:1','-v','minimal','-warnaserror')
if(Test-Path -LiteralPath $config){$testArgs+=@('--configfile',$config)}
& $sdk @testArgs
if($LASTEXITCODE -ne 0){throw 'TV test compilation failed.'}
& $sdk run --project $tests -c Release --no-build --no-restore
if($LASTEXITCODE -ne 0){throw 'TV state/contract tests failed.'}
$buildArgs=@('build',(Join-Path $repoRoot 'src\PersonalTV\PersonalTV.csproj'),'-c','Release','--nologo','-m:1','-v','minimal',"-p:GameDir=$GameDir",'-warnaserror')
if(Test-Path -LiteralPath $config){$buildArgs+=@('--configfile',$config)}
& $sdk @buildArgs
if($LASTEXITCODE -ne 0){throw 'TV mod compilation failed.'}
$output=Join-Path $repoRoot 'artifacts\PersonalTV-0.1.2'
$mods=Join-Path $output 'Mods'
New-Item -ItemType Directory -Path $mods -Force | Out-Null
$built=Join-Path $repoRoot 'src\PersonalTV\bin\Release\net6.0\Personal.TV.dll'
Copy-Item -LiteralPath $built -Destination $mods -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs\personal-tv.md') -Destination (Join-Path $output 'READ-ME.md') -Force
Compress-Archive -Path (Join-Path $output '*') -DestinationPath (Join-Path $repoRoot 'artifacts\PersonalTV-0.1.2.zip') -Force
if($Install){
    if(Get-Process -Name 'Schedule I' -ErrorAction SilentlyContinue){throw 'Close the game before installing Personal Wall Screens.'}
    $backup=Join-Path $repoRoot ('artifacts\install-backups\PersonalTV-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
    $destination=Join-Path $GameDir 'Mods\Personal.TV.dll'
    $proof=Join-Path $GameDir 'Mods\Personal.TVProof.dll'
    if((Test-Path -LiteralPath $destination)-or(Test-Path -LiteralPath $proof)){
        New-Item -ItemType Directory -Path $backup -Force | Out-Null
        foreach($source in @($destination,$proof)){if(Test-Path -LiteralPath $source){Copy-Item -LiteralPath $source -Destination $backup}}
    }
    if(Test-Path -LiteralPath $proof){Move-Item -LiteralPath $proof -Destination ($proof+'.disabled') -Force}
    Copy-Item -LiteralPath $built -Destination $destination -Force
    if((Get-FileHash -LiteralPath $built).Hash -ne (Get-FileHash -LiteralPath $destination).Hash){throw 'Installed TV DLL hash does not match the build.'}
    Write-Output 'Installed Personal.TV.dll. It loads on the next game launch.'
}
Write-Output (Join-Path $repoRoot 'artifacts\PersonalTV-0.1.2.zip')
