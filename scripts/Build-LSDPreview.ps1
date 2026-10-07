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
$echoDirectory = Join-Path $repoRoot 'src\LSDPreview\Assets'
$echoManifest = Get-Content -LiteralPath (Join-Path $echoDirectory 'echo.json') -Raw | ConvertFrom-Json
foreach ($inputFile in @(@{ Path = (Join-Path $echoDirectory 'echo.bundle'); Expected = $echoManifest.bundleSha256 },
    @{ Path = (Join-Path $repoRoot 'scripts\Build-LSDEcho.py'); Expected = $echoManifest.generatorSha256 })) {
    $actualHash = (Get-FileHash -LiteralPath $inputFile.Path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $inputFile.Expected) { throw 'Owned echo mixer inputs changed. Rebuild with scripts/Build-LSDEcho.py.' }
}
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
    $audioTypes = $definition.MainModule.Types | Where-Object { $_.FullName -in @('PersonalMods.LSDPreview.PreviewAudio', 'PersonalMods.LSDPreview.EchoMixerBank') }
    if ($audioTypes.Count -ne 2) { throw 'The direct native-bus echo layer is missing.' }
    if ($definition.MainModule.GetTypeReferences() | Where-Object { $_.FullName -eq 'UnityEngine.AudioEchoFilter' }) {
        throw 'The preview references the stripped AudioEchoFilter class.'
    }
    $hasMixerRoute = $hasMixerParameter = $false
    foreach ($method in @($audioTypes | ForEach-Object { $_.Methods })) {
        if (-not $method.HasBody) { continue }
        foreach ($instruction in $method.Body.Instructions) {
            $call = $instruction.Operand
            if ($call -isnot [Mono.Cecil.MethodReference]) { continue }
            if ($call.Name -eq 'set_outputAudioMixerGroup' -and $call.DeclaringType.FullName -eq 'UnityEngine.Audio.AudioMixer') { $hasMixerRoute = $true }
            if ($call.Name -eq 'SetFloat' -and $call.DeclaringType.FullName -eq 'UnityEngine.Audio.AudioMixer') { $hasMixerParameter = $true }
            if ($call.DeclaringType.FullName -eq 'PersonalMods.ModSupport' -and $call.Name -eq 'Patch') { throw 'The audio preview must not detour native playback methods.' }
            if ($call.DeclaringType.FullName -eq 'UnityEngine.AudioListener' -and $call.Name -eq 'GetOutputData') { throw 'Normal preview audio must not capture listener history.' }
            if ($call.DeclaringType.FullName -eq 'UnityEngine.AudioClip' -and $call.Name -eq 'SetData') { throw 'Normal preview audio must not rewrite PCM history.' }
            if ($call.DeclaringType.FullName -eq 'UnityEngine.AudioSource') { throw 'The direct bus route must not discover, modify or replay individual audio sources.' }
            if ($call.Name -match '^FindObjects(OfType|ByType)') { throw 'Normal preview audio must not scan scene sources.' }
            if ($call.Name -match '^TransitionToSnapshot') { throw 'The preview must not replace native mixer snapshots.' }
        }
    }
    if (-not $hasMixerRoute -or -not $hasMixerParameter) { throw 'Direct native mixer output routing or owned Echo parameter updates are missing.' }
    $previewType = $definition.MainModule.Types | Where-Object { $_.FullName -eq 'PersonalMods.LSDPreview.LSDPreviewMod' }
    $flowType = $definition.MainModule.Types | Where-Object { $_.FullName -eq 'PersonalMods.LSDPreview.SurfaceRipples' }
    $flowCalls = @($flowType.Methods | Where-Object { $_.HasBody } | ForEach-Object { $_.Body.Instructions } |
        ForEach-Object { $_.Operand } | Where-Object { $_ -is [Mono.Cecil.MethodReference] })
    if (-not ($flowCalls | Where-Object { $_.DeclaringType.FullName -eq 'UnityEngine.Graphics' -and $_.Name -eq 'DrawProceduralNow' }) -or
        -not ($flowCalls | Where-Object { $_.DeclaringType.FullName -eq 'UnityEngine.Material' -and $_.Name -eq 'SetPass' }) -or
        -not ($flowCalls | Where-Object { $_.DeclaringType.FullName -eq 'UnityEngine.ScreenCapture' -and $_.Name -eq 'CaptureScreenshotIntoRenderTexture' })) {
        throw 'The completed-frame capture or explicit shader draw is missing.'
    }
    if ($flowCalls | Where-Object { $_.DeclaringType.FullName -eq 'UnityEngine.Rendering.RenderPipelineManager' }) {
        throw 'Normal flow must use the completed-frame coroutine rather than the removed render-event experiment.'
    }
    if ($flowCalls | Where-Object { $_.DeclaringType.FullName -eq 'UnityEngine.Shader' -and $_.Name -eq 'SetGlobalFloat' }) {
        throw 'Flow orientation must not depend on the shared camera-global _FlipY.'
    }
    $presentMethod = $flowType.Methods | Where-Object { $_.Name -eq 'Present' }
    $presentCalls = @($presentMethod.Body.Instructions | ForEach-Object { $_.Operand } | Where-Object { $_ -is [Mono.Cecil.MethodReference] })
    if (-not ($presentCalls | Where-Object { $_.DeclaringType.FullName -eq 'UnityEngine.Graphics' -and $_.Name -eq 'Blit' -and $_.Parameters.Count -eq 4 })) {
        throw 'The screen-capture presentation needs its explicit texture-origin correction.'
    }
    $frameIterator = $previewType.NestedTypes | Where-Object { $_.Name -like '*RenderCompletedFrames*' }
    $iteratorCalls = @($frameIterator.Methods | Where-Object { $_.HasBody } | ForEach-Object { $_.Body.Instructions } |
        ForEach-Object { $_.Operand } | Where-Object { $_ -is [Mono.Cecil.MethodReference] })
    if (-not ($iteratorCalls | Where-Object { $_.DeclaringType.FullName -eq 'UnityEngine.WaitForEndOfFrame' -and $_.Name -eq '.ctor' })) {
        throw 'The normal flow coroutine must wait for cameras and UI to finish.'
    }
    $initializer = $previewType.Methods | Where-Object { $_.Name -eq 'OnInitializeMelon' }
    foreach ($instruction in $initializer.Body.Instructions) {
        $call = $instruction.Operand
        if ($call -is [Mono.Cecil.MethodReference] -and
            ($call.DeclaringType.FullName -in @('PersonalMods.LSDPreview.PreviewAudio', 'PersonalMods.LSDPreview.EchoMixerBank') -or
             $call.DeclaringType.FullName.StartsWith('Il2CppScheduleOne.Audio.') -or
             ($call.DeclaringType.FullName -eq 'PersonalMods.ModSupport' -and $call.Name -eq 'Patch'))) {
            throw 'The preview initializer must not initialize audio or install detours during game startup.'
        }
    }
    $shaderResource = $definition.MainModule.Resources | Where-Object { $_.Name -eq 'Personal.LSDPreview.Peripheral.bundle' }
    if (-not $shaderResource) { throw 'The authored peripheral shader bundle is missing from the DLL.' }
    $bytes = $shaderResource.GetResourceData()
    $hasher = [System.Security.Cryptography.SHA256]::Create()
    try { $embeddedHash = [BitConverter]::ToString($hasher.ComputeHash($bytes)).Replace('-', '').ToLowerInvariant() }
    finally { $hasher.Dispose() }
    if ($embeddedHash -ne $shaderManifest.bundleSha256) { throw 'The embedded shader does not match the validated authored bundle.' }
    $echoResource = $definition.MainModule.Resources | Where-Object { $_.Name -eq 'Personal.LSDPreview.Echo.bundle' }
    if (-not $echoResource) { throw 'The owned native Echo mixer bundle is missing from the DLL.' }
    $hasher = [System.Security.Cryptography.SHA256]::Create()
    try { $embeddedEchoHash = [BitConverter]::ToString($hasher.ComputeHash($echoResource.GetResourceData())).Replace('-', '').ToLowerInvariant() }
    finally { $hasher.Dispose() }
    if ($embeddedEchoHash -ne $echoManifest.bundleSha256) { throw 'The embedded Echo mixer does not match the validated bundle.' }
    Write-Output 'Checked compiled audio: direct native mixer buses and owned Echo parameters; no startup audio initialization, source scans/replays, playback detours, listener capture, PCM history writes or stripped filter; embedded assets verified.'
    Write-Output 'Checked completed-frame flow: end-of-frame coroutine, GPU capture, explicit material binding and screen draw; no render-event dependency.'
    $textType = $definition.MainModule.Types | Where-Object { $_.FullName -eq 'PersonalMods.LSDPreview.HudTextBlur' }
    $textCalls = @($textType.Methods | Where-Object { $_.HasBody } | ForEach-Object { $_.Body.Instructions } |
        ForEach-Object { $_.Operand } | Where-Object { $_ -is [Mono.Cecil.MethodReference] })
    if ($textCalls | Where-Object { $_.Name -in @('set_text', 'SetText', 'set_fontSize', 'set_characterSpacing', 'set_wordSpacing', 'set_localPosition', 'set_anchoredPosition') }) {
        throw 'Reading-focus effects must retain native wording, spacing, layout and input targets.'
    }
    if (-not ($textCalls | Where-Object { $_.DeclaringType.FullName -eq 'PersonalMods.LSDPreview.TextFocus' -and $_.Name -eq 'Sample' })) {
        throw 'The owned, independently timed reading-focus layer is missing.'
    }
    Write-Output 'Checked reading focus: owned glyph treatment; no text rewriting or layout/spacing changes.'
} finally { $definition.Dispose() }

