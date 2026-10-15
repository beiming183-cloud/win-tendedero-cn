param(
    [string]$DotnetPath = '',
    [string]$PackageSource = ''
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if (-not $DotnetPath) {
    $installed = Get-Command dotnet -ErrorAction SilentlyContinue
    $candidates = @()
    if ($installed) { $candidates += $installed.Source }
    $candidates += 'D:\Codex\Tools\dotnet-sdk\dotnet.exe'
    foreach ($candidate in $candidates) {
        if (-not (Test-Path -LiteralPath $candidate)) { continue }
        if ((& $candidate --list-sdks) -match '^8\.') { $DotnetPath = $candidate; break }
    }
}
if (-not $DotnetPath -or -not (Test-Path -LiteralPath $DotnetPath)) { throw 'Install .NET 8 SDK or pass -DotnetPath.' }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$project = Join-Path $root 'src\WinTendedero\WinTendedero.csproj'
$out = Join-Path $root 'artifacts'
$publish = Join-Path $out 'publish'
$package = Join-Path $out 'win-tendedero'
[xml]$projectXml = Get-Content -LiteralPath $project -Raw -Encoding utf8
$version = $projectXml.Project.PropertyGroup.InformationalVersion | Where-Object { $_ } | Select-Object -First 1
$arguments = @('publish', $project, '-c', 'Release', '-r', 'win-x64', '-o', $publish,
    '--self-contained', 'true', '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:EnableCompressionInSingleFile=true', '-p:DebugType=none')
if ($PackageSource) { $arguments += @('--source', $PackageSource) }
& $DotnetPath @arguments
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }
New-Item -ItemType Directory -Path $package -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $publish 'win-tendedero.exe') -Destination $package -Force
Copy-Item -LiteralPath (Join-Path $root 'LICENSE'), (Join-Path $root 'THIRD-PARTY-NOTICES.md'), (Join-Path $root '使用说明.txt') -Destination $package -Force
Copy-Item -LiteralPath (Join-Path $root 'docs\LICENSE-zh-patch.txt') -Destination $package -Force
Copy-Item -LiteralPath (Join-Path $root 'src\WinTendedero\Assets\Fonts\OFL.txt') -Destination (Join-Path $package 'OFL-Manrope.txt') -Force
$cache = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }
$assets = Get-Content -LiteralPath (Join-Path $root 'src\WinTendedero\obj\project.assets.json') -Raw | ConvertFrom-Json
$runtimeDependency = $assets.project.frameworks.PSObject.Properties.Value.downloadDependencies | Where-Object { $_.name -eq 'Microsoft.NETCore.App.Runtime.win-x64' } | Select-Object -First 1
if (-not $runtimeDependency) { throw 'Runtime license source was not resolved.' }
$runtimeVersion = ($runtimeDependency.version -replace '^\[([^,]+),.*$', '$1').Trim()
$runtimePackage = Join-Path $cache "microsoft.netcore.app.runtime.win-x64\$runtimeVersion"
Copy-Item -LiteralPath (Join-Path $runtimePackage 'LICENSE.TXT') -Destination (Join-Path $package 'LICENSE-dotnet.txt') -Force
Copy-Item -LiteralPath (Join-Path $runtimePackage 'THIRD-PARTY-NOTICES.TXT') -Destination (Join-Path $package 'THIRD-PARTY-NOTICES-dotnet.txt') -Force
$zip = Join-Path $out "win-tendedero-v$version-windows-x64.zip"
Compress-Archive -LiteralPath $package -DestinationPath $zip -CompressionLevel Optimal -Force
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $out 'SHA256SUMS.txt'), "$hash  $([IO.Path]::GetFileName($zip))`n", [Text.UTF8Encoding]::new($false))
Write-Output "Portable package: $zip"
