param(
    [ValidateSet('Debug','Release')][string]$Configuration = 'Debug',
    [string]$EvidenceDirectory = (Join-Path $PSScriptRoot '../../artifacts/issue22'),
    [string]$ExecutablePath
)
$ErrorActionPreference = 'Stop'
$executable = if ($ExecutablePath) { [System.IO.Path]::GetFullPath($ExecutablePath) } else {
    Join-Path $PSScriptRoot "bin/$Configuration/net10.0-windows10.0.17763.0/BlazorFeasibility.Windows.exe"
}
if (-not (Test-Path -LiteralPath $executable)) { throw 'Build the requested research configuration first.' }
New-Item -ItemType Directory -Path $EvidenceDirectory -Force | Out-Null
$log = Join-Path $EvidenceDirectory "blazor-$($Configuration.ToLowerInvariant())-native.log"
$profileRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../artifacts/blazor-profiles'))
New-Item -ItemType Directory -Path $profileRoot -Force | Out-Null
$probeProfileDirectory = [System.IO.Path]::GetFullPath((Join-Path $profileRoot ('mfn-blazor-probe-' + [Guid]::NewGuid().ToString('N'))))
$profilePrefix = $profileRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if (-not $probeProfileDirectory.StartsWith($profilePrefix, [StringComparison]::OrdinalIgnoreCase) -or
    -not [System.IO.Path]::GetFileName($probeProfileDirectory).StartsWith('mfn-blazor-probe-', [StringComparison]::Ordinal)) {
    throw 'Refusing a profile outside this project artifact directory.'
}
try {
    # On Windows Start-Process -Wait waits for the owned process tree, including
    # WebView2 children. Waiting only for the .NET parent can leave seed files locked
    # while browser subprocesses retire. The existing cleanup bound stays unchanged.
    $errorLog = Join-Path $EvidenceDirectory "blazor-$($Configuration.ToLowerInvariant())-stderr.log"
    $process = Start-Process -FilePath $executable -ArgumentList @('--validate','--profile',('"' + $probeProfileDirectory + '"')) -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput $log -RedirectStandardError $errorLog
    try { $process.WaitForExit(); $nativeExit = $process.ExitCode }
    finally { $process.Dispose() }
}
finally {
    $cleanupError = $null
    for ($attempt = 0; $attempt -lt 20 -and (Test-Path -LiteralPath $probeProfileDirectory); ++$attempt) {
        try { Remove-Item -LiteralPath $probeProfileDirectory -Recurse -Force }
        catch { $cleanupError = $_.Exception; Start-Sleep -Milliseconds 100 }
    }
    if (Test-Path -LiteralPath $probeProfileDirectory) { throw ('Research profile cleanup failed after process exit: ' + $cleanupError.GetType().Name + ', HRESULT=' + $cleanupError.HResult) }
}
Get-Content -LiteralPath $log
if ($nativeExit -ne 0 -or -not (Select-String -LiteralPath $log -Pattern 'FEASIBILITY EXECUTED; INTERACTIVE BLAZOR NO-GO' -Quiet)) {
    throw 'Research native validation failed or did not reach its terminal decision.'
}
Write-Output 'PROFILE CLEANUP PASS after process exit; no privileged capability enabled'
