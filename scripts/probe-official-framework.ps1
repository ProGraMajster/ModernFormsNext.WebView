param([string]$PackageVersion = '1.11.1')
$ErrorActionPreference = 'Stop'
if ($PackageVersion -notmatch '^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$') {
    throw 'Use an explicit valid framework package version.'
}
# Isolate all generated files from repository source guards/central package versions.
# Never edit the pin, product references, global NuGet config or an existing consumer.
$probeRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../artifacts/official-framework-probes'))
New-Item -ItemType Directory -Path $probeRoot -Force | Out-Null
$probeDirectory = Join-Path $probeRoot ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $probeDirectory | Out-Null
Set-Content (Join-Path $probeDirectory 'Directory.Build.props') '<Project />'
Set-Content (Join-Path $probeDirectory 'Directory.Build.targets') '<Project />'
$packages = @('ModernFormsNext', 'ModernFormsNext.WindowKit', 'ModernFormsNext.WindowKit.Backend.Windows')
$references = ($packages | ForEach-Object { '<PackageReference Include="' + $_ + '" Version="' + $PackageVersion + '" />' }) -join ''
$project = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0-windows10.0.17763.0</TargetFramework><EnableWindowsTargeting>true</EnableWindowsTargeting><ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally></PropertyGroup><ItemGroup>' + $references + '</ItemGroup></Project>'
$sources = @{
    control = 'public static class ControlProbe { public static System.Type Form => typeof(ModernFormsNext.Form); public static System.Type Window => typeof(ModernFormsNext.WindowKit.Platform.IWindowImpl); }'
    hosting = 'using ModernFormsNext; using ModernFormsNext.WindowKit.Platform; using ModernFormsNext.WindowKit.Backend.Windows; public static class HostingProbe { public static System.Type Host => typeof(NativeViewHost); public static System.Type Factory => typeof(INativeViewFactory); public static System.Type Site => typeof(WindowsNativeViewSite); }'
}
$results = [ordered]@{ sdk = (& dotnet --version); packageVersion = $PackageVersion; source = 'https://api.nuget.org/v3/index.json'; builds = @(); packageHashes = @(); latestStable = @{} }
foreach ($packageId in ($packages + 'ModernFormsNext.WindowKit.Backend.Android')) {
    try {
        $index = Invoke-RestMethod ('https://api.nuget.org/v3-flatcontainer/' + $packageId.ToLowerInvariant() + '/index.json')
        $results.latestStable[$packageId] = @($index.versions | Where-Object { $_ -notmatch '-' })[-1]
    }
    catch {
        if ([int]$_.Exception.Response.StatusCode -ne 404) { throw }
        $results.latestStable[$packageId] = 'HTTP404'
    }
}
foreach ($kind in @('control','hosting')) {
    $directory = Join-Path $probeDirectory $kind
    New-Item -ItemType Directory -Path $directory | Out-Null
    $projectPath = Join-Path $directory 'Probe.csproj'
    Set-Content $projectPath $project
    Set-Content (Join-Path $directory 'Probe.cs') $sources[$kind]
    foreach ($configuration in @('Debug','Release')) {
        $log = Join-Path $directory ($configuration.ToLowerInvariant() + '.log')
        # Global property is deliberate: an ancestor Directory.Packages.props must
        # not replace these explicit official package versions during SDK imports.
        & dotnet build $projectPath -c $configuration -m:1 -p:UseSharedCompilation=false -p:ManagePackageVersionsCentrally=false --source 'https://api.nuget.org/v3/index.json' -v:minimal *> $log
        $buildExit = $LASTEXITCODE
        $results.builds += @{ kind = $kind; configuration = $configuration; exitCode = $buildExit; log = $log }
        if ($kind -eq 'control' -and $buildExit -ne 0) {
            Get-Content $log; throw 'Official package control could not compile; hosting compatibility is inconclusive.'
        }
    }
}
$assets = Get-Content (Join-Path $probeDirectory 'control/obj/project.assets.json') -Raw | ConvertFrom-Json
$globalPackages = @($assets.packageFolders.PSObject.Properties.Name)[0]
foreach ($packageId in $packages) {
    $lower = $packageId.ToLowerInvariant()
    $nupkg = Join-Path $globalPackages "$lower/$PackageVersion/$lower.$PackageVersion.nupkg"
    $results.packageHashes += @{ package = $packageId; sha256 = (Get-FileHash -LiteralPath $nupkg -Algorithm SHA256).Hash }
}
$missingHosting = $false
foreach ($build in @($results.builds | Where-Object kind -eq 'hosting')) {
    if ($build.exitCode -eq 0) { continue }
    $errors = @(Get-Content $build.log | Where-Object { $_ -match ': error ' })
    $missing = $errors.Count -gt 0 -and @($errors | Where-Object {
        $_ -notmatch 'error CS0246:' -or $_ -notmatch 'NativeViewHost|INativeViewFactory|WindowsNativeViewSite'
    }).Count -eq 0
    if (-not $missing) { Get-Content $build.log; throw 'Unexpected hosting probe error; do not classify it as missing public API.' }
    $missingHosting = $true
}
$results.decision = if ($missingHosting) { 'NO-GO: official packages lack required hosting API' } else { 'Hosting symbols available; native/package qualification still required' }
$results | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $probeDirectory 'result.json')
Write-Output ("CONTROL COMPILE PASS Debug/Release; " + $results.decision)
Write-Output ("Evidence: " + $probeDirectory)
# A missing required API is a real release-gate failure, never a green qualification.
if ($missingHosting) { exit 2 }
