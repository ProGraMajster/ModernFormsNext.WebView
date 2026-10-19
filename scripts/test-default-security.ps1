param(
    [Parameter(Mandatory)][ValidateSet('Windows','Android')][string]$Platform,
    [Parameter(Mandatory)][string]$FrameworkRoot,
    [ValidateSet('Debug','Release')][string]$Configuration='Debug',
    [string]$AdbPath,
    [string]$Serial,
    [string]$AndroidSdkDirectory,
    [string]$JavaSdkDirectory,
    [switch]$AllowPhysicalDevice,
    [switch]$Recreate,
    [switch]$Keyboard,
    [switch]$Desktop,
    [switch]$Focused
)
$ErrorActionPreference='Stop'
$cleanupFailed=$false
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$snapshot=& (Join-Path $PSScriptRoot 'get-framework-snapshot.ps1')
$frameworkCommit=(git -C $FrameworkRoot rev-parse HEAD | Out-String).Trim()
if($LASTEXITCODE -or $frameworkCommit -ne $snapshot.commit -or (git -C $FrameworkRoot status --porcelain)){throw 'Expected the clean frozen CurrentMaster checkout'}
$output=Join-Path $repo ('artifacts/default-security/'+$Configuration+'-'+$Platform+'-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $output -Force | Out-Null
$fixture=Join-Path $repo "eng/fixtures/LoopbackSecurity/bin/$Configuration/net10.0/LoopbackSecurity.exe"
if(!(Test-Path -LiteralPath $fixture)){throw 'Build the loopback fixture before native testing'}
$server=$null;$reverse=$false;$result=[ordered]@{status='RUNNING';platform=$Platform;configuration=$Configuration;startUtc=[DateTime]::UtcNow.ToString('o');frameworkCommit=$frameworkCommit;webViewCommit=(git -C $repo rev-parse HEAD | Out-String).Trim();workingTreeDirty=[bool](git -C $repo status --porcelain)}
try {
    $start=[Diagnostics.ProcessStartInfo]::new($fixture)
    $start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.WindowStyle='Hidden'
    $start.RedirectStandardInput=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    $server=[Diagnostics.Process]::Start($start)
    $announced=$server.StandardOutput.ReadLineAsync()
    if(!$announced.Wait(5000)){throw 'Owned fixture did not announce within its startup bound'}
    $line=$announced.Result
    $line | Set-Content (Join-Path $output 'fixture.jsonl')
    $info=if($line){$line | ConvertFrom-Json}else{$null}
    if(!$info -or $info.processId -ne $server.Id){throw 'Cannot verify owned loopback process'}
    $result.fixture=$info
    # A successful plain HTTP control proves that the test-owned listener can serve.
    if((Invoke-WebRequest -Uri ($info.httpUri+'/control') -TimeoutSec 5).Content -ne 'owned security fixture'){throw 'Loopback positive control failed'}
    $tlsPort=([uri]$info.tlsUri).Port
    if($Platform -eq 'Android'){
        if(!$AdbPath -or !$Serial){throw 'Android requires an explicit ADB path and serial'}
        & $AdbPath -s $Serial reverse --no-rebind "tcp:$tlsPort" "tcp:$tlsPort"
        if($LASTEXITCODE){throw 'Cannot create a new test-owned TLS reverse mapping; existing mappings are preserved'}
        $reverse=$true
        & (Join-Path $PSScriptRoot 'test-android-runtime.ps1') -AdbPath $AdbPath -Serial $Serial -FrameworkRoot $FrameworkRoot -Configuration $Configuration -AllowPhysicalDevice:$AllowPhysicalDevice -Recreate:$Recreate -AndroidSdkDirectory $AndroidSdkDirectory -JavaSdkDirectory $JavaSdkDirectory -TlsProbeUri ($info.tlsUri+'/certificate') *> (Join-Path $output 'android-driver.log')
        if(!$?){throw 'Selected-device native default security/lifecycle validation failed'}
    }else{
        $sample=Join-Path $repo "samples/ModernFormsNext.WebView.Sample.Windows/bin/$Configuration/net10.0-windows10.0.17763.0/ModernFormsNext.WebView.Sample.Windows.exe"
        $name='download-'+[Guid]::NewGuid().ToString('N')+'.txt'
        # Read the current user's actual Downloads known folder, including redirection.
        if(!('MfnQaKnownFolder' -as [type])){Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class MfnQaKnownFolder {
 [DllImport("shell32.dll")] private static extern int SHGetKnownFolderPath(ref Guid id,uint flags,IntPtr token,out IntPtr path);
 public static string Downloads(){Guid id=new("374DE290-123F-4565-9164-39C4925E467B");IntPtr value;int hr=SHGetKnownFolderPath(ref id,0,IntPtr.Zero,out value);Marshal.ThrowExceptionForHR(hr);try{return Marshal.PtrToStringUni(value)!;}finally{Marshal.FreeCoTaskMem(value);}}
}
'@}
        $downloadPath=Join-Path ([MfnQaKnownFolder]::Downloads()) $name
        if(Test-Path -LiteralPath $downloadPath){throw 'Unique download fixture path already exists'}
        $oldTls=$env:MFN_QA_TLS_URI;$oldDownload=$env:MFN_QA_DOWNLOAD_URI
        try {
            $env:MFN_QA_TLS_URI=$info.tlsUri+'/certificate';$env:MFN_QA_DOWNLOAD_URI=$info.httpUri+'/'+$name
            $arguments=@($(if($Keyboard){'--validate-keyboard'}else{'--validate'}))
            if($Focused){$arguments+='--validate-security'}
            if($Desktop){$arguments+='--validate-desktop'}
            & $sample @arguments *> (Join-Path $output 'windows-native.log')
            if($LASTEXITCODE){throw 'Native Windows validation failed'}
        }finally{$env:MFN_QA_TLS_URI=$oldTls;$env:MFN_QA_DOWNLOAD_URI=$oldDownload}
        if(!(Select-String -LiteralPath (Join-Path $output 'windows-native.log') -Pattern 'RUNTIME PASS' -Quiet)){throw 'Windows terminal result absent'}
        $state=Invoke-RestMethod -Uri ($info.httpUri+'/state') -TimeoutSec 5
        $state | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $output 'server-state.json')
        if($state.requests.('/'+$name) -lt 1){throw 'Actual attachment request did not reach the owned server'}
        Start-Sleep -Seconds 1
        if(Test-Path -LiteralPath $downloadPath){throw 'Default-denied attachment created a file in Downloads'}
        $result.download='PASS actual attachment response requested; no unique file in real Downloads during bounded observation (not a general filesystem proof)'
        $result.sampleSHA256=(Get-FileHash -LiteralPath $sample).Hash
        $result.sampleAssemblySHA256=(Get-FileHash -LiteralPath ([IO.Path]::ChangeExtension($sample,'dll'))).Hash
    }
    $state=Invoke-RestMethod -Uri ($info.httpUri+'/state') -TimeoutSec 5
    $state | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $output 'server-state.json')
    if($state.tlsConnections -lt 1){throw 'Browser never connected to the TLS fixture'}
    if($state.requests.PSObject.Properties['/certificate']){throw 'Browser accepted the untrusted certificate and sent HTTP application data'}
    $result.tls='PASS actual TLS connection, typed native certificate failure, no HTTP request through rejected certificate, fresh document recovery'
    $result.status='PASS'
}catch{$result.status='FAIL';$result.failure=$_.Exception.Message;throw}
finally{
    if($reverse){& $AdbPath -s $Serial reverse --remove "tcp:$tlsPort" *> (Join-Path $output 'reverse-cleanup.log')}
    if($server){
        if(!$server.HasExited){
            $server.StandardInput.WriteLine('stop');$server.StandardInput.Flush()
            if(!$server.WaitForExit(5000)){
                Stop-Process -Id $server.Id
                $cleanupFailed=$true
                $result.status='FAIL';$result.failure='Owned TLS fixture failed graceful private-key/resource cleanup'
            }
        }
        $server.StandardError.ReadToEnd() | Set-Content (Join-Path $output 'fixture-error.log')
        $result.fixtureCleanShutdown=$server.HasExited -and $server.ExitCode -eq 0
        if(!$result.fixtureCleanShutdown){$cleanupFailed=$true;$result.status='FAIL';$result.failure='Owned TLS fixture exit was not clean'}
        $server.Dispose()
    }
    $result.endUtc=[DateTime]::UtcNow.ToString('o');$result | ConvertTo-Json -Depth 7 | Set-Content (Join-Path $output 'result.json')
    Write-Output "Evidence: $output"
    if($cleanupFailed){throw $result.failure}
}
