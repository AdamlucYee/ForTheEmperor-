param([switch]$Test)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.build-home'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_NOLOGO = '1'
    dotnet build ForTheEmperor.csproj -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    if ($Test) {
        $run = Start-Process -FilePath "$PSScriptRoot\bin\Release\net8.0-windows\ForTheEmperor.exe" -ArgumentList '--self-test', 'artifacts' -Wait -PassThru -WindowStyle Hidden
        Get-Content "$PSScriptRoot\artifacts\test-results.txt"
        if ($run.ExitCode -ne 0) { throw 'Checks failed.' }
    }
    dotnet publish ForTheEmperor.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:EnableSingleFileAnalyzer=false -p:IncludeNativeLibrariesForSelfExtract=true -o dist --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Copy-Item -LiteralPath "$PSScriptRoot\README.md" -Destination "$PSScriptRoot\dist\README.md"
    Write-Host "Ready: $PSScriptRoot\dist\ForTheEmperor.exe"
} finally { Pop-Location }
