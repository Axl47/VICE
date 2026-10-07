param(
    [string]$GameDir='C:\Program Files (x86)\Steam\steamapps\common\Schedule I',
    [string]$AssemblyPath=''
)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
if(-not $AssemblyPath){$AssemblyPath=Join-Path $repoRoot 'src\VICE.Enforcement\bin\Release\net6.0\VICE.Enforcement.dll'}
$null=[Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'MelonLoader\net6\Mono.Cecil.dll'))
$rawDir=Join-Path $GameDir 'MelonLoader\Dependencies\Il2CppAssemblyGenerator\Cpp2IL\cpp2il_out'
$addresses=@{}; $native=@{}
function Index-Type($type){
    foreach($method in $type.Methods){
        $attribute=$method.CustomAttributes | Where-Object {$_.AttributeType.Name -eq 'AddressAttribute'} | Select-Object -First 1
        if(-not $attribute){continue}
        $rva=($attribute.Fields | Where-Object Name -eq 'RVA').Argument.Value
        if(-not $rva -or $rva -eq '0x0'){continue}
        $key="$($type.FullName)::$($method.Name)"
        if(-not $addresses.ContainsKey($rva)){$addresses[$rva]=[Collections.Generic.List[string]]::new()}
        $addresses[$rva].Add($method.FullName)
        if(-not $native.ContainsKey($key)){$native[$key]=[Collections.Generic.List[string]]::new()}
        $native[$key].Add($rva)
    }
    foreach($nested in $type.NestedTypes){Index-Type $nested}
}
foreach($file in (Get-ChildItem -LiteralPath $rawDir -Filter '*.dll' -File)){
    $raw=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($file.FullName)
    try{foreach($type in $raw.MainModule.Types){Index-Type $type}}finally{$raw.Dispose()}
}
$game=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDir 'MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll'))
$mod=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($AssemblyPath)
try{
    if(@($mod.CustomAttributes | Where-Object {$_.AttributeType.Name -eq 'MelonInfoAttribute'}).Count -ne 1){throw 'Enforcement must register exactly one mod entry point.'}
    if(-not($mod.CustomAttributes | Where-Object {$_.AttributeType.Name -eq 'HarmonyDontPatchAllAttribute'})){throw 'Automatic PatchAll must remain disabled.'}
    if($mod.MainModule.Types | Where-Object {$_.Name -in @('RivalGang','GangTuning')}){throw 'Cartel changes must not be compiled into Enforcement.'}
    $count=0
    function Check-PatchType($type){
        foreach($attribute in ($type.CustomAttributes | Where-Object {$_.AttributeType.FullName -eq 'HarmonyLib.HarmonyPatch'})){
            if($type.Name -eq 'PursuitBehaviour_UpdateArrest_Patch'){continue} # Deliberately excluded by EnforcementMod.
            $owner=$attribute.ConstructorArguments[0].Value.FullName
            $name=$attribute.ConstructorArguments[1].Value
            $key="$($owner -replace '^Il2Cpp','')::$name"
            if(-not $native.ContainsKey($key)){throw "Missing native target: $key"}
            foreach($rva in $native[$key]){
                if($addresses[$rva].Count -ne 1){throw "Unsafe shared native target $key at $rva : $($addresses[$rva] -join '; ')"}
            }
            $target=$game.MainModule.Types | Where-Object FullName -eq $owner
            $methods=@($target.Methods | Where-Object Name -eq $name)
            if($attribute.ConstructorArguments.Count -gt 2){
                $expected=@($attribute.ConstructorArguments[2].Value | ForEach-Object {$_.Value.FullName}) -join ','
                $methods=@($methods | Where-Object {(@($_.Parameters | ForEach-Object {$_.ParameterType.FullName}) -join ',') -eq $expected})
            }
            if($methods.Count -ne 1){throw "Missing or ambiguous target signature: $key"}
            $method=$methods[0]
            foreach($hook in ($type.Methods | Where-Object Name -in @('Prefix','Postfix'))){
                foreach($parameter in $hook.Parameters){
                    $actual=$parameter.ParameterType.FullName -replace '&$',''
                    if($parameter.Name -eq '__instance'){$required=$owner}
                    elseif($parameter.Name -eq '__result'){$required=$method.ReturnType.FullName}
                    elseif($parameter.Name -like '__*'){continue}
                    else{
                        $targetParameter=$method.Parameters | Where-Object Name -eq $parameter.Name
                        if(-not $targetParameter){throw "Hook argument $($parameter.Name) missing from $key"}
                        $required=$targetParameter.ParameterType.FullName -replace '&$',''
                    }
                    if($actual -ne $required){throw "Hook argument type changed for $key / $($parameter.Name): $actual versus $required"}
                }
            }
            $script:count++
        }
        foreach($nested in $type.NestedTypes){Check-PatchType $nested}
    }
    foreach($type in $mod.MainModule.Types){Check-PatchType $type}
    Write-Output "Verified $count attribute patch signatures and exclusive native targets; one IL2CPP entry point; no cartel types."
}finally{$game.Dispose(); $mod.Dispose()}
& (Join-Path $PSScriptRoot 'Check-NativeTargets.ps1') -GameDir $GameDir -SourceDir (Join-Path $repoRoot 'src\BetterPolice') -ExcludeFiles @('RivalGang.cs')
