# Exercises the real first-run window, persisted language, live bindings and Shell label.
# Restores the user's exact settings after testing. Never operates on desktop files.
param()
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$exe = Join-Path $PSScriptRoot 'dist\ForTheEmperor.exe'
$settings = Join-Path $env:LOCALAPPDATA 'ForTheEmperor\settings.json'
$menuKey = 'HKCU:\Software\Classes\*\shell\ForTheEmperor.Execute'
if (Get-Process ForTheEmperor -ErrorAction SilentlyContinue) { throw 'Close the desktop companion before running this check.' }
if (Test-Path -LiteralPath $menuKey) { throw 'Existing Shell registration found; leaving it untouched.' }
$original = if (Test-Path -LiteralPath $settings) { [IO.File]::ReadAllBytes($settings) } else { $null }
$results = [Collections.Generic.List[string]]::new()
$testProcess = $null
function Wait-Window([int]$processId, [string]$namePart) {
    $limit = [DateTime]::UtcNow.AddSeconds(15)
    while ([DateTime]::UtcNow -lt $limit) {
        $condition = [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty, $processId)
        $windows = [Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children, $condition)
        foreach ($window in $windows) {
            if ($window.Current.Name -notlike "*$namePart*") { continue }
            if ($namePart -eq 'Desktop Companion') {
                $selector = $window.FindFirst([Windows.Automation.TreeScope]::Descendants, [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty, 'LanguageCombo'))
                if ($null -eq $selector) { continue }
            }
            return $window
        }
        Start-Sleep -Milliseconds 150
    }
    throw "Window not found: $namePart"
}
function Find-Control($window, [string]$property, [string]$value) {
    $prop = if ($property -eq 'Id') { [Windows.Automation.AutomationElement]::AutomationIdProperty } else { [Windows.Automation.AutomationElement]::NameProperty }
    $control = $window.FindFirst([Windows.Automation.TreeScope]::Descendants, [Windows.Automation.PropertyCondition]::new($prop, $value))
    if ($null -eq $control) { throw "Control not found: $value" }
    return $control
}
function Set-Language($window, [string]$name) {
    $combo = Find-Control $window 'Id' 'LanguageCombo'
    $combo.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    Start-Sleep -Milliseconds 150
    $item = Find-Control $combo 'Name' $name
    $item.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $combo.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()
    Start-Sleep -Milliseconds 250
}
function Stop-TestProcess {
    if ($testProcess -and !$testProcess.HasExited) { $testProcess.Kill(); $testProcess.WaitForExit() }
    $limit = [DateTime]::UtcNow.AddSeconds(8)
    while ((Test-Path -LiteralPath $menuKey) -and [DateTime]::UtcNow -lt $limit) { Start-Sleep -Milliseconds 100 }
    if (Test-Path -LiteralPath $menuKey) { throw 'Watchdog cleanup failed.' }
}
try {
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($settings)) | Out-Null
    [IO.File]::WriteAllText($settings, '{"Chapter":3,"Scale":1.1,"Sound":false,"MenuEnabled":false}')
    $testProcess = Start-Process -FilePath $exe -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -PassThru
    $picker = Wait-Window $testProcess.Id 'Language / 语言'
    if (Test-Path -LiteralPath $menuKey) { throw 'Shell menu registered before language confirmation.' }
    (Find-Control $picker 'Name' 'English').GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()
    (Find-Control $picker 'Name' 'Continue / 进入桌面').GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
    $panel = Wait-Window $testProcess.Id 'Desktop Companion'
    Find-Control $panel 'Name' 'Mission control' | Out-Null
    $prefs = Get-Content -LiteralPath $settings -Raw | ConvertFrom-Json
    if ($prefs.Language -ne 'en' -or $prefs.Chapter -ne 3 -or $prefs.Scale -ne 1.1) { throw 'Language migration lost existing preferences.' }
    $results.Add('PASS Old settings prompt for language; English choice persists without losing chapter or scale')
    (Find-Control $panel 'Id' 'MenuCheck').GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle()
    Start-Sleep -Milliseconds 400
    if ((Get-Item -LiteralPath $menuKey).GetValue('') -ne "Execute in the Emperor's name") { throw 'English Shell command missing.' }
    Set-Language $panel '简体中文'
    Find-Control $panel 'Name' '行动控制' | Out-Null
    if ((Get-Item -LiteralPath $menuKey).GetValue('') -ne '以帝皇之名处决') { throw 'Chinese Shell command did not refresh.' }
    Set-Language $panel 'English'
    Find-Control $panel 'Name' 'Mission control' | Out-Null
    if ((Get-Item -LiteralPath $menuKey).GetValue('') -ne "Execute in the Emperor's name") { throw 'English Shell command did not refresh.' }
    $results.Add('PASS Live Chinese/English switching updates panel and actual Explorer command')
    Stop-TestProcess
    $testProcess.Dispose()
    $testProcess = Start-Process -FilePath $exe -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -PassThru
    $panel = Wait-Window $testProcess.Id 'Desktop Companion'
    Find-Control $panel 'Name' 'Mission control' | Out-Null
    $results.Add('PASS Restart opens the saved English interface without another language prompt')
    Stop-TestProcess
    $results.Add('PASS Watchdog removes localized Explorer command after process exit')
} catch {
    $results.Add('FAIL ' + $_.Exception.Message)
    throw
} finally {
    try { Stop-TestProcess } finally {
        if ($testProcess) { $testProcess.Dispose() }
        if ($null -ne $original) { [IO.File]::WriteAllBytes($settings, $original) }
        elseif (Test-Path -LiteralPath $settings) { Remove-Item -LiteralPath $settings }
        $output = Join-Path $PSScriptRoot 'artifacts\language-integration-results.txt'
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($output)) | Out-Null
        $results | Set-Content -LiteralPath $output
        $results | Write-Output
    }
}
