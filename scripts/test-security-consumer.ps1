param(
    [Parameter(Mandatory)][string]$FrameworkRoot,
    [ValidateSet('CurrentMaster','HistoricalBaseline')][string]$Snapshot='CurrentMaster',
    [ValidateSet('Debug','Release')][string[]]$Configurations=@('Debug','Release')
)
$ErrorActionPreference='Stop'
$entry=& (Join-Path $PSScriptRoot 'get-framework-snapshot.ps1') -Snapshot $Snapshot
$FrameworkRoot=(Resolve-Path -LiteralPath $FrameworkRoot).Path
$frameworkCommit=(git -C $FrameworkRoot rev-parse HEAD | Out-String).Trim()
if($LASTEXITCODE -or $frameworkCommit -ne $entry.commit -or (git -C $FrameworkRoot status --porcelain)){throw 'Expected the clean frozen framework source checkout'}
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output=Join-Path $repo ('artifacts/security-consumers/'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $output -Force | Out-Null
# Real external compilation: do not inherit repository central versions, generated
# source rules or warning suppression. Product references keep their own normal props.
Set-Content (Join-Path $output 'Directory.Build.props') '<Project><PropertyGroup><ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally><ShouldUnsetParentConfigurationAndPlatform>false</ShouldUnsetParentConfigurationAndPlatform></PropertyGroup></Project>'
Set-Content (Join-Path $output 'Directory.Packages.props') '<Project />'
Set-Content (Join-Path $output 'Directory.Build.targets') '<Project />'
$project=@'
<Project Sdk="Microsoft.NET.Sdk">
 <PropertyGroup><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
 <ItemGroup><ProjectReference Include="$(WebViewSourceRoot)/src/ModernFormsNext.WebView/ModernFormsNext.WebView.csproj" /></ItemGroup>
</Project>
'@
$cases=@{
 Safe=@'
using ModernFormsNext.WebView;
internal static class Consumer {
 internal static void Configure(WebView browser) { browser.NewWindowPolicy=WebViewNewWindowPolicy.Deny; browser.NewWindowPolicy=WebViewNewWindowPolicy.UserInitiated; }
}
'@
 Unsafe=@'
using ModernFormsNext.WebView;
internal static class Consumer {
 internal static void Configure(WebView browser) { browser.NewWindowPolicy=WebViewNewWindowPolicy.AllowScriptInitiated; }
}
'@
 Suppressed=@'
using ModernFormsNext.WebView;
internal static class Consumer {
 internal static void Configure(WebView browser) {
#pragma warning disable MFNWVSEC001 // Reviewed: this application explicitly accepts non-gesture popup intents.
  browser.NewWindowPolicy=WebViewNewWindowPolicy.AllowScriptInitiated;
#pragma warning restore MFNWVSEC001
 }
}
'@
}
$cases.UnsafeStrict=$cases.Unsafe
$result=[ordered]@{snapshot=$Snapshot;frameworkCommit=$frameworkCommit;webViewCommit=(git -C $repo rev-parse HEAD | Out-String).Trim();workingTreeDirty=[bool](git -C $repo status --porcelain);sdk=(& dotnet --version | Out-String).Trim();mode='external compiler diagnostics; runtime dynamic warning has separate deterministic/native tests';builds=@()}
$properties=@("-p:ModernFormsNextSourceRoot=$FrameworkRoot","-p:WebViewSourceRoot=$repo",'-p:EnableWindowsTargeting=true','-p:UseSharedCompilation=false','-p:GeneratePackageOnBuild=false')
try {
 foreach($name in @('Safe','Unsafe','Suppressed','UnsafeStrict')){
  $directory=Join-Path $output $name; New-Item -ItemType Directory -Path $directory | Out-Null
  $projectPath=Join-Path $directory 'Consumer.csproj'
  Set-Content -LiteralPath $projectPath $project
  Set-Content -LiteralPath (Join-Path $directory 'Consumer.cs') $cases[$name]
  & dotnet restore $projectPath @properties -v:quiet *> (Join-Path $directory 'restore.log')
  if($LASTEXITCODE){throw "$name consumer restore failed"}
  foreach($configuration in $Configurations){
   $log=Join-Path $directory ($configuration+'.log')
   $arguments=@('build',$projectPath,'-c',$configuration,'--no-restore','-m:1','-v:quiet')+$properties
   if($name -ne 'Unsafe'){$arguments+='-warnaserror'}
   & dotnet @arguments *> $log
   $exitCode=$LASTEXITCODE;$text=Get-Content -LiteralPath $log -Raw
   if($name -eq 'UnsafeStrict'){
    if($exitCode -eq 0 -or $text -notmatch 'Consumer\.cs\(\d+,\d+\).*error MFNWVSEC001'){throw 'Unsafe consumer did not fail specifically on its security warning under warnings-as-errors'}
   }elseif($exitCode){throw "$name $configuration consumer build failed"}
   if($name -in @('Unsafe','UnsafeStrict')){
    if($text -notmatch 'Consumer\.cs\(\d+,\d+\).*MFNWVSEC001' -or $text -notmatch 'Security warning:' -or $text -notmatch 'phishing' -or $text -notmatch 'UserInitiated'){
     throw 'The external consumer did not receive the precise diagnostic ID, risk and safer alternative'
    }
   }elseif($text -match 'MFNWVSEC001'){throw 'Safe or locally suppressed consumer unexpectedly received the security diagnostic'}
   if($text -match '(?i)warning\s+(?!MFNWVSEC001\b)[A-Z]+\d+'){throw 'Unexpected consumer warning; no global NoWarn is applied'}
   $result.builds+=@{case=$name;configuration=$configuration;status='PASS';exitCode=$exitCode;expectedSecurityDiagnostic=($name -in @('Unsafe','UnsafeStrict'))}
   Write-Output "$name $configuration consumer diagnostic PASS"
  }
 }
 $result.status='PASS'
}catch{$result.status='FAIL';$result.failure=$_.Exception.Message;throw}
finally{$result | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $output 'result.json');Write-Output "Evidence: $output"}
# The last native compilation deliberately failed. All expectations above passed;
# return success explicitly so CI does not inherit that expected compiler exit code.
exit 0
