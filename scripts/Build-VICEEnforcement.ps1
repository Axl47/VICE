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
foreach($project in @('tests\VICE.Core\VICE.Core.Tests.csproj','tests\BetterPolice\BetterPolice.Tests.csproj','src\VICE.Enforcement\VICE.Enforcement.csproj','src\BetterPolice\BetterPolice.csproj')){
    $arguments=@('build',(Join-Path $repoRoot $project),'-c','Release','--nologo','-m:1','-v','minimal','-warnaserror',"-p:GameDir=$GameDir")
    if(Test-Path -LiteralPath $config){$arguments+=@('--configfile',$config)}
    & $sdk @arguments
    if($LASTEXITCODE -ne 0){throw "Compilation failed: $project"}
}
foreach($project in @('tests\VICE.Core\VICE.Core.Tests.csproj','tests\BetterPolice\BetterPolice.Tests.csproj')){
    & $sdk run --project (Join-Path $repoRoot $project) -c Release --no-build --no-restore
    if($LASTEXITCODE -ne 0){throw "Checks failed: $project"}
}
& (Join-Path $PSScriptRoot 'Check-VICEEnforcement.ps1') -GameDir $GameDir
$output=Join-Path $repoRoot 'artifacts\VICE.Enforcement-0.1.0'
foreach($folder in @('Mods','UserLibs','UserData\VICE.Enforcement','Licenses')){New-Item -ItemType Directory -Path (Join-Path $output $folder) -Force | Out-Null}
Copy-Item -LiteralPath (Join-Path $repoRoot 'src\VICE.Enforcement\bin\Release\net6.0\VICE.Enforcement.dll') -Destination (Join-Path $output 'Mods\VICE.Enforcement.dll') -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'src\VICE.Core\bin\Release\net6.0\VICE.Core.dll') -Destination (Join-Path $output 'UserLibs\VICE.Core.dll') -Force
Copy-Item -Path (Join-Path $repoRoot 'src\VICE.Enforcement\Defaults\*') -Destination (Join-Path $output 'UserData\VICE.Enforcement') -Recurse -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'src\VICE.Enforcement\LICENSE.NACops') -Destination (Join-Path $output 'Licenses\NACops-MIT.txt') -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs\vice-enforcement.md') -Destination (Join-Path $output 'READ-ME.md') -Force
Get-ChildItem -LiteralPath $output -Recurse -File | Where-Object Extension -eq '.dll' | ForEach-Object {
    [pscustomobject]@{File=$_.FullName.Substring($output.Length+1); SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'hashes.json')
$archive=Join-Path $repoRoot 'artifacts\VICE.Enforcement-0.1.0.zip'
Compress-Archive -Path (Join-Path $output '*') -DestinationPath $archive -Force
if($Install){& (Join-Path $PSScriptRoot 'Install-VICEEnforcement.ps1') -GameDir $GameDir -PackageDir $output}
Write-Output "Built and checked VICE Enforcement: $archive"
