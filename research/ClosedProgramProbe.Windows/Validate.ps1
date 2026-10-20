param(
    [Parameter(Mandatory)][string]$FrameworkRoot,
    [ValidateSet('CurrentMaster','HistoricalBaseline')][string]$Snapshot='CurrentMaster',
    [ValidateSet('Debug','Release')][string]$Configuration='Debug',
    [string]$EvidenceDirectory=(Join-Path $PSScriptRoot ('../../artifacts/closed-program/'+[Guid]::NewGuid().ToString('N')))
)
$ErrorActionPreference='Stop'
if(!$IsWindows){throw 'Native Windows required'}
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$expected=& (Join-Path $repo 'scripts/get-framework-snapshot.ps1') -Snapshot $Snapshot
$framework=(git -C $FrameworkRoot rev-parse HEAD | Out-String).Trim()
if($framework -ne $expected.commit -or (git -C $FrameworkRoot status --porcelain)){throw 'Expected clean frozen framework root'}
$executable=Join-Path $PSScriptRoot "bin/$Configuration/net10.0-windows10.0.17763.0/ClosedProgramProbe.Windows.exe"
if(!(Test-Path -LiteralPath $executable)){throw 'Build the selected research configuration first'}
New-Item -ItemType Directory -Path $EvidenceDirectory -Force | Out-Null
$profileRoot=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)+[IO.Path]::DirectorySeparatorChar
$profile=[IO.Path]::GetFullPath((Join-Path $profileRoot ('mfn-closed-counter-'+[Guid]::NewGuid().ToString('N'))))
function VerifyOwnedProfile {
    $absolute=[IO.Path]::GetFullPath($profile)
    if(!$absolute.StartsWith($profileRoot,[StringComparison]::OrdinalIgnoreCase) -or ![IO.Path]::GetFileName($absolute).StartsWith('mfn-closed-counter-',[StringComparison]::Ordinal)){throw 'Profile path escaped the owned temporary root'}
}
VerifyOwnedProfile
if(Test-Path -LiteralPath $profile){throw 'Unique profile already exists'}
$log=Join-Path $EvidenceDirectory 'native.log'
$result=[ordered]@{status='RUNNING';startUtc=[DateTime]::UtcNow.ToString('o');head=(git -C $repo rev-parse HEAD | Out-String).Trim();dirty=[bool](git -C $repo status --porcelain);framework=$framework;snapshot=$Snapshot;configuration=$Configuration;assemblySHA256=(Get-FileHash ([IO.Path]::ChangeExtension($executable,'dll'))).Hash;dotnetSdk=(& dotnet --version | Out-String).Trim();profileCleaned=$false}
$process=$null
try {
    # Process-tree wait precedes the unchanged bounded deletion; environment-owned
    # child processes can legitimately retain the profile until application exit.
    $process=Start-Process -FilePath $executable -ArgumentList @('--validate','--profile',('"'+$profile+'"')) -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput $log -RedirectStandardError (Join-Path $EvidenceDirectory 'stderr.log')
    $process.WaitForExit();$result.exitCode=$process.ExitCode
    if($process.ExitCode -ne 0 -or !(Select-String -LiteralPath $log -Pattern 'CLOSED EXPERIMENT EXECUTED; NARROW DIAGNOSTIC GO' -Quiet)){throw 'Native closed-program experiment did not qualify; inspect actual NO-GO evidence'}
    $result.status='PASS'
}catch{$result.status='FAIL';$result.failure=$_.Exception.Message;throw}
finally {
    if($process){$process.Dispose()}
    try {
        VerifyOwnedProfile
        for($attempt=0;$attempt -lt 20 -and (Test-Path -LiteralPath $profile);++$attempt){
            try{[IO.Directory]::Delete($profile,$true)}
            catch [IO.IOException]{Start-Sleep -Milliseconds 100}
            catch [UnauthorizedAccessException]{Start-Sleep -Milliseconds 100}
        }
        if(Test-Path -LiteralPath $profile){throw 'Owned profile cleanup failed after process-tree exit'}
        $result.profileCleaned=$true
    }catch{$result.status='FAIL';$result.failure=$_.Exception.Message;throw}
    finally{$result.endUtc=[DateTime]::UtcNow.ToString('o');$result | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $EvidenceDirectory 'result.json');Get-Content -LiteralPath $log}
}
