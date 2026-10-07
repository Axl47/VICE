param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Schedule I',
    [switch]$Install
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$sdk = Join-Path $repoRoot '.tools\dotnet-sdk-8.0.425\dotnet.exe'
if (-not (Test-Path -LiteralPath $sdk)) { $sdk = 'dotnet' }
$env:DOTNET_CLI_HOME = Join-Path $repoRoot '.tools\cli-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$buildArguments = @('build', (Join-Path $repoRoot 'src\TVProof\TVProof.csproj'), '-c', 'Release', '--nologo', '-m:1', '-v', 'minimal', "-p:GameDir=$GameDir", '-warnaserror')
$nugetConfig = Join-Path $repoRoot 'NuGet.local.config'
if (Test-Path -LiteralPath $nugetConfig) { $buildArguments += @('--configfile', $nugetConfig) }
& $sdk @buildArguments
if ($LASTEXITCODE -ne 0) { throw 'TV proof compilation failed.' }
$output = Join-Path $repoRoot 'artifacts\TVProof-0.1.1'
$mods = Join-Path $output 'Mods'
New-Item -ItemType Directory -Path $mods -Force | Out-Null
$built = Join-Path $repoRoot 'src\TVProof\bin\Release\net6.0\Personal.TVProof.dll'
Copy-Item -LiteralPath $built -Destination $mods -Force
@'
Personal TV Proof 0.1.1 - disposable local video experiment

Copy Mods/Personal.TVProof.dll into the game's Mods folder and restart the game.
Load a save, stand somewhere with a few metres of space, and press F7.
F7 creates the screen in front of you, or moves the existing screen to you.
F6 pauses/resumes. F10 removes the screen and stops its audio.
The screen stays in place and keeps playing while you walk around.
Version 0.1.1 fixes resume and waits for actual decoder preparation. The video
texture preserves the source resolution up to 4096 pixels per dimension.

Default video: C:\Users\ortaa\Videos\Moon Landing.mp4
To use another MP4, close the game and change VideoPath in the
[PersonalTVProof] category of UserData/MelonPreferences.cfg.
Use the same path-string quoting style as the existing entry.
Original video files are never copied, edited or uploaded.

This is a local-only visual experiment: no shop item, wall placement,
multiplayer synchronization, saved screens, Relay, or fullscreen interface.
No S1API dependency is added. MelonLoader and the installed IL2CPP game suffice.
Remove Personal.TVProof.dll from Mods while the game is closed to uninstall.
The prototype adds no save data and has no impact on saved furniture.

Optional developer check: launch the game with --tv-poc-check.
It tests native decoding, the textured world rectangle, pause/resume and seek
in an offscreen menu camera, writes PNGs/result.txt, then exits without loading
a save. Outputs default to UserData/PersonalTVProof/Check.
A successful check does not establish audible sound or loaded-save usability.
'@ | Set-Content -LiteralPath (Join-Path $output 'READ-ME.txt') -Encoding utf8
Compress-Archive -Path (Join-Path $output '*') -DestinationPath (Join-Path $repoRoot 'artifacts\TVProof-0.1.1.zip') -Force
if ($Install) {
    $destination = Join-Path $GameDir 'Mods\Personal.TVProof.dll'
    if (Test-Path -LiteralPath $destination) {
        if (Get-Process -Name 'Schedule I' -ErrorAction SilentlyContinue) { throw 'Close the game before replacing a loaded TV proof DLL.' }
        $backup = Join-Path $repoRoot ('artifacts\install-backups\TVProof-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
        New-Item -ItemType Directory -Path $backup -Force | Out-Null
        Copy-Item -LiteralPath $destination -Destination $backup
    }
    Copy-Item -LiteralPath $built -Destination $destination -Force
    if ((Get-FileHash -LiteralPath $built).Hash -ne (Get-FileHash -LiteralPath $destination).Hash) { throw 'Installed TV proof hash does not match the build.' }
    Write-Output 'Installed Personal.TVProof.dll. It loads on the next game launch.'
}
Write-Output (Join-Path $repoRoot 'artifacts\TVProof-0.1.1.zip')
