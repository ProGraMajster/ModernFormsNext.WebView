param(
    [Parameter(Mandatory)][string]$AdbPath,
    [Parameter(Mandatory)][string]$Serial,
    [Parameter(Mandatory)][string]$FrameworkRoot,
    [ValidateSet('Debug','Release')][string]$Configuration='Debug',
    [ValidateSet('Full','Floor')][string]$Mode='Full',
    [switch]$Recreate,
    [switch]$NativeBack,
    # The human must explicitly designate the selected physical device for this test.
    [switch]$AllowPhysicalDevice,
    [string]$AndroidSdkDirectory,
    [string]$JavaSdkDirectory,
    [string]$TlsProbeUri,
    [string]$EvidenceDirectory=(Join-Path $PSScriptRoot '../artifacts/native-android'),
    [int]$TimeoutSeconds=180
)
$ErrorActionPreference='Stop'
$AdbPath=(Resolve-Path -LiteralPath $AdbPath).Path
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runId=[Guid]::NewGuid().ToString('N')
$output=Join-Path $EvidenceDirectory "$Configuration-$Mode-$runId"
New-Item -ItemType Directory -Path $output -Force | Out-Null
function DeviceText([string[]]$Arguments) {
    $value=(& $AdbPath -s $Serial @Arguments | Out-String).Trim()
    if($LASTEXITCODE){throw "Selected-device command failed: $($Arguments[0])"}
    return $value
}
if((DeviceText @('get-state')) -ne 'device'){throw 'Selected device is not ready'}
$isEmulator=(DeviceText @('shell','getprop','ro.kernel.qemu')) -eq '1'
if(!$isEmulator -and !$AllowPhysicalDevice){throw 'Physical-device installation requires an explicit human test-device approval and -AllowPhysicalDevice'}
$api=[int](DeviceText @('shell','getprop','ro.build.version.sdk'))
if($api -lt 23){throw 'Runtime is below the declared API23 baseline'}
if($NativeBack -and $Mode -ne 'Floor'){throw 'The explicit native Back fixture belongs to the Floor mode'}
$entry=& (Join-Path $PSScriptRoot 'get-framework-snapshot.ps1')
$actualFramework=(git -C $FrameworkRoot rev-parse HEAD | Out-String).Trim()
if($LASTEXITCODE -or $actualFramework -ne $entry.commit -or (git -C $FrameworkRoot status --porcelain)){throw 'Framework root must be the clean frozen CurrentMaster snapshot'}
$apk=Join-Path $repo "samples/ModernFormsNext.WebView.Sample.Android/bin/$Configuration/net10.0-android/com.programajster.modernformsnext.webview.sample-Signed.apk"
if(!(Test-Path -LiteralPath $apk)){throw 'Build and sign the requested sample first'}
# Inspect the actual APK rather than just the source manifest. Older AAR metadata can
# implicitly inject legacy storage/phone permissions during the final manifest merge.
$sdkRoot=if($AndroidSdkDirectory){(Resolve-Path -LiteralPath $AndroidSdkDirectory).Path}else{Split-Path (Split-Path $AdbPath -Parent) -Parent}
$aapt=Join-Path $sdkRoot 'build-tools/36.0.0/aapt2.exe'
if(!(Test-Path -LiteralPath $aapt)){$aapt=Join-Path $sdkRoot 'build-tools/36.0.0/aapt2'}
$permissions=& $aapt dump permissions $apk
if($LASTEXITCODE){throw 'Cannot inspect final APK permissions'}
$permissions | Set-Content (Join-Path $output 'apk-permissions.log')
$names=@($permissions | ForEach-Object {if($_ -match "uses-permission: name='([^']+)'"){$Matches[1]}})
if($names.Count -ne 1 -or $names[0] -ne 'android.permission.INTERNET'){throw 'The native QA sample must request only INTERNET; refusing installation'}
$identity=[ordered]@{
    runId=$runId;startUtc=[DateTime]::UtcNow.ToString('o');serial=$Serial;emulator=$isEmulator
    api=$api;abi=(DeviceText @('shell','getprop','ro.product.cpu.abi'));configuration=$Configuration;mode=$Mode
    webViewCommit=(git -C $repo rev-parse HEAD | Out-String).Trim();webViewWorkingTreeDirty=[bool](git -C $repo status --porcelain)
    frameworkCommit=$actualFramework;androidSdkRoot=$sdkRoot;dotnetSdk=(& dotnet --version | Out-String).Trim();apkSHA256=(Get-FileHash -LiteralPath $apk).Hash
    recreation=[bool]$Recreate;nativeBack=[bool]$NativeBack;status='RUNNING'
}
$identity | ConvertTo-Json | Set-Content (Join-Path $output 'result.json')
$providerPackages=DeviceText @('shell','pm','list','packages','webview')
$providerPackages | Set-Content (Join-Path $output 'provider-packages.log')
if($api -ge 24){DeviceText @('shell','dumpsys','webviewupdate') | Set-Content (Join-Path $output 'selected-provider.log')}
& $AdbPath version | Set-Content (Join-Path $output 'adb-version.log')
if($JavaSdkDirectory){
    $java=Join-Path (Resolve-Path -LiteralPath $JavaSdkDirectory).Path 'bin/java.exe'
    if(!(Test-Path -LiteralPath $java)){$java=Join-Path $JavaSdkDirectory 'bin/java'}
    & $java -version 2>&1 | Set-Content (Join-Path $output 'jdk-version.log')
    if($LASTEXITCODE){throw 'Cannot record the explicitly supplied build JDK'}
}else{'NOT SUPPLIED: this driver does not build the APK; record its actual build JDK separately' | Set-Content (Join-Path $output 'jdk-version.log')}
foreach($package in ($providerPackages -split '[\r\n]+' | Where-Object {$_ -match '^package:'})){
    $provider=$package.Substring(8).Trim()
    ((DeviceText @('shell','dumpsys','package',$provider)) -split '[\r\n]+') | Where-Object {$_ -match 'versionCode=|versionName='} | Set-Content (Join-Path $output ($provider+'-version.log'))
}
try {
    # Avoid an incremental helper retaining the installation indefinitely when
    # multiple SDK client versions share the machine's existing ADB server.
    & $AdbPath -s $Serial install --no-incremental --no-streaming -r $apk *> (Join-Path $output 'install.log')
    if($LASTEXITCODE){throw 'Sample installation failed; no uninstall/downgrade fallback is attempted'}
    [void](DeviceText @('shell','am','force-stop','com.programajster.modernformsnext.webview.sample'))
    $arguments=@('shell','am','start','-n','com.programajster.modernformsnext.webview.sample/.MainActivity','--es','validation-run-id',$runId,'--ez',$(if($Mode -eq 'Floor'){'validate-floor'}else{'validate'}),'true')
    if($Recreate){$arguments+=@('--ez','validate-lifecycle','true')}
    if($NativeBack){$arguments+=@('--ez','validate-back','true')}
    if($TlsProbeUri){
        $probe=[uri]$TlsProbeUri
        if($probe.Scheme -ne 'https' -or $probe.Host -ne '127.0.0.1'){throw 'Only the owned loopback TLS fixture is accepted'}
        $arguments+=@('--es','validation-tls-uri',$TlsProbeUri)
    }
    DeviceText $arguments | Set-Content (Join-Path $output 'start.log')
    $samplePid=''
    for($attempt=0;$attempt -lt 40 -and !$samplePid;++$attempt){
        if($api -lt 24){
            $line=((DeviceText @('shell','ps')) -split '[\r\n]+') | Where-Object {$_ -match '\scom\.programajster\.modernformsnext\.webview\.sample\s*$'} | Select-Object -First 1
            $candidate=if($line){($line.Trim() -split '\s+')[1]}else{''}
        }else{
            $candidate=(& $AdbPath -s $Serial shell pidof -s com.programajster.modernformsnext.webview.sample | Out-String).Trim()
        }
        if($candidate -match '^\d+$'){$samplePid=$candidate}else{Start-Sleep -Milliseconds 100}
    }
    if(!$samplePid){throw 'Sample process did not start'}
    $identity.processId=$samplePid
    $pattern='^\d\d-\d\d\s+\d\d:\d\d:\d\d\.\d+\s+'+[regex]::Escape($samplePid)+'\s+'
    $watch=[Diagnostics.Stopwatch]::StartNew();$backSent=$false;$passed=$false
    while($watch.Elapsed.TotalSeconds -lt $TimeoutSeconds){
        # Host filtering works on API23, whose logcat does not accept --pid. A fresh
        # run marker excludes old PID-reuse records without clearing anyone's logcat.
        $lines=@(& $AdbPath -s $Serial logcat -d -v threadtime -s MFNWebView:I AndroidRuntime:E '*:S' | Where-Object {$_ -match $pattern})
        $marker=-1
        for($i=0;$i -lt $lines.Count;++$i){if($lines[$i].Contains('RUN START '+$runId)){$marker=$i;break}}
        if($marker -ge 0){
            $current=@($lines | Select-Object -Skip $marker -Last 2000)
            $current | Set-Content (Join-Path $output 'native.log')
            if($current -match 'RUNTIME FAIL|FATAL EXCEPTION'){throw 'Native sample reported failure'}
            if($NativeBack -and !$backSent -and $current -match 'FLOOR WAIT NATIVE BACK'){
                $window=DeviceText @('shell','dumpsys','window','windows')
                $focused=@($window -split '[\r\n]+' | Where-Object {$_ -match 'mCurrentFocus='})
                if(!($focused -match 'com\.programajster\.modernformsnext\.webview\.sample/')){throw 'Refusing Back: foreground window is not the selected sample'}
                [void](DeviceText @('shell','input','keyevent','4'));$backSent=$true
            }
            $terminal=if($Mode -eq 'Floor'){'FLOOR PASS'}else{'RUNTIME PASS'}
            if($current -match $terminal){$passed=$true;break}
        }
        Start-Sleep -Milliseconds 500
    }
    if(!$passed){throw 'Native sample did not reach its terminal result before the bound'}
    if($NativeBack -and !$backSent){throw 'Native Back actor did not execute'}
    $identity.status='PASS';$identity.nativeBackSent=$backSent
    Get-Content (Join-Path $output 'native.log') -Tail 16
}
catch {$identity.status='FAIL';$identity.failure=$_.Exception.Message;throw}
finally {$identity.endUtc=[DateTime]::UtcNow.ToString('o');$identity | ConvertTo-Json | Set-Content (Join-Path $output 'result.json');Write-Output "Evidence: $output"}
