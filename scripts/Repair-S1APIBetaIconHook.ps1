param(
    [Parameter(Mandatory=$true)][string]$InputPath,
    [Parameter(Mandatory=$true)][string]$OutputPath,
    [string]$GameDir='C:\Program Files (x86)\Steam\steamapps\common\Schedule I'
)
$ErrorActionPreference='Stop'
# Local compatibility repair for Bars 3.2.1-beta.7. Do not alter the live DLL
# in place: produce an independently testable copy and install after verification.
if ([IO.Path]::GetFullPath($InputPath) -eq [IO.Path]::GetFullPath($OutputPath)) { throw 'Input and output must be different files.' }
$null=[Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'MelonLoader\net6\Mono.Cecil.dll'))
$game=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDir 'MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll'))
$resolver=[Mono.Cecil.DefaultAssemblyResolver]::new()
foreach($directory in @((Split-Path ([IO.Path]::GetFullPath($InputPath)) -Parent),(Join-Path $GameDir 'MelonLoader\net6'),(Join-Path $GameDir 'MelonLoader\Il2CppAssemblies'))) { $resolver.AddSearchDirectory($directory) }
$reader=[Mono.Cecil.ReaderParameters]::new()
$reader.AssemblyResolver=$resolver
$api=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($InputPath,$reader)
try {
    $version=($api.CustomAttributes | Where-Object {$_.AttributeType.Name -eq 'AssemblyInformationalVersionAttribute'}).ConstructorArguments[0].Value
    if($api.Name.Name -ne 'S1API' -or $version -ne '3.2.1-beta.7+db1de3989400b32baaa62ca6a536427754caefc5') { throw "Unsupported S1API build: $version" }
    $owner='Il2CppScheduleOne.Product.ProductIconManager'
    $type=$game.MainModule.Types | Where-Object FullName -eq $owner
    if($type.Methods | Where-Object Name -eq 'GenerateIcons') { throw 'The installed game still has GenerateIcons; no beta repair is needed.' }
    $targets=@($type.Methods | Where-Object {$_.Name -eq 'GenerateRuntimeIcons' -and $_.ReturnType.FullName -eq 'UnityEngine.Sprite' -and $_.Parameters.Count -eq 1 -and $_.Parameters[0].ParameterType.FullName -eq 'System.String'})
    if($targets.Count -ne 1) { throw 'Expected native GenerateRuntimeIcons(string) -> Sprite signature not found.' }
    # IL2CPP can merge unrelated bodies at the same native address. Check the
    # actual game symbols before selecting a Harmony target.
    $rawDir=Join-Path $GameDir 'MelonLoader\Dependencies\Il2CppAssemblyGenerator\Cpp2IL\cpp2il_out'
    $native=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $rawDir 'Assembly-CSharp.dll'))
    try {
        $rawType=$native.MainModule.Types | Where-Object FullName -eq 'ScheduleOne.Product.ProductIconManager'
        $method=$rawType.Methods | Where-Object {$_.Name -eq 'GenerateRuntimeIcons' -and $_.Parameters.Count -eq 1}
        $address=$method.CustomAttributes | Where-Object {$_.AttributeType.Name -eq 'AddressAttribute'}
        $rva=($address.Fields | Where-Object Name -eq 'RVA').Argument.Value
        if(-not $rva -or $rva -eq '0x0') { throw 'Native icon target has no executable address.' }
        function Find-Aliases($types,$rva) {
            foreach($t in $types) {
                foreach($m in $t.Methods) {
                    $a=$m.CustomAttributes | Where-Object {$_.AttributeType.Name -eq 'AddressAttribute'}
                    if(($a.Fields | Where-Object Name -eq 'RVA').Argument.Value -eq $rva) { $m.FullName }
                }
                Find-Aliases $t.NestedTypes $rva
            }
        }
        $aliases=@(Find-Aliases $native.MainModule.Types $rva)
        if($aliases.Count -ne 1) { throw "Icon target shares a native body: $($aliases -join '; ')" }
    } finally { $native.Dispose() }
    $patch=$api.MainModule.Types | Where-Object FullName -eq 'S1API.Internal.Patches.ProductPackagingContentPatches'
    $changed=0
    foreach($name in @('GenerateIconsPrefix','GenerateIconsFinalizer')) {
        $hook=$patch.Methods | Where-Object Name -eq $name
        $attributes=@($hook.CustomAttributes | Where-Object {$_.AttributeType.FullName -eq 'HarmonyLib.HarmonyPatch'})
        if($attributes.Count -ne 1) { throw "Expected one target attribute on $name." }
        $attribute=$attributes[0]
        if($attribute.ConstructorArguments.Count -ne 2 -or $attribute.ConstructorArguments[0].Value.FullName -ne $owner) { throw "Unexpected patch owner on $name." }
        $old=$attribute.ConstructorArguments[1]
        if($old.Value -eq 'GenerateRuntimeIcons') { continue }
        if($old.Value -ne 'GenerateIcons') { throw "Unexpected existing target on $name." }
        $attribute.ConstructorArguments[1]=[Mono.Cecil.CustomAttributeArgument]::new($old.Type,'GenerateRuntimeIcons')
        $changed++
    }
    # Prefix/finalizer bodies and every public contract stay intact. Other
    # packaging hooks now register instead of PatchAll aborting at this class.
    $parent=Split-Path ([IO.Path]::GetFullPath($OutputPath)) -Parent
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
    $api.Write($OutputPath)
    Write-Output "S1API beta icon repair: $changed target attributes changed; native target exclusive at $rva."
} finally { $api.Dispose(); $game.Dispose(); $resolver.Dispose() }
