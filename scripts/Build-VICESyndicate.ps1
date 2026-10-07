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
foreach($project in @('src\VICE.Syndicate\VICE.Syndicate.csproj','tests\VICE.Syndicate\VICE.Syndicate.Tests.csproj')){
    $buildArgs=@('build',(Join-Path $repoRoot $project),'-c','Release','--nologo','-m:1','-v','minimal','-warnaserror',"-p:GameDir=$GameDir")
    if(Test-Path -LiteralPath $config){$buildArgs+=@('--configfile',$config)}
    & $sdk @buildArgs
    if($LASTEXITCODE -ne 0){throw "Compilation failed: $project"}
}
$built=Join-Path $repoRoot 'src\VICE.Syndicate\bin\Release\net6.0\VICE.Syndicate.dll'
& $sdk run --project (Join-Path $repoRoot 'tests\VICE.Syndicate\VICE.Syndicate.Tests.csproj') -c Release --no-build --no-restore -- $GameDir $built
if($LASTEXITCODE -ne 0){throw 'Syndicate difficulty or beta patch contracts failed.'}
$output=Join-Path $repoRoot 'artifacts\VICE.Syndicate-0.1.0'
foreach($folder in @('Mods','UserLibs','UserData\VICE.Syndicate','Licenses')){New-Item -ItemType Directory -Path (Join-Path $output $folder) -Force | Out-Null}
Copy-Item -LiteralPath $built -Destination (Join-Path $output 'Mods\VICE.Syndicate.dll') -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'src\VICE.Core\bin\Release\net6.0\VICE.Core.dll') -Destination (Join-Path $output 'UserLibs\VICE.Core.dll') -Force
Copy-Item -Path (Join-Path $repoRoot 'src\VICE.Syndicate\Defaults\*') -Destination (Join-Path $output 'UserData\VICE.Syndicate') -Recurse -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'src\VICE.Syndicate\LICENSE.CartelEnforcer') -Destination (Join-Path $output 'Licenses\CartelEnforcer-MIT.txt') -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs\vice-syndicate.md') -Destination (Join-Path $output 'READ-ME.md') -Force
Get-ChildItem -LiteralPath $output -Recurse -File | Where-Object Name -ne 'hashes.json' | ForEach-Object {
    [pscustomobject]@{File=$_.FullName.Substring($output.Length+1); SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'hashes.json')
$archive=Join-Path $repoRoot 'artifacts\VICE.Syndicate-0.1.0.zip'
Compress-Archive -Path (Join-Path $output '*') -DestinationPath $archive -Force
if($Install){& (Join-Path $PSScriptRoot 'Install-VICESyndicate.ps1') -GameDir $GameDir -PackageDir $output}
Write-Output "Built and checked VICE Syndicate: $archive"
