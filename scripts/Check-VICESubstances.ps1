param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Schedule I',
    [string]$BuiltDll = '',
    [switch]$AssetsOnly
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if (-not $BuiltDll) { $BuiltDll = Join-Path $repoRoot 'src\VICE.Substances\bin\Release\net6.0\VICE.Substances.dll' }
$contract = Get-Content -LiteralPath (Join-Path $repoRoot 'src\LSDGameplay\Assets\studio.contract.json') -Raw | ConvertFrom-Json
if ($contract.schema -ne 2) { throw 'Obsolete studio asset contract. Rebuild the studio artwork.' }
foreach ($entry in @($contract.bundle) + @($contract.sources)) {
    $asset = Join-Path $repoRoot $entry.path
    if ((Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256) { throw "Studio asset changed: $($entry.path). Rebuild the studio artwork." }
}
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
if ($AssetsOnly) { Write-Output 'Checked original studio, shader and echo asset contracts.'; return }
# Runtime logs exposed both missing generic audio lookups and a stripped echo
# filter. Guard compiled references; C# compilation alone accepts the wrappers.
$null = [System.Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'MelonLoader\net6\Mono.Cecil.dll'))
$definition = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($builtDll)
try {
    $studio = $definition.MainModule.Resources | Where-Object Name -eq 'Personal.LSDGameplay.Studio.bundle'
    if (-not $studio) { throw 'The accepted studio artwork is missing from Substances.' }
    $hasher = [Security.Cryptography.SHA256]::Create()
    try { $studioHash = [BitConverter]::ToString($hasher.ComputeHash($studio.GetResourceData())).Replace('-', '').ToLowerInvariant() }
    finally { $hasher.Dispose() }
    if ($studioHash -ne $contract.bundle.sha256) { throw 'The embedded studio bundle differs from the accepted art contract.' }
    foreach ($icon in Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src\LSDGameplay\Assets\Icons') -Filter '*.png' -File) {
        $resource = $definition.MainModule.Resources | Where-Object Name -eq "Personal.LSDGameplay.Icons.$($icon.BaseName).png"
        if (-not $resource) { throw "Missing workshop inventory icon: $($icon.Name)" }
        $hasher = [Security.Cryptography.SHA256]::Create()
        try { $iconHash = [BitConverter]::ToString($hasher.ComputeHash($resource.GetResourceData())).Replace('-', '') }
        finally { $hasher.Dispose() }
        if ($iconHash -ne (Get-FileHash -LiteralPath $icon.FullName -Algorithm SHA256).Hash) { throw "Embedded workshop icon differs: $($icon.Name)" }
    }
    Write-Output 'Checked accepted studio artwork and embedded inventory icons against their original assets.'
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
            if ($call.DeclaringType.FullName -in @('PersonalMods.ModSupport', 'VICE.Core.ModSupport') -and $call.Name -eq 'Patch') { throw 'The audio preview must not detour native playback methods.' }
            if ($call.DeclaringType.FullName -eq 'UnityEngine.AudioListener' -and $call.Name -eq 'GetOutputData') { throw 'Normal preview audio must not capture listener history.' }
            if ($call.DeclaringType.FullName -eq 'UnityEngine.AudioClip' -and $call.Name -eq 'SetData') { throw 'Normal preview audio must not rewrite PCM history.' }
            if ($call.DeclaringType.FullName -eq 'UnityEngine.AudioSource') { throw 'The direct bus route must not discover, modify or replay individual audio sources.' }
            if ($call.Name -match '^FindObjects(OfType|ByType)') { throw 'Normal preview audio must not scan scene sources.' }
            if ($call.Name -match '^TransitionToSnapshot') { throw 'The preview must not replace native mixer snapshots.' }
        }
    }
    if (-not $hasMixerRoute -or -not $hasMixerParameter) { throw 'Direct native mixer output routing or owned Echo parameter updates are missing.' }
    $previewType = $definition.MainModule.Types | Where-Object { $_.FullName -eq 'PersonalMods.LSDPreview.PreviewFeature' }
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
    $initializer = $previewType.Methods | Where-Object { $_.Name -eq 'Initialize' }
    foreach ($instruction in $initializer.Body.Instructions) {
        $call = $instruction.Operand
        if ($call -is [Mono.Cecil.MethodReference] -and
            ($call.DeclaringType.FullName -in @('PersonalMods.LSDPreview.PreviewAudio', 'PersonalMods.LSDPreview.EchoMixerBank') -or
             $call.DeclaringType.FullName.StartsWith('Il2CppScheduleOne.Audio.') -or
             ($call.DeclaringType.FullName -in @('PersonalMods.ModSupport', 'VICE.Core.ModSupport') -and $call.Name -eq 'Patch'))) {
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
    $eyeType = $definition.MainModule.Types | Where-Object { $_.FullName -eq 'PersonalMods.LSDPreview.ClosedEyeView' }
    $updateMethod = $previewType.Methods | Where-Object { $_.Name -eq 'OnUpdate' }
    $updateCalls = @($updateMethod.Body.Instructions | ForEach-Object { $_.Operand } | Where-Object { $_ -is [Mono.Cecil.MethodReference] })
    if (-not $eyeType -or -not ($updateCalls | Where-Object { $_.Name -eq 'get_bKey' }) -or
        -not ($updateCalls | Where-Object { $_.DeclaringType.FullName -eq 'PersonalMods.LSDPreview.ClosedEyeView' -and $_.Name -eq 'Step' })) {
        throw 'The held-B closed-eye control and transition state are missing.'
    }
    Write-Output 'Checked closed eyes: held B input and guarded transition state; patterns share the owned completed-frame shader.'
} finally { $definition.Dispose() }
