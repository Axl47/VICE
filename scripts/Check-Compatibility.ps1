param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Schedule I')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$null = [Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'MelonLoader\net6\Mono.Cecil.dll'))
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDir 'MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll'))
try {
    $count = 0
    $sources = Get-ChildItem (Join-Path $repoRoot 'src') -Recurse -Filter '*.cs' | Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' }
    foreach ($file in $sources) {
        $source = Get-Content -LiteralPath $file.FullName -Raw
        foreach ($match in [regex]::Matches($source, 'ModSupport\.Patch\([^,]+,\s*typeof\(([^)]+)\),\s*"([^"]+)"')) {
            $name = $match.Groups[1].Value
            $methodName = $match.Groups[2].Value
            $type = $assembly.MainModule.Types | Where-Object { $_.Name -eq $name -and $_.Namespace -like 'Il2CppScheduleOne*' }
            if (@($type).Count -ne 1) { throw "Ambiguous or missing patch type: $name" }
            if (-not ($type.Methods | Where-Object { $_.Name -eq $methodName })) { throw "Native patch target missing: $name.$methodName" }
            $count++
        }
    }
    Write-Output "Verified $count explicit native patch targets. Runtime detours still require an in-game check."
} finally { $assembly.Dispose() }
