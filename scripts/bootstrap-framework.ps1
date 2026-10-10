param([string] $Destination = (Join-Path $PSScriptRoot '../.dependencies/framework'))
$ErrorActionPreference = 'Stop'
$frameworkCommit = '69c5ea2b20b07d9df801340c8ae9e3e1ca5dfbc3'
if (Test-Path -LiteralPath $Destination) {
    $actual = git -C $Destination rev-parse HEAD
    if ($LASTEXITCODE -ne 0 -or $actual -ne $frameworkCommit) { throw 'Destination must be a checkout at the pinned framework commit. Choose another destination; existing files will not be overwritten.' }
    if (git -C $Destination status --porcelain) { throw 'Existing framework checkout has local changes.' }
}
else {
    git clone --no-checkout https://github.com/ProGraMajster/ModernFormsNext.git $Destination
    if ($LASTEXITCODE -ne 0) { throw 'Framework clone failed.' }
    git -C $Destination checkout --detach $frameworkCommit
    if ($LASTEXITCODE -ne 0) { throw 'Pinned checkout failed.' }
}
(Resolve-Path -LiteralPath $Destination).Path
