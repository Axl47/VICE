[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][string]$Tag,
    [string]$RunnerDirectory = '',
    [string]$CompanionArchive = ''
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if ($Tag -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,79}$') { throw 'Use a filename-safe release tag, for example v0.3.0 or v0.3.0-beta.1.' }
if (-not $RunnerDirectory) { $RunnerDirectory = Join-Path (Split-Path $repoRoot -Parent) 'VICE-ReleaseRunner' }
$runner = (Resolve-Path -LiteralPath $RunnerDirectory).Path
$settings = Get-Content -LiteralPath (Join-Path $runner '.runner') -Raw | ConvertFrom-Json
if ($settings.gitHubUrl.TrimEnd('/') -ne 'https://github.com/Axl47/ScheduleOne-Mods') { throw 'This runner is registered to a different repository.' }
if (-not $CompanionArchive) { $CompanionArchive = $env:VICE_TV_COMPANION_ARCHIVE }
if (-not $CompanionArchive) {
    $localConfig = Join-Path $runner 'vice-release.local.json'
    if (Test-Path -LiteralPath $localConfig) { $CompanionArchive = (Get-Content -LiteralPath $localConfig -Raw | ConvertFrom-Json).CompanionArchive }
}
if (-not $CompanionArchive -or -not (Test-Path -LiteralPath $CompanionArchive -PathType Leaf)) { throw 'Supply -CompanionArchive or save its path in vice-release.local.json beside the runner.' }
if ((Get-FileHash -LiteralPath $CompanionArchive -Algorithm SHA256).Hash -ne 'FD0F421D396BEC8E8425486D4B8B6552E8EA0D0AD9443875582118C246B87712') { throw 'TV companion archive hash mismatch.' }
if (Get-Process -Name Runner.Listener -ErrorAction SilentlyContinue) { throw 'A runner is already listening. Finish or stop its session before publishing another tag.' }
Push-Location $repoRoot
try {
    $origin = git remote get-url origin
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the origin remote.' }
    $originUrl = ($origin -replace '^git@github.com:', 'https://github.com/').TrimEnd('/') -replace '\.git$', ''
    if ($originUrl -ne $settings.gitHubUrl.TrimEnd('/')) { throw 'The origin remote does not match the registered runner repository.' }
    $changes = @(git status --porcelain)
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the repository.' }
    if ($changes.Count) { throw 'Commit your changes before creating a release tag.' }
    $branch = git branch --show-current
    if ($LASTEXITCODE -ne 0 -or -not $branch) { throw 'Check out the release branch before publishing.' }
    $head = git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Could not determine the release commit.' }
    $remoteHead = @(git ls-remote origin "refs/heads/$branch")
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the remote branch.' }
    if ($remoteHead.Count -ne 1 -or ($remoteHead[0] -split '\s+')[0] -ne $head) { throw 'Push your committed branch before creating a release tag.' }
    $existingLocalTag = @(git tag --list $Tag)
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect local tags.' }
    $existingRemoteTag = @(git ls-remote origin "refs/tags/$Tag")
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect remote tags.' }
    if ($existingLocalTag.Count -or $existingRemoteTag.Count) { throw 'This tag already exists. Use a new tag; existing release tags are never moved.' }
    if (-not $PSCmdlet.ShouldProcess('Axl47/ScheduleOne-Mods', "Create and push $Tag at $head, then run one release build on this PC")) { return }
    git tag --annotate $Tag -m "VICE $Tag"
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the release tag.' }
    git push origin "refs/tags/$Tag"
    if ($LASTEXITCODE -ne 0) { throw "Tag $Tag was created locally, but its push failed. It has been preserved; fix the push before starting the runner." }
    Write-Host 'Tag pushed. Starting the local runner for one build; keep this terminal open until it exits.'
    & (Join-Path $PSScriptRoot 'Start-VICEReleaseRunner.ps1') -RunnerDirectory $runner -CompanionArchive $CompanionArchive -AssetSourceDirectory $repoRoot -Once
    Write-Host 'The local runner has stopped. Check GitHub Actions for the build result and release publication:'
    Write-Host 'https://github.com/Axl47/ScheduleOne-Mods/actions/workflows/release.yml'
} finally { Pop-Location }
