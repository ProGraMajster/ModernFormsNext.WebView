param(
    [Parameter(Mandatory)][string]$FrameworkRoot,
    [ValidateSet('CurrentMaster','HistoricalBaseline')][string]$Snapshot='CurrentMaster',
    [ValidateSet('Debug','Release')][string]$Configuration='Debug',
    [string[]]$Arguments=@('--validate'),
    [string]$EvidenceDirectory=(Join-Path $PSScriptRoot ('../artifacts/native-windows/'+[Guid]::NewGuid().ToString('N')))
)
$ErrorActionPreference='Stop'
if(!$IsWindows){throw 'Native Windows validation requires Windows'}
$allowed=@('--validate','--validate-popup','--popup-gesture','--validate-security','--validate-keyboard','--validate-desktop')
if(!$Arguments.Count -or @($Arguments | Where-Object {$_ -notin $allowed}).Count){throw 'Only explicit sample validation modes are accepted'}
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$entry=& (Join-Path $PSScriptRoot 'get-framework-snapshot.ps1') -Snapshot $Snapshot
$frameworkCommit=(git -C $FrameworkRoot rev-parse HEAD | Out-String).Trim()
if($LASTEXITCODE -or $frameworkCommit -ne $entry.commit -or (git -C $FrameworkRoot status --porcelain)){throw 'Expected the selected clean frozen framework snapshot'}
$executable=Join-Path $repo "samples/ModernFormsNext.WebView.Sample.Windows/bin/$Configuration/net10.0-windows10.0.17763.0/ModernFormsNext.WebView.Sample.Windows.exe"
if(!(Test-Path -LiteralPath $executable)){throw 'Build the selected Windows sample first'}
New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
$profileRoot=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)+[IO.Path]::DirectorySeparatorChar
$profile=[IO.Path]::GetFullPath((Join-Path $profileRoot ('mfn-webview-'+[Guid]::NewGuid().ToString('N'))))
if(!$profile.StartsWith($profileRoot,[StringComparison]::OrdinalIgnoreCase) -or ![IO.Path]::GetFileName($profile).StartsWith('mfn-webview-',[StringComparison]::Ordinal) -or (Test-Path -LiteralPath $profile)){throw 'Refusing a profile outside the unique test-owned temporary directory'}
$log=Join-Path $EvidenceDirectory 'windows-native.log'
$errorLog=Join-Path $EvidenceDirectory 'windows-stderr.log'
$result=[ordered]@{status='RUNNING';startUtc=[DateTime]::UtcNow.ToString('o');configuration=$Configuration;snapshot=$Snapshot;frameworkCommit=$frameworkCommit;webViewCommit=(git -C $repo rev-parse HEAD | Out-String).Trim();workingTreeDirty=[bool](git -C $repo status --porcelain);arguments=$Arguments;sampleSHA256=(Get-FileHash -LiteralPath $executable).Hash;assemblySHA256=(Get-FileHash -LiteralPath ([IO.Path]::ChangeExtension($executable,'dll'))).Hash;profileCleaned=$false}
$process=$null
try {
    # Same process-tree wait used by the existing research validators. Native
    # environment references live until application exit. Keep the original 20x100ms
    # filesystem cleanup bound, but start it AFTER the owned engine processes exit.
    $nativeArguments=@($Arguments)+@('--profile',('"'+$profile+'"'))
    $process=Start-Process -FilePath $executable -ArgumentList $nativeArguments -WorkingDirectory $repo -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput $log -RedirectStandardError $errorLog
    $process.WaitForExit();$result.exitCode=$process.ExitCode
    $terminal=if($Arguments -contains '--validate-popup'){'POPUP PASS'}else{'RUNTIME PASS'}
    if($process.ExitCode -ne 0 -or !(Select-String -LiteralPath $log -Pattern $terminal -Quiet)){throw 'Native sample failed or did not reach its required terminal result'}
    $result.status='PASS'
}catch{$result.status='FAIL';$result.failure=$_.Exception.Message;throw}
finally {
    if($process){$process.Dispose()}
    try {
        # Resolve/verify this exact absolute target again immediately before any
        # recursive deletion. Never enumerate or delete other temporary profiles.
        $resolved=[IO.Path]::GetFullPath($profile)
        if(!$resolved.StartsWith($profileRoot,[StringComparison]::OrdinalIgnoreCase) -or ![IO.Path]::GetFileName($resolved).StartsWith('mfn-webview-',[StringComparison]::Ordinal)){throw 'Profile cleanup target escaped its test-owned root'}
        for($attempt=0;$attempt -lt 20 -and (Test-Path -LiteralPath $resolved);++$attempt){
            try{[IO.Directory]::Delete($resolved,$true)}
            catch [IO.IOException]{Start-Sleep -Milliseconds 100}
            catch [UnauthorizedAccessException]{Start-Sleep -Milliseconds 100}
        }
        if(Test-Path -LiteralPath $resolved){throw 'Owned profile cleanup failed after process-tree exit'}
        $result.profileCleaned=$true
    }catch{$result.status='FAIL';$result.failure=$_.Exception.Message;throw}
    finally{
        $result.endUtc=[DateTime]::UtcNow.ToString('o')
        $result | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $EvidenceDirectory 'windows-process.json')
        if(Test-Path -LiteralPath $log){Get-Content -LiteralPath $log}
        Write-Output "Windows process/profile evidence: $EvidenceDirectory"
    }
}
