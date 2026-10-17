param(
    [Parameter(Mandatory)][string]$AdbPath,
    [Parameter(Mandatory)][string]$Serial,
    [ValidateSet('Debug','Release')][string]$Configuration = 'Debug',
    [string]$EvidenceDirectory = (Join-Path $PSScriptRoot '../../artifacts/issue16'),
    [switch]$Recreate
)
$ErrorActionPreference = 'Stop'
$package = 'com.programajster.modernformsnext.webview.originprobe'
$apk = Join-Path $PSScriptRoot "bin/$Configuration/net10.0-android/$package-Signed.apk"
if (-not (Test-Path -LiteralPath $apk)) { throw 'Build/sign the research APK first.' }
New-Item -ItemType Directory -Path $EvidenceDirectory -Force | Out-Null
$log = Join-Path $EvidenceDirectory "origin-$($Configuration.ToLowerInvariant())-native.log"
Set-Content -LiteralPath $log -Value ''
& $AdbPath -s $Serial install -r $apk | Out-Null
if ($LASTEXITCODE) { throw 'Research APK installation failed.' }
& $AdbPath -s $Serial shell am force-stop $package | Out-Null
$arguments = @('-s',$Serial,'shell','am','start','-n',"$package/.MainActivity")
if ($Recreate) { $arguments += @('--ez','recreate','true') }
& $AdbPath @arguments | Out-Null
if ($LASTEXITCODE) { throw 'Research Activity launch failed.' }
$samplePid = $null
for ($attempt=0; -not $samplePid -and $attempt -lt 20; ++$attempt) {
    Start-Sleep -Milliseconds 100
    $samplePid = & $AdbPath -s $Serial shell pidof -s $package
}
if (-not $samplePid) { throw 'Research process did not start.' }
$required = if ($Recreate) { 2 } else { 1 }
for ($attempt=0; $attempt -lt 90; ++$attempt) {
    & $AdbPath -s $Serial logcat -d --pid=$samplePid -s MFNOriginProbe:I '*:S' | Set-Content -LiteralPath $log
    if (Select-String -LiteralPath $log -Pattern 'ORIGIN PROBE FAIL' -Quiet) { Get-Content -LiteralPath $log; throw 'Native origin experiment failed.' }
    if (-not (& $AdbPath -s $Serial shell pidof -s $package)) { Get-Content -LiteralPath $log; throw 'Research process exited before completing validation.' }
    if (@(Select-String -LiteralPath $log -Pattern 'ORIGIN PROBE EXECUTED').Count -ge $required) {
        if ($Recreate -and -not (Select-String -LiteralPath $log -Pattern 'DESTROY native View/listener/proxies disposed' -Quiet)) { throw 'Activity recreation did not dispose the previous fixture.' }
        Get-Content -LiteralPath $log
        return
    }
    Start-Sleep -Milliseconds 500
}
Get-Content -LiteralPath $log
throw 'Native origin experiment did not finish.'
