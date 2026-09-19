# Packages the already-built executable. Does not build or upload.
param()
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$exe = Join-Path $taskRoot 'dist\ForTheEmperor.exe'
[xml]$project = Get-Content -LiteralPath (Join-Path $taskRoot 'ForTheEmperor.csproj') -Raw
$version = [string]$project.Project.PropertyGroup.Version
$label = $version -replace '\.0$', ''
if ((Get-Item -LiteralPath $exe).VersionInfo.FileVersion -ne "$version.0") { throw 'Build the current project version before packaging.' }
$runtimeConfig = Get-Content -LiteralPath (Join-Path $taskRoot 'bin\Release\net8.0-windows\win-x64\ForTheEmperor.runtimeconfig.json') -Raw | ConvertFrom-Json
if (!$runtimeConfig.runtimeOptions.includedFrameworks) { throw 'A self-contained publish is required.' }
$assets = Get-Content -LiteralPath (Join-Path $taskRoot 'obj\project.assets.json') -Raw | ConvertFrom-Json
$bundleName = "ForTheEmperor-v$label-win-x64"
$staging = Join-Path $taskRoot ('artifacts\package-' + [Guid]::NewGuid().ToString('N'))
$bundle = Join-Path $staging $bundleName
$notices = Join-Path $bundle 'licenses'
New-Item -ItemType Directory -Path $notices -Force | Out-Null
Copy-Item -LiteralPath $exe -Destination (Join-Path $bundle 'ForTheEmperor.exe')
foreach ($name in @('README.zh-CN.txt', 'README.en.txt', 'ART_CREDITS.md')) {
    Copy-Item -LiteralPath (Join-Path $taskRoot "packaging\$name") -Destination $bundle
}
foreach ($framework in $runtimeConfig.runtimeOptions.includedFrameworks) {
    $package = $framework.name.ToLowerInvariant() + '.runtime.win-x64'
    $location = $null
    foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
        $candidate = Join-Path $folder "$package\$($framework.version)"
        if (Test-Path -LiteralPath $candidate) { $location = $candidate; break }
    }
    if (!$location) { throw "Runtime licence source not found: $package" }
    $licence = Get-ChildItem -LiteralPath $location -File | Where-Object { $_.Name -in @('LICENSE', 'LICENSE.TXT') } | Select-Object -First 1
    if (!$licence) { throw "Missing runtime licence: $package" }
    Copy-Item -LiteralPath $licence.FullName -Destination (Join-Path $notices "$($framework.name)-LICENSE.txt")
    $thirdParty = Join-Path $location 'THIRD-PARTY-NOTICES.TXT'
    if (Test-Path -LiteralPath $thirdParty) { Copy-Item -LiteralPath $thirdParty -Destination (Join-Path $notices "$($framework.name)-THIRD-PARTY-NOTICES.txt") }
}
$releaseRoot = Join-Path $taskRoot 'release'
New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
$archive = Join-Path $releaseRoot "$bundleName.zip"
Compress-Archive -LiteralPath $bundle -DestinationPath $archive -Force
Copy-Item -LiteralPath $bundle -Destination $releaseRoot -Recurse -Force
@(
    ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() + "  $bundleName.zip"),
    ((Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant() + "  $bundleName/ForTheEmperor.exe")
) | Set-Content -LiteralPath (Join-Path $releaseRoot 'SHA256SUMS.txt') -Encoding ascii
Write-Output "Portable folder: $(Join-Path $releaseRoot $bundleName)"
Write-Output "Portable ZIP: $archive"
