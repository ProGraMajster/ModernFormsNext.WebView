# Keep the dependency outside this repository: otherwise NuGet searches upward and
# accidentally applies this extension's central package versions to the pinned framework.
param(
    [string]$Destination,
    # Preserve the original no-argument contract; new development selects CurrentMaster explicitly.
    [ValidateSet('CurrentMaster','HistoricalBaseline')][string]$Snapshot = 'HistoricalBaseline'
)
$ErrorActionPreference = 'Stop'
$entry = & (Join-Path $PSScriptRoot 'get-framework-snapshot.ps1') -Snapshot $Snapshot
$frameworkCommit = $entry.commit
if (-not $Destination) { $Destination = Join-Path $PSScriptRoot "../../.modernformsnext-webview/$($entry.directory)" }
$Destination = [IO.Path]::GetFullPath($Destination)
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')).TrimEnd([IO.Path]::DirectorySeparatorChar)
if ($Destination.Equals($repositoryRoot, [StringComparison]::OrdinalIgnoreCase) -or
    $Destination.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Framework sources must be outside the WebView repository to isolate central package versions.'
}
if (Test-Path -LiteralPath $Destination) {
    $actual = git -C $Destination rev-parse HEAD
    if ($LASTEXITCODE -ne 0 -or $actual -ne $frameworkCommit) { throw 'Destination must be a checkout at the pinned framework commit. Choose another destination; existing files will not be overwritten.' }
    $changes = git -C $Destination status --porcelain
    if ($LASTEXITCODE -ne 0 -or $changes) { throw 'Existing framework checkout is unreadable or has local changes.' }
}
else {
    git clone --filter=blob:none --no-checkout https://github.com/ProGraMajster/ModernFormsNext.git $Destination
    if ($LASTEXITCODE -ne 0) { throw 'Framework clone failed.' }
    git -C $Destination checkout --detach $frameworkCommit
    if ($LASTEXITCODE -ne 0) { throw 'Pinned checkout failed.' }
}
(Resolve-Path -LiteralPath $Destination).Path
