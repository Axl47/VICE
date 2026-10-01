param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Schedule I',
    [string]$SourceDir = ''
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if (-not $SourceDir) { $SourceDir = Join-Path $repoRoot 'src' }
$null = [Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'MelonLoader\net6\Mono.Cecil.dll'))
$rawDir = Join-Path $GameDir 'MelonLoader\Dependencies\Il2CppAssemblyGenerator\Cpp2IL\cpp2il_out'
if (-not (Test-Path -LiteralPath (Join-Path $rawDir 'Assembly-CSharp.dll'))) {
    throw 'Cpp2IL metadata is required to check shared native patch targets. Regenerate the MelonLoader assemblies first.'
}
$functions = @{}
$methods = @{}
function Index-NativeType($type) {
    foreach ($method in $type.Methods) {
        $address = $null
        foreach ($attribute in $method.CustomAttributes) {
            if ($attribute.AttributeType.Name -eq 'AddressAttribute') { $address = $attribute; break }
        }
        if (-not $address) { continue }
        $rva = $null
        foreach ($field in $address.Fields) { if ($field.Name -eq 'RVA') { $rva = [string]$field.Argument.Value; break } }
        if (-not $rva -or $rva -eq '0x0') { continue }
        $key = "$($type.FullName)::$($method.Name)"
        $parameterNames = [Collections.Generic.List[string]]::new()
        foreach ($parameter in $method.Parameters) { $parameterNames.Add($parameter.ParameterType.FullName) }
        $entry = [pscustomobject]@{
            RVA=$rva; Type=$type.FullName; Name=$method.Name; Signature=$method.FullName
            Parameters=([string]::Join(',', $parameterNames)); ReturnType=$method.ReturnType.FullName
        }
        if (-not $functions.ContainsKey($rva)) { $functions[$rva] = [Collections.Generic.List[object]]::new() }
        if (-not $methods.ContainsKey($key)) { $methods[$key] = [Collections.Generic.List[object]]::new() }
        $functions[$rva].Add($entry); $methods[$key].Add($entry)
    }
    foreach ($nested in $type.NestedTypes) { Index-NativeType $nested }
}
# Small IL2CPP bodies can also be shared with framework methods and constructors.
foreach ($file in (Get-ChildItem -LiteralPath $rawDir -Filter '*.dll' -File)) {
    $raw = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($file.FullName)
    try { foreach ($type in $raw.MainModule.Types) { Index-NativeType $type } }
    finally { $raw.Dispose() }
}
function Is-ReviewedAlias($key, $aliases) {
    if ($aliases.Count -ne 2) { return $false }
    # Identical signatures across different native owner types are still unsafe:
    # the IL2CPP trampoline marshals the owner before any Harmony hook can cast it.
    if ($key -eq 'ScheduleOne.Economy.Customer::SetOfferedContract') {
        # FishNet's generated writer has the same owner, arguments and behavior.
        foreach ($alias in $aliases) {
            if ($alias.Type -ne 'ScheduleOne.Economy.Customer' -or $alias.ReturnType -ne 'System.Void' -or $alias.Parameters -ne 'ScheduleOne.Quests.ContractInfo,ScheduleOne.GameTime.GameDateTime' -or ($alias.Name -ne 'SetOfferedContract' -and $alias.Name -notlike 'RpcWriter___Observers_SetOfferedContract_*')) { return $false }
        }
        return $true
    }
    return $false
}
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDir 'MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll'))
try {
    $count = 0; $reviewed = [Collections.Generic.HashSet[string]]::new()
    $sources = Get-ChildItem -LiteralPath $SourceDir -Recurse -Filter '*.cs' | Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' }
    foreach ($file in $sources) {
        $source = Get-Content -LiteralPath $file.FullName -Raw
        foreach ($match in [regex]::Matches($source, 'ModSupport\.Patch\([^,]+,\s*typeof\(([^)]+)\),\s*"([^"]+)"')) {
            $name = $match.Groups[1].Value; $methodName = $match.Groups[2].Value
            $type = $assembly.MainModule.Types | Where-Object { $_.Name -eq $name -and $_.Namespace -like 'Il2CppScheduleOne*' }
            if (@($type).Count -ne 1) { throw "Ambiguous or missing patch type: $name" }
            $key = "$($type.FullName -replace '^Il2Cpp', '')::$methodName"
            if (-not $methods.ContainsKey($key)) { throw "Native patch address missing: $key" }
            foreach ($method in $methods[$key]) {
                $aliases = $functions[$method.RVA]
                if ($aliases.Count -gt 1) {
                    if (-not (Is-ReviewedAlias $key $aliases)) {
                        $examples = [string]::Join('; ', @($aliases | Select-Object -First 3 -ExpandProperty Signature))
                        throw "Unsafe shared native patch target $key at $($method.RVA) ($($aliases.Count) methods): $examples"
                    }
                    $null = $reviewed.Add($key)
                }
            }
            $count++
        }
    }
    Write-Output "Verified native address sharing for $count patch declarations; $($reviewed.Count) narrowly reviewed shared targets."
} finally { $assembly.Dispose() }
