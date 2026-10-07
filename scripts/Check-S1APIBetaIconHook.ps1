param(
    [Parameter(Mandatory=$true)][string]$OriginalPath,
    [Parameter(Mandatory=$true)][string]$RepairedPath,
    [string]$GameDir='C:\Program Files (x86)\Steam\steamapps\common\Schedule I'
)
$ErrorActionPreference='Stop'
$null=[Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'MelonLoader\net6\Mono.Cecil.dll'))
function Get-Attributes($attributes) {
    foreach($attribute in $attributes) {
        $attribute.AttributeType.FullName+':'+[Convert]::ToBase64String($attribute.GetBlob())
    }
}
function Get-Contract($assembly) {
    'Assembly:'+ $assembly.Name.FullName
    Get-Attributes $assembly.CustomAttributes
    foreach($reference in $assembly.MainModule.AssemblyReferences) { 'Reference:'+ $reference.FullName }
    foreach($resource in $assembly.MainModule.Resources) {
        'Resource:'+ $resource.Name+':'+$resource.Attributes
        if($resource -is [Mono.Cecil.EmbeddedResource]) {
            'Data:'+ [Convert]::ToBase64String($resource.GetResourceData())
        }
    }
    function Get-Types($types) {
        foreach($type in $types) {
            'Type:'+ $type.FullName+':'+$type.Attributes+':'+$type.BaseType
            Get-Attributes $type.CustomAttributes
            foreach($interface in $type.Interfaces) { 'Interface:'+ $interface.InterfaceType.FullName }
            foreach($field in $type.Fields) {
                'Field:'+ $field.FullName+':'+$field.Attributes+':'+$field.Constant
                Get-Attributes $field.CustomAttributes
            }
            foreach($method in $type.Methods) {
                'Method:'+ $method.FullName+':'+$method.Attributes+':'+$method.ImplAttributes
                foreach($parameter in $method.Parameters) {
                    'Parameter:'+ $parameter.Name+':'+$parameter.Attributes+':'+$parameter.Constant
                    Get-Attributes $parameter.CustomAttributes
                }
                $attributes=$method.CustomAttributes
                if($type.FullName -eq 'S1API.Internal.Patches.ProductPackagingContentPatches' -and $method.Name -in @('GenerateIconsPrefix','GenerateIconsFinalizer')) {
                    $attributes=@($attributes | Where-Object {$_.AttributeType.FullName -ne 'HarmonyLib.HarmonyPatch'})
                }
                Get-Attributes $attributes
                if($method.HasBody) {
                    'Body:'+ $method.Body.InitLocals+':'+$method.Body.MaxStackSize
                    foreach($variable in $method.Body.Variables) { 'Variable:'+ $variable.VariableType.FullName }
                    foreach($instruction in $method.Body.Instructions) { $instruction.ToString() }
                    foreach($handler in $method.Body.ExceptionHandlers) {
                        'Handler:'+ $handler.HandlerType+':'+$handler.TryStart+':'+$handler.TryEnd+':'+$handler.HandlerStart+':'+$handler.HandlerEnd+':'+$handler.CatchType
                    }
                }
            }
            foreach($property in $type.Properties) { 'Property:'+ $property.FullName+':'+$property.Attributes; Get-Attributes $property.CustomAttributes }
            foreach($event in $type.Events) { 'Event:'+ $event.FullName+':'+$event.Attributes; Get-Attributes $event.CustomAttributes }
            Get-Types $type.NestedTypes
        }
    }
    Get-Types $assembly.MainModule.Types
}
$original=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($OriginalPath)
$repaired=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($RepairedPath)
try {
    $before=@(Get-Contract $original)
    $after=@(Get-Contract $repaired)
    if($before.Count -ne $after.Count) { throw 'S1API contract record count changed.' }
    for($i=0;$i -lt $before.Count;$i++) { if($before[$i] -cne $after[$i]) { throw "Unexpected S1API change at record $i : $($before[$i]) -> $($after[$i])" } }
    $patch=$repaired.MainModule.Types | Where-Object FullName -eq 'S1API.Internal.Patches.ProductPackagingContentPatches'
    foreach($name in @('GenerateIconsPrefix','GenerateIconsFinalizer')) {
        $hook=$patch.Methods | Where-Object Name -eq $name
        $target=$hook.CustomAttributes | Where-Object {$_.AttributeType.FullName -eq 'HarmonyLib.HarmonyPatch'}
        if($target.ConstructorArguments[1].Value -ne 'GenerateRuntimeIcons') { throw "Wrong repaired target on $name." }
    }
    Write-Output "S1API repair verified: $($before.Count) metadata/body/resource records unchanged; only the two expected Harmony target attributes differ."
} finally { $original.Dispose(); $repaired.Dispose() }
