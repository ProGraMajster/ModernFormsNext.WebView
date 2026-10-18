# Shared by bootstrap, isolated consumers and CI. Never resolve a floating branch here.
param([ValidateSet('CurrentMaster','HistoricalBaseline')][string]$Snapshot = 'CurrentMaster')
$ErrorActionPreference = 'Stop'
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../eng/framework-snapshots.json') -Raw | ConvertFrom-Json
$entry = $manifest.$Snapshot
if ($null -eq $entry -or $entry.commit -cnotmatch '^[0-9a-f]{40}$' -or
    $entry.directory -notmatch '^framework-[a-z0-9-]+$') {
    throw "Invalid frozen framework snapshot: $Snapshot"
}
$entry
