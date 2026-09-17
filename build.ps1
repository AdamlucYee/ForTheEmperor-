param([switch]$Test)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.build-home'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_NOLOGO = '1'
    $publishDir = Join-Path $PSScriptRoot ('artifacts\publish-' + [Guid]::NewGuid().ToString('N'))
    dotnet publish ForTheEmperor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableSingleFileAnalyzer=false -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=false -p:DebugType=embedded -o $publishDir --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    $publishedExe = Join-Path $publishDir 'ForTheEmperor.exe'
    if ($Test) {
        $run = Start-Process -FilePath $publishedExe -ArgumentList '--self-test', 'artifacts' -Wait -PassThru -WindowStyle Hidden
        Get-Content "$PSScriptRoot\artifacts\test-results.txt"
        if ($run.ExitCode -ne 0) { throw 'Checks failed.' }
    }
    New-Item -ItemType Directory -Path "$PSScriptRoot\dist" -Force | Out-Null
    Copy-Item -LiteralPath $publishedExe -Destination "$PSScriptRoot\dist\ForTheEmperor.exe"
    Copy-Item -LiteralPath $publishedExe -Destination "$PSScriptRoot\ForTheEmperor.exe"
    Copy-Item -LiteralPath "$PSScriptRoot\README.md" -Destination "$PSScriptRoot\dist\README.md"
    $archive = Join-Path $PSScriptRoot 'dist\ForTheEmperor-win-x64-portable.zip'
    Compress-Archive -LiteralPath "$PSScriptRoot\dist\ForTheEmperor.exe", "$PSScriptRoot\dist\README.md" -DestinationPath $archive -Force
    Write-Host "Ready: $PSScriptRoot\dist\ForTheEmperor.exe"
    Write-Host "Portable package: $archive"
} finally { Pop-Location }