$packageRoot = Join-Path $repoRoot 'artifacts\LSDPreview-0.11.0'
$modsDirectory = Join-Path $packageRoot 'Mods'
New-Item -ItemType Directory -Path $modsDirectory -Force | Out-Null
$dll = Join-Path $modsDirectory 'Personal.LSDPreview.dll'
Copy-Item -LiteralPath $builtDll -Destination $dll -Force
$guide = Join-Path $packageRoot 'README.md'
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs\lsd-preview.md') -Destination $guide -Force
$baselineGuide = Join-Path $packageRoot 'lsd-baseline-0.10.1.md'
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs\lsd-baseline-0.10.1.md') -Destination $baselineGuide -Force
$alcoholGuide = Join-Path $packageRoot 'future-alcohol-preview.md'
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs\future-alcohol-preview.md') -Destination $alcoholGuide -Force
$baselinesDirectory = Join-Path $packageRoot 'baselines'
New-Item -ItemType Directory -Path $baselinesDirectory -Force | Out-Null
$baselinePreferences = Join-Path $baselinesDirectory 'lsd-0.10.1.cfg'
$baselineHashes = Join-Path $baselinesDirectory 'lsd-0.10.1.sha256'
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs\baselines\lsd-0.10.1.cfg') -Destination $baselinePreferences -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs\baselines\lsd-0.10.1.sha256') -Destination $baselineHashes -Force
$hashFile = Join-Path $packageRoot 'SHA256SUMS.txt'
$hashLines = @()
foreach ($entry in @(@{ Path = $dll; Name = 'Mods/Personal.LSDPreview.dll' }, @{ Path = $guide; Name = 'README.md' },
    @{ Path = $baselineGuide; Name = 'lsd-baseline-0.10.1.md' }, @{ Path = $alcoholGuide; Name = 'future-alcohol-preview.md' },
    @{ Path = $baselinePreferences; Name = 'baselines/lsd-0.10.1.cfg' }, @{ Path = $baselineHashes; Name = 'baselines/lsd-0.10.1.sha256' })) {
    $hashLines += (Get-FileHash -LiteralPath $entry.Path -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $entry.Name
}
Set-Content -LiteralPath $hashFile -Value $hashLines -Encoding utf8
$zip = Join-Path $repoRoot 'artifacts\LSDPreview-0.11.0.zip'
Compress-Archive -LiteralPath $modsDirectory, $guide, $hashFile, $baselineGuide, $alcoholGuide, $baselinesDirectory -DestinationPath $zip -Force
Write-Output "Built and checked the separate LSD preview: $zip"
