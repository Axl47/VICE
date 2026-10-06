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
$shaderDirectory = Join-Path $repoRoot 'src\LSDPreview\Assets'
$shaderManifest = Get-Content -LiteralPath (Join-Path $shaderDirectory 'peripheral.json') -Raw | ConvertFrom-Json
foreach ($inputFile in @(@{ Name = 'PeripheralWarp.hlsl'; Expected = $shaderManifest.sourceSha256 },
    @{ Name = 'peripheral.bundle'; Expected = $shaderManifest.bundleSha256 })) {
    $actualHash = (Get-FileHash -LiteralPath (Join-Path $shaderDirectory $inputFile.Name) -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $inputFile.Expected) { throw 'Authored peripheral shader inputs changed. Rebuild with scripts/Build-LSDShader.py.' }
}
$projects = @('src\LSDPreview\LSDPreview.csproj', 'tests\LSDPreview\LSDPreview.Tests.csproj')
foreach ($project in $projects) {
    $buildArguments = @('build', (Join-Path $repoRoot $project), '-c', 'Release', '--nologo', '-m:1', '-v', 'minimal', "-p:GameDir=$GameDir", '-warnaserror')
    if ($NugetConfig) { $buildArguments += @('--configfile', $NugetConfig) }
    & $DotnetPath @buildArguments
    if ($LASTEXITCODE -ne 0) { throw "LSD preview compilation failed: $project" }
}
& $DotnetPath run --project (Join-Path $repoRoot 'tests\LSDPreview\LSDPreview.Tests.csproj') -c Release --no-build --no-restore
if ($LASTEXITCODE -ne 0) { throw 'LSD preview timing, visibility, displacement calibration, color ownership and audio checks failed.' }

$builtDll = Join-Path $repoRoot 'src\LSDPreview\bin\Release\net6.0\Personal.LSDPreview.dll'
# Runtime logs exposed both missing generic audio lookups and a stripped echo
# filter. Guard compiled references; C# compilation alone accepts the wrappers.
$null = [System.Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'MelonLoader\net6\Mono.Cecil.dll'))
$definition = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($builtDll)
try {
    $audioType = $definition.MainModule.Types | Where-Object { $_.FullName -eq 'PersonalMods.LSDPreview.PreviewAudio' }
    if (-not $audioType) { throw 'The preview audio layer is missing.' }
    if ($definition.MainModule.GetTypeReferences() | Where-Object { $_.FullName -eq 'UnityEngine.AudioEchoFilter' }) {
        throw 'The preview references the stripped AudioEchoFilter class.'
    }
    $hasListenerCapture = $hasClipWrite = $false
    foreach ($method in $audioType.Methods) {
        if (-not $method.HasBody) { continue }
        foreach ($instruction in $method.Body.Instructions) {
            $call = $instruction.Operand
            if ($call -is [Mono.Cecil.MethodReference] -and $call.Name -eq 'GetOutputData' -and $call.DeclaringType.FullName -eq 'UnityEngine.AudioListener') { $hasListenerCapture = $true }
            if ($call -is [Mono.Cecil.MethodReference] -and $call.Name -eq 'SetData' -and $call.DeclaringType.FullName -eq 'UnityEngine.AudioClip') { $hasClipWrite = $true }
            if ($call -is [Mono.Cecil.MethodReference] -and $call.Name -eq 'TransitionToSnapshots') { throw 'The preview must not replace native mixer snapshots.' }
            if ($call -is [Mono.Cecil.GenericInstanceMethod] -and $call.DeclaringType.FullName.StartsWith('UnityEngine.') -and
                $call.Name -match '^(GetComponent|GetComponentsInChildren|GetComponentInChildren|AddComponent|FindObjectsOfType)$') {
                throw "Audio contains an unsafe IL2CPP generic component call: $($call.FullName)"
            }
        }
    }
    if (-not $hasListenerCapture -or -not $hasClipWrite) { throw 'The actual listener capture or delayed PCM write is missing.' }
    $shaderResource = $definition.MainModule.Resources | Where-Object { $_.Name -eq 'Personal.LSDPreview.Peripheral.bundle' }
    if (-not $shaderResource) { throw 'The authored peripheral shader bundle is missing from the DLL.' }
    $bytes = $shaderResource.GetResourceData()
    $hasher = [System.Security.Cryptography.SHA256]::Create()
    try { $embeddedHash = [BitConverter]::ToString($hasher.ComputeHash($bytes)).Replace('-', '').ToLowerInvariant() }
    finally { $hasher.Dispose() }
    if ($embeddedHash -ne $shaderManifest.bundleSha256) { throw 'The embedded shader does not match the validated authored bundle.' }
    Write-Output 'Checked compiled audio: actual listener capture and PCM writes, no stripped filter or generic audio component call; authored peripheral shader verified.'
} finally { $definition.Dispose() }

$packageRoot = Join-Path $repoRoot 'artifacts\LSDPreview-0.7.0'
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
$zip = Join-Path $repoRoot 'artifacts\LSDPreview-0.7.0.zip'
Compress-Archive -LiteralPath $modsDirectory, $guide, $hashFile -DestinationPath $zip -Force
Write-Output "Built and checked the separate LSD preview: $zip"
