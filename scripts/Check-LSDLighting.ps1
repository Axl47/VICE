param([Parameter(Mandatory)][string]$BeforeDirectory,[Parameter(Mandatory)][string]$AfterDirectory)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$samples=Get-Content -LiteralPath (Join-Path $AfterDirectory 'lighting-samples.json') -Raw | ConvertFrom-Json
function Read-LightingSamples([string]$directory,[string]$image,[string]$model) {
    $bitmap=[System.Drawing.Bitmap]::new((Join-Path $directory $image))
    try {
        if($bitmap.Width -ne $samples.width -or $bitmap.Height -ne $samples.height){throw 'Lighting capture resolution changed.'}
        $red=$green=$blue=$count=0.0
        foreach($point in $samples.samples | Where-Object model -eq $model){
            for($dy=-2;$dy -le 2;$dy++){for($dx=-2;$dx -le 2;$dx++){
                $pixel=$bitmap.GetPixel(([int]$point.x+$dx),([int]$point.y+$dy))
                $red+=$pixel.R;$green+=$pixel.G;$blue+=$pixel.B;$count++
            }}
        }
        if($count -eq 0){throw 'No tabletop sample coordinates.'}
        @(($red/$count),($green/$count),($blue/$count))
    } finally { $bitmap.Dispose() }
}
$results=foreach($directory in @($BeforeDirectory,$AfterDirectory)){
    foreach($model in @('WorkbenchTop','Table')){
        $room=Read-LightingSamples $directory 'room-lighting.png' $model
        $warm=Read-LightingSamples $directory 'warm-key-lighting.png' $model
        $cool=Read-LightingSamples $directory 'cool-side-lighting.png' $model
        $delta=@(0..2 | ForEach-Object { $warm[$_]-$room[$_] })
        $response=($delta | ForEach-Object { [Math]::Abs($_) } | Measure-Object -Average).Average
        [pscustomobject]@{Stage=if($directory -eq $BeforeDirectory){'Before'}else{'After'};Model=$model;Room=$room;Warm=$warm;Cool=$cool;WarmResponse=$response}
    }
}
$results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $AfterDirectory 'lighting-response.json')
foreach($result in $results | Where-Object Stage -eq 'After'){
    if($result.WarmResponse -lt 5){throw "The $($result.Model) did not visibly respond to the warm light."}
}
$baseline=$results | Where-Object { $_.Stage -eq 'Before' -and $_.Model -eq 'WorkbenchTop' }
if($baseline.WarmResponse -ge 3){throw 'The baseline no longer demonstrates the unlit bench.'}
$results | Select-Object Stage,Model,@{Name='WarmResponse';Expression={[Math]::Round($_.WarmResponse,2)}} | Format-Table
Write-Output 'Passed: native screenshot samples show lighting response on both tables, replacing the unlit bench baseline.'
