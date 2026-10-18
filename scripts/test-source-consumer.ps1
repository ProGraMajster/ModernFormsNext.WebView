param(
    [Parameter(Mandatory)][string]$FrameworkRoot,
    [ValidateSet('CurrentMaster','HistoricalBaseline')][string]$Snapshot = 'CurrentMaster',
    [ValidateSet('Windows','Android')][string]$Platform = 'Windows',
    [ValidateSet('Debug','Release')][string[]]$Configurations = @('Debug','Release'),
    [string]$JavaSdkDirectory
)
$ErrorActionPreference = 'Stop'
$entry = & (Join-Path $PSScriptRoot 'get-framework-snapshot.ps1') -Snapshot $Snapshot
$FrameworkRoot = (Resolve-Path -LiteralPath $FrameworkRoot).Path
$actualCommit = git -C $FrameworkRoot rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or $actualCommit -ne $entry.commit) { throw 'Consumer source root does not match the selected frozen snapshot.' }
$changes = git -C $FrameworkRoot status --porcelain
if ($LASTEXITCODE -ne 0 -or $changes) { throw 'Framework source checkout must be clean.' }
$webViewRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$outputRoot = Join-Path $webViewRoot 'artifacts/source-consumers'
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$directory = Join-Path $outputRoot ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $directory | Out-Null
# These files isolate the external consumer from WebView's source guard and central
# versions. The local property must NOT propagate globally to product references.
Set-Content -LiteralPath (Join-Path $directory 'Directory.Build.props') '<Project><PropertyGroup><ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally><ShouldUnsetParentConfigurationAndPlatform>false</ShouldUnsetParentConfigurationAndPlatform></PropertyGroup></Project>'
Set-Content -LiteralPath (Join-Path $directory 'Directory.Packages.props') '<Project />'
Set-Content -LiteralPath (Join-Path $directory 'Directory.Build.targets') '<Project />'
$tfm = if ($Platform -eq 'Windows') { 'net10.0-windows10.0.17763.0' } else { 'net10.0-android' }
$outputType = if ($Platform -eq 'Windows') { 'Exe' } else { 'Library' }
$platformVersion = if ($Platform -eq 'Android') { '<SupportedOSPlatformVersion>23.0</SupportedOSPlatformVersion>' } else { '' }
$project = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>__TFM__</TargetFramework>
    <OutputType>__OUTPUT__</OutputType>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    __PLATFORM_VERSION__
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$(WebViewSourceRoot)/src/ModernFormsNext.WebView.__PLATFORM__/ModernFormsNext.WebView.__PLATFORM__.csproj" />
  </ItemGroup>
</Project>
'@
$project = $project.Replace('__TFM__',$tfm).Replace('__OUTPUT__',$outputType).Replace('__PLATFORM_VERSION__',$platformVersion).Replace('__PLATFORM__',$Platform)
$projectPath = Join-Path $directory 'Consumer.csproj'
Set-Content -LiteralPath $projectPath -Value $project
# Compile real public types, including native sites, without a friend assembly or
# repository props/targets. Windows execution is deliberately headless, not native QA.
$source = @'
using ModernFormsNext;
using ModernFormsNext.WebView;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Backend.__PLATFORM__;
using ModernFormsNext.WebView.__PLATFORM__;

internal static class SourceContractProbe
{
    private static readonly Type[] Contracts =
    [
        typeof(Form), typeof(NativeViewHost), typeof(INativeViewFactory),
        typeof(__PLATFORM__NativeViewSite), typeof(__PLATFORM__WebViewProvider),
        typeof(WebView), typeof(WebViewTransport), typeof(WebViewRpcClient),
        typeof(WebViewLocalContent), typeof(WebViewLocalContentPolicy)
    ];
    private static void Main()
    {
        IWebViewPeerFactoryProvider provider = new __PLATFORM__WebViewProvider();
        if (Contracts.Length != 10 || WebViewLocalContentPolicy.Default.Scripts != WebViewLocalContentSource.Self ||
            provider is null)
            throw new InvalidOperationException("Public contract smoke failed.");
        Console.WriteLine("SOURCE CONSUMER CONTRACT PASS (headless; no native engine)");
        __WINDOWS_ASSET_ASSERTIONS__
    }
}
'@
$assetAssertions = if ($Platform -eq 'Windows') {
@'
        foreach (string asset in new[] { "Microsoft.Web.WebView2.Core.dll",
            "runtimes/win-x86/native/WebView2Loader.dll", "runtimes/win-x64/native/WebView2Loader.dll",
            "runtimes/win-arm64/native/WebView2Loader.dll" })
            if (!File.Exists(System.IO.Path.Combine(AppContext.BaseDirectory, asset)))
                throw new InvalidOperationException("Missing external consumer asset: " + asset);
        foreach (string wrapper in new[] { "Microsoft.Web.WebView2.Wpf.dll", "Microsoft.Web.WebView2.WinForms.dll" })
            if (File.Exists(System.IO.Path.Combine(AppContext.BaseDirectory, wrapper)))
                throw new InvalidOperationException("Unused UI wrapper leaked to external consumer: " + wrapper);
        Console.WriteLine("SOURCE CONSUMER ASSETS PASS (Core + all native loaders; no UI wrappers)");
'@
} else { '' }
Set-Content -LiteralPath (Join-Path $directory 'Consumer.cs') -Value $source.Replace('__PLATFORM__',$Platform).Replace('__WINDOWS_ASSET_ASSERTIONS__',$assetAssertions)
$properties = @("-p:ModernFormsNextSourceRoot=$FrameworkRoot","-p:WebViewSourceRoot=$webViewRoot",'-p:EnableWindowsTargeting=true','-p:GeneratePackageOnBuild=false')
if ($JavaSdkDirectory) { $properties += "-p:JavaSdkDirectory=$JavaSdkDirectory" }
$results = [ordered]@{
    snapshot = $Snapshot; frameworkCommit = $actualCommit; platform = $Platform
    webViewCommit = (git -C $webViewRoot rev-parse HEAD); sdk = (& dotnet --version)
    mode = 'isolated source-reference compile; Windows headless execution; not official packages or native qualification'
    builds = @()
}
& dotnet restore $projectPath @properties -v:minimal *> (Join-Path $directory 'restore.log')
if ($LASTEXITCODE -ne 0) { Get-Content (Join-Path $directory 'restore.log'); throw 'Source consumer restore failed.' }
foreach ($configuration in $Configurations) {
    $log = Join-Path $directory "$configuration-build.log"
    & dotnet build $projectPath -c $configuration --no-restore -m:1 -warnaserror -p:UseSharedCompilation=false @properties -v:minimal *> $log
    if ($LASTEXITCODE -ne 0) { Get-Content $log; throw "Source consumer $configuration build failed." }
    $execution = 'NOT RUN (Android compile only)'
    if ($Platform -eq 'Windows') {
        & dotnet (Join-Path $directory "bin/$configuration/$tfm/Consumer.dll") *> (Join-Path $directory "$configuration-headless.log")
        if ($LASTEXITCODE -ne 0) { Get-Content (Join-Path $directory "$configuration-headless.log"); throw 'Headless consumer execution failed.' }
        $execution = 'headless contract execution PASS'
    }
    $results.builds += @{ configuration = $configuration; compile = 'PASS'; execution = $execution; log = $log }
}
$results | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $directory 'result.json')
Write-Output "SOURCE CONSUMER PASS $Snapshot $Platform $actualCommit"
Write-Output "Evidence: $directory"
