# Runs the shipped EXE, creates ONE uniquely named disposable desktop fixture,
# invokes the registered command, and tests watchdog cleanup after a forced exit.
param()
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$exe = Join-Path $taskRoot 'dist\ForTheEmperor.exe'
$menuKey = 'HKCU:\Software\Classes\*\shell\ForTheEmperor.Execute'
$desktopRoot = [Environment]::GetFolderPath('DesktopDirectory')
$fixture = Join-Path $desktopRoot ("ForTheEmperor-integration-test-" + [Guid]::NewGuid().ToString('N') + '.txt')
$results = [Collections.Generic.List[string]]::new()
$petProcess = $null
if (Get-Process ForTheEmperor -ErrorAction SilentlyContinue) { throw 'Close any running pet before this integration check.' }
if (Test-Path -LiteralPath $menuKey) { throw 'Existing menu registration found; integration check will not overwrite it.' }
$settingsPath = Join-Path $env:LOCALAPPDATA 'ForTheEmperor\settings.json'
$originalSettings = if (Test-Path -LiteralPath $settingsPath) { [IO.File]::ReadAllBytes($settingsPath) } else { $null }
function Test-DesktopIcon([string]$state) {
    $probeInfo = [Diagnostics.ProcessStartInfo]::new($exe)
    $probeInfo.UseShellExecute = $false
    $probeInfo.CreateNoWindow = $true
    $probeInfo.ArgumentList.Add('--probe-icon')
    $probeInfo.ArgumentList.Add($fixture)
    $probeInfo.ArgumentList.Add($state)
    $probe = [Diagnostics.Process]::Start($probeInfo)
    try {
        if (!$probe.WaitForExit(8000)) { $probe.Kill(); throw 'Explorer icon probe timed out.' }
        if ($probe.ExitCode -eq 2) { throw 'Explorer desktop icons are unavailable to UI Automation; visible-icon regression cannot be verified.' }
        if ($probe.ExitCode -ne 0) { throw "Explorer icon did not become $state." }
    } finally { $probe.Dispose() }
}
try {
    [IO.File]::WriteAllText($fixture, 'Disposable integration fixture created by ForTheEmperor verification. Safe to remove from Recycle Bin.')
    # Supply an explicit language for unattended verification; restore preferences below.
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($settingsPath)) | Out-Null
    [IO.File]::WriteAllText($settingsPath, '{"Language":"en","MenuEnabled":true}')
    $petProcess = Start-Process -FilePath $exe -WorkingDirectory $taskRoot -WindowStyle Hidden -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds(8)
    while (!(Test-Path -LiteralPath "$menuKey\command") -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 200 }
    $key = Get-Item -LiteralPath "$menuKey\command"
    $expected = '"' + $exe + '" --execute "%1"'
    if ($key.GetValue('') -ne $expected) { throw 'Registered command does not match shipped EXE.' }
    $results.Add('PASS Current-user menu registration points to shipped EXE with quoted file argument')
    Test-DesktopIcon 'present'
    $results.Add('PASS Test fixture is visibly listed in Explorer desktop before execution')
    $psi = [Diagnostics.ProcessStartInfo]::new($exe)
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.ArgumentList.Add('--execute')
    $psi.ArgumentList.Add($fixture)
    $helper = [Diagnostics.Process]::Start($psi)
    if (!$helper.WaitForExit(6000)) { $helper.Kill(); throw 'Context command helper timed out.' }
    if ($helper.ExitCode -ne 0) { throw 'Context command helper rejected the fixture.' }
    $helper.Dispose()
    if (!(Test-Path -LiteralPath $fixture)) { throw 'File disappeared before the attack animation.' }
    $results.Add('PASS IPC accepts desktop fixture and leaves it intact while animation starts')
    $deadline = [DateTime]::UtcNow.AddSeconds(12)
    while ((Test-Path -LiteralPath $fixture) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 250 }
    if (Test-Path -LiteralPath $fixture) { throw 'Desktop fixture was not recycled at impact.' }
    $results.Add('PASS Shipped EXE: context helper -> IPC -> animation -> desktop fixture recycled')
    Test-DesktopIcon 'gone'
    $results.Add('PASS Explorer desktop icon disappears after recycling without F5 or manual refresh')
    # Wait for recovery and return, then intentionally test abnormal-exit cleanup.
    Start-Sleep -Seconds 5
    $petProcess.Kill()
    $petProcess.WaitForExit()
    $deadline = [DateTime]::UtcNow.AddSeconds(8)
    while ((Test-Path -LiteralPath $menuKey) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 200 }
    if (Test-Path -LiteralPath $menuKey) { throw 'Watchdog did not remove the menu registration.' }
    $results.Add('PASS Watchdog removes only its owned menu after the main process is terminated')
} catch {
    $results.Add('FAIL ' + $_.Exception.Message)
    throw
} finally {
    if ($petProcess -and !$petProcess.HasExited) { $petProcess.Kill(); $petProcess.WaitForExit() }
    if ($petProcess) { $petProcess.Dispose() }
    if ($null -ne $originalSettings) { [IO.File]::WriteAllBytes($settingsPath, $originalSettings) }
    elseif (Test-Path -LiteralPath $settingsPath) { Remove-Item -LiteralPath $settingsPath }
    # Delete only the exact fixture created above; never recurse into the desktop.
    if (Test-Path -LiteralPath $fixture) { Remove-Item -LiteralPath $fixture }
    $output = Join-Path $taskRoot 'artifacts'
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    $results | Set-Content -LiteralPath (Join-Path $output 'integration-results.txt')
    $results | Write-Output
}
