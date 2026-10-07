param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Schedule I',
    [string]$DotnetPath = 'dotnet',
    [switch]$Install
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$portableSdk = Join-Path $repoRoot '.tools/dotnet-sdk-8.0.425/dotnet.exe'
if ($DotnetPath -eq 'dotnet' -and (Test-Path -LiteralPath $portableSdk)) { $DotnetPath = $portableSdk }
$env:DOTNET_CLI_HOME = Join-Path $repoRoot '.tools/cli-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$provenance = Get-Content -LiteralPath (Join-Path $repoRoot 'src/S1LSDComparison/Assets/provenance.json') -Raw | ConvertFrom-Json
foreach ($asset in @(
    @{ Name = 's1lsd_shaders.bundle'; Hash = $provenance.shaderBundleSha256 },
    @{ Name = 's1lsd_shaders.original.bundle'; Hash = $provenance.originalShaderBundleSha256 },
    @{ Name = 'lsd.default.json'; Hash = $provenance.defaultConfigSha256 }
)) {
    $path = Join-Path $repoRoot "src/S1LSDComparison/Assets/$($asset.Name)"
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $asset.Hash) { throw "Original asset changed: $path" }
}
foreach ($project in @('src/S1LSDComparison/S1LSDComparison.csproj', 'tests/S1LSDComparison/S1LSDComparison.Tests.csproj')) {
    $arguments = @('build', (Join-Path $repoRoot $project), '-c', 'Release', '--nologo', '-m:1', '-v', 'minimal', '-warnaserror', "-p:GameDir=$GameDir")
    if (Test-Path -LiteralPath (Join-Path $repoRoot 'NuGet.local.config')) { $arguments += @('--configfile', (Join-Path $repoRoot 'NuGet.local.config')) }
    & $DotnetPath @arguments
    if ($LASTEXITCODE -ne 0) { throw "Comparison compilation failed: $project" }
}
& $DotnetPath run --project (Join-Path $repoRoot 'tests/S1LSDComparison/S1LSDComparison.Tests.csproj') -c Release --no-build --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Comparison dose/render-target checks failed.' }
$builtDll = Join-Path $repoRoot 'src/S1LSDComparison/bin/Release/net6.0/Personal.S1LSDComparison.dll'
$null = [System.Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'MelonLoader/net6/Mono.Cecil.dll'))
$definition = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($builtDll)
try {
    if ($definition.MainModule.AssemblyReferences | Where-Object { $_.Name -in @('S1LSD', 'S1API', 'Personal.LSDPreview') }) { throw 'Comparison must be independently loadable.' }
    if ($definition.MainModule.GetTypeReferences() | Where-Object { $_.FullName -match '^S1LSD\.(Content|Persistence|Patches|Runtime)\.' -or $_.FullName -eq 'UnityEngine.AudioEchoFilter' }) { throw 'Original runtime/content/persistence or stripped audio filter found.' }
    if ($definition.MainModule.GetTypeReferences() | Where-Object { $_.FullName -in @('UnityEngine.GUI', 'UnityEngine.GUILayout', 'UnityEngine.GUIContent') }) { throw 'Comparison controls must not use stripped IMGUI APIs.' }
    foreach ($control in @('UnityEngine.UI.Button', 'UnityEngine.UI.GraphicRaycaster', 'Il2CppTMPro.TextMeshProUGUI')) {
        if (-not ($definition.MainModule.GetTypeReferences() | Where-Object FullName -eq $control)) { throw "Missing native panel control: $control" }
    }
    foreach ($resourceCheck in @(
        @{ Name = 'Comparison.Shaders.bundle'; Hash = $provenance.shaderBundleSha256 },
        @{ Name = 'Comparison.Defaults.json'; Hash = $provenance.defaultConfigSha256 }
    )) {
        $resource = $definition.MainModule.Resources | Where-Object { $_.Name -eq $resourceCheck.Name }
        if (-not $resource) { throw "Missing embedded asset: $($resourceCheck.Name)" }
        $hasher = [System.Security.Cryptography.SHA256]::Create()
        try { $hash = [BitConverter]::ToString($hasher.ComputeHash($resource.GetResourceData())).Replace('-', '').ToLowerInvariant() }
        finally { $hasher.Dispose() }
        if ($hash -ne $resourceCheck.Hash) { throw "Changed embedded asset: $($resourceCheck.Name)" }
    }
    $renderer = $definition.MainModule.Types | Where-Object { $_.FullName -eq 'PersonalMods.S1LSDComparison.ComparisonRenderer' }
    $calls = @($renderer.Methods | Where-Object HasBody | ForEach-Object { $_.Body.Instructions } | ForEach-Object { $_.Operand } | Where-Object { $_ -is [Mono.Cecil.MethodReference] })
    foreach ($required in @('CaptureScreenshotIntoRenderTexture', 'DrawProceduralNow', 'SetPass')) {
        if (-not ($calls | Where-Object Name -eq $required)) { throw "Missing render binding: $required" }
    }
    if ($calls | Where-Object { $_.DeclaringType.FullName -eq 'UnityEngine.Rendering.RenderPipelineManager' }) { throw 'Comparison must not depend on camera rendering callbacks.' }
    $lease = $definition.MainModule.Types | Where-Object { $_.FullName -eq 'PersonalMods.S1LSDComparison.BaselineLease' }
    $leaseCalls = @($lease.Methods | Where-Object HasBody | ForEach-Object { $_.Body.Instructions } | ForEach-Object { $_.Operand } | Where-Object { $_ -is [Mono.Cecil.MethodReference] })
    if ($leaseCalls | Where-Object { $_.DeclaringType.FullName -match 'MelonPreferences|System\.IO\.' }) { throw 'Baseline isolation must not change preferences or files.' }
    Write-Output 'PASS: original embedded assets verified; no original gameplay/persistence runtime, audio filter, camera callbacks, or baseline preference/file writes.'
} finally { $definition.Dispose() }
$packageRoot = Join-Path $repoRoot 'artifacts/S1LSDComparison-0.1.1'
$null = New-Item -ItemType Directory -Force -Path (Join-Path $packageRoot 'Mods'), (Join-Path $packageRoot 'licenses/S1LSD')
Copy-Item -LiteralPath $builtDll -Destination (Join-Path $packageRoot 'Mods/Personal.S1LSDComparison.dll')
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/s1lsd-comparison.md') -Destination (Join-Path $packageRoot 'README.md')
Copy-Item -LiteralPath (Join-Path $repoRoot 'licenses/S1LSD/LICENSE.txt'), (Join-Path $repoRoot 'licenses/S1LSD/THIRD_PARTY_NOTICES.txt') -Destination (Join-Path $packageRoot 'licenses/S1LSD')
Compress-Archive -Path (Join-Path $packageRoot '*') -DestinationPath "$packageRoot.zip" -Force
Write-Output "Packaged: $packageRoot.zip"
if ($Install) {
    if (Get-Process -Name 'Schedule I' -ErrorAction SilentlyContinue) { throw 'Package built. Close Schedule I before installing the comparison DLL.' }
    if (Test-Path -LiteralPath (Join-Path $GameDir 'Mods/S1LSD.dll')) { throw 'The original S1LSD.dll is installed; remove it before using the isolated comparison.' }
    $destination = Join-Path $GameDir 'Mods/Personal.S1LSDComparison.dll'
    if (Test-Path -LiteralPath $destination) {
        $backup = Join-Path $repoRoot ('artifacts/s1lsd-comparison-backups/' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
        $null = New-Item -ItemType Directory -Force -Path $backup
        Copy-Item -LiteralPath $destination -Destination $backup
    }
    Copy-Item -LiteralPath $builtDll -Destination $destination
    if ((Get-FileHash -LiteralPath $destination).Hash -ne (Get-FileHash -LiteralPath $builtDll).Hash) { throw 'Installed comparison hash mismatch.' }
    Write-Output "Installed: $destination"
}
