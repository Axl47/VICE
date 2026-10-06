param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Schedule I',
    [string]$DotnetPath = 'dotnet',
    [string]$NugetConfig = ''
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$portableSdk = Join-Path $repoRoot '.tools\dotnet-sdk-8.0.425\dotnet.exe'
if ($DotnetPath -eq 'dotnet' -and (Test-Path -LiteralPath $portableSdk)) { $DotnetPath = $portableSdk }
if (-not $NugetConfig -and (Test-Path -LiteralPath (Join-Path $repoRoot 'NuGet.local.config'))) {
    $NugetConfig = Join-Path $repoRoot 'NuGet.local.config'
}
$env:DOTNET_CLI_HOME = Join-Path $repoRoot '.tools\cli-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$projects = @('src\LSDPreview\LSDPreview.csproj', 'tests\LSDPreview\LSDPreview.Tests.csproj')
foreach ($project in $projects) {
    $buildArguments = @('build', (Join-Path $repoRoot $project), '-c', 'Release', '--nologo', '-m:1', '-v', 'minimal', "-p:GameDir=$GameDir", '-warnaserror')
    if ($NugetConfig) { $buildArguments += @('--configfile', $NugetConfig) }
    & $DotnetPath @buildArguments
    if ($LASTEXITCODE -ne 0) { throw "LSD preview compilation failed: $project" }
}
& $DotnetPath run --project (Join-Path $repoRoot 'tests\LSDPreview\LSDPreview.Tests.csproj') -c Release --no-build --no-restore
if ($LASTEXITCODE -ne 0) { throw 'LSD preview timing, gaze, transport, color ownership and audio envelope checks failed.' }

$builtDll = Join-Path $repoRoot 'src\LSDPreview\bin\Release\net6.0\Personal.LSDPreview.dll'
# The live 0.4.0 log exposed a missing IL2CPP generic AudioListener lookup.
# Guard the compiled calls, because that fault cannot be caught by a C# build.
$null = [System.Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'MelonLoader\net6\Mono.Cecil.dll'))
$definition = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($builtDll)
try {
    $audioType = $definition.MainModule.Types | Where-Object { $_.FullName -eq 'PersonalMods.LSDPreview.PreviewAudio' }
    if (-not $audioType) { throw 'The preview audio layer is missing.' }
    foreach ($method in $audioType.Methods) {
        if (-not $method.HasBody) { continue }
        foreach ($instruction in $method.Body.Instructions) {
            $call = $instruction.Operand
            if ($call -is [Mono.Cecil.GenericInstanceMethod] -and $call.DeclaringType.FullName.StartsWith('UnityEngine.') -and
                $call.Name -match '^(GetComponent|GetComponentsInChildren|GetComponentInChildren|AddComponent|FindObjectsOfType)$') {
                throw "Audio contains an unsafe IL2CPP generic component call: $($call.FullName)"
            }
        }
    }
    Write-Output 'Checked compiled audio calls: component lookup/addition avoids IL2CPP generic instantiation.'
} finally { $definition.Dispose() }

$packageRoot = Join-Path $repoRoot 'artifacts\LSDPreview-0.5.0'
$modsDirectory = Join-Path $packageRoot 'Mods'
New-Item -ItemType Directory -Path $modsDirectory -Force | Out-Null
$dll = Join-Path $modsDirectory 'Personal.LSDPreview.dll'
Copy-Item -LiteralPath $builtDll -Destination $dll -Force
$guide = Join-Path $packageRoot 'README.md'
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs\lsd-preview.md') -Destination $guide -Force
$hashFile = Join-Path $packageRoot 'SHA256SUMS.txt'
$hashLines = @()
foreach ($entry in @(@{ Path = $dll; Name = 'Mods/Personal.LSDPreview.dll' }, @{ Path = $guide; Name = 'README.md' })) {
    $hashLines += (Get-FileHash -LiteralPath $entry.Path -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $entry.Name
}
Set-Content -LiteralPath $hashFile -Value $hashLines -Encoding utf8
$zip = Join-Path $repoRoot 'artifacts\LSDPreview-0.5.0.zip'
Compress-Archive -LiteralPath $modsDirectory, $guide, $hashFile -DestinationPath $zip -Force
Write-Output "Built and checked the separate LSD preview: $zip"
