# Export already-built binaries. Use Build-VICE.ps1 for checked, fresh updates.
param(
    [ValidateSet('Commerce','Arsenal','Lifestyle','Interface','Enforcement','Syndicate')]
    [string[]]$Modules = @(),
    [string]$OutputDirectory = ''
)
& (Join-Path $PSScriptRoot 'Package-VICE.ps1') @PSBoundParameters
