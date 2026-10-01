param(
    [string]$OriginalDll = 'C:\Games\Backup\Schedule I\Mods\MoreGuns.dll',
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Schedule I'
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$destination = Join-Path $repoRoot '.research\assets\moreguns.bundle'
if (-not (Test-Path -LiteralPath $OriginalDll)) { throw "Original MoreGuns.dll missing: $OriginalDll. Supply your authorized private copy with -OriginalDll." }
$cecil = Join-Path $GameDir 'MelonLoader\net6\Mono.Cecil.dll'
$null = [Reflection.Assembly]::LoadFrom($cecil)
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($OriginalDll)
try {
    $resource = $assembly.MainModule.Resources | Where-Object { $_.Name -eq 'MoreGuns.Assets.resources' }
    if ($null -eq $resource) { throw 'This DLL does not contain the expected More Guns asset resource.' }
    $reader = [Resources.ResourceReader]::new($resource.GetResourceStream())
    try {
        [string]$resourceType = ''
        [byte[]]$resourceBytes = @()
        $reader.GetResourceData('VoidanesGuns', [ref]$resourceType, [ref]$resourceBytes)
        if ($resourceType -ne 'ResourceTypeCode.ByteArray') { throw "Unexpected resource type: $resourceType" }
        $length = [BitConverter]::ToInt32($resourceBytes, 0)
        if ($length -ne $resourceBytes.Length - 4 -or $length -lt 8) { throw 'Invalid bundle resource length.' }
        [byte[]]$bundle = $resourceBytes[4..($resourceBytes.Length - 1)]
        if ([Text.Encoding]::ASCII.GetString($bundle, 0, 7) -ne 'UnityFS') { throw 'Resource is not a Unity asset bundle.' }
        $null = New-Item -ItemType Directory -Path (Split-Path $destination) -Force
        [IO.File]::WriteAllBytes($destination, $bundle)
        Write-Output "Prepared private weapon assets: $destination"
        Get-FileHash -LiteralPath $destination -Algorithm SHA256 | Select-Object Hash
    } finally { $reader.Dispose() }
} finally { $assembly.Dispose() }
