param([string]$ExecutablePath = "$PSScriptRoot/../artifacts/win-x64/FinanceOS.exe")
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$exe = (Resolve-Path $ExecutablePath).Path
function Read-LaunchCount($window) {
    $elements = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($element in $elements) {
        if ($element.Current.Name -match '(?:Local SQLite ready · schema|SQLite local pregătit · schema) 5 · (?:workspace launches|porniri) (\d+)$') { return [long]$Matches[1] }
    }
    return $null
}
function Find-ById($window, $id) {
    $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
    return $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}
function Invoke-ById($window, $id) {
    $button = Find-ById $window $id
    $deadline = (Get-Date).AddSeconds(5)
    while ($null -ne $button -and !$button.Current.IsEnabled -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 100
        $button = Find-ById $window $id
    }
    if ($null -eq $button -or !$button.Current.IsEnabled) { throw "Missing or disabled control: $id" }
    $pattern = $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $pattern.Invoke()
}
function Run-Smoke([bool]$changePreferences, [bool]$forceCrash = $false) {
    $process = Start-Process $exe -PassThru
    try {
        $deadline = (Get-Date).AddSeconds(30)
        $count = $null
        while ((Get-Date) -lt $deadline) {
            if ($process.HasExited) { throw 'Desktop exited during startup.' }
            $process.Refresh()
            if ($process.MainWindowHandle -ne 0) {
                $window = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
                $count = Read-LaunchCount $window
                if ($null -ne $count) { break }
            }
            Start-Sleep -Milliseconds 200
        }
        if ($null -eq $count) { throw 'SQLite ready state was not visible within 30 seconds.' }
        foreach ($id in @('CheckStorageButton', 'TestNoticeButton')) {
            Invoke-ById $window $id
        }
        $deadline = (Get-Date).AddSeconds(5)
        do {
            $notice = Find-ById $window 'NoticeText'
            if ($notice.Current.Name -match 'local notification adapter is working|Notificarea locală funcționează') { break }
            Start-Sleep -Milliseconds 100
        } while ((Get-Date) -lt $deadline)
        if ($notice.Current.Name -notmatch 'local notification adapter is working|Notificarea locală funcționează') { throw 'Notification action failed.' }
        if ($changePreferences) {
            $onboarding = Find-ById $window 'DismissOnboardingButton'
            if ($null -ne $onboarding -and $onboarding.Current.IsEnabled) { Invoke-ById $window 'DismissOnboardingButton' }
            if ((Find-ById $window 'SwitchModeButton').Current.Name -match 'Simple mode|Mod simplu') { Invoke-ById $window 'SwitchModeButton' }
            if ((Find-ById $window 'SwitchThemeButton').Current.Name -match 'Dark theme|Temă închisă') { Invoke-ById $window 'SwitchThemeButton' }
            if ((Find-ById $window 'SwitchLanguageButton').Current.Name -eq 'RO') { Invoke-ById $window 'SwitchLanguageButton' }
        }
        Invoke-ById $window 'SettingsButton'
        $deadline = (Get-Date).AddSeconds(5)
        do {
            $preferences = Find-ById $window 'CurrentPreferencesText'
            if ($preferences.Current.Name -match 'Dark.*ro.*Advanced') { break }
            Start-Sleep -Milliseconds 100
        } while ((Get-Date) -lt $deadline)
        if ($preferences.Current.Name -notmatch 'Dark.*ro.*Advanced') { throw 'Preferences did not persist and render.' }
        if ($changePreferences) {
            Invoke-ById $window 'OverviewButton'
            $quick = Find-ById $window 'QuickTitleBox'
            $value = $quick.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
            $value.SetValue('M2 smoke task')
            Invoke-ById $window 'QuickAddButton'
        }
        Invoke-ById $window 'TasksButton'
        $deadline = (Get-Date).AddSeconds(5)
        do {
            $rows = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
            $found = $false
            foreach ($row in $rows) { if ($row.Current.Name -like '*M2 smoke task*') { $found = $true; break } }
            if ($found) { break }
            Start-Sleep -Milliseconds 100
        } while ((Get-Date) -lt $deadline)
        if (!$found) { throw 'Task was not visible after creation or restart.' }
        if ($changePreferences) {
            $selected = $false
            foreach ($row in $rows) {
                if ($row.Current.Name -like '*M2 smoke task*') {
                    $pattern = $null
                    if ($row.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) {
                        $pattern.Select(); $selected = $true; break
                    }
                }
            }
            if (!$selected) { throw 'Task row could not be selected for reminder.' }
        }
        Invoke-ById $window 'RemindersButton'
        if ($changePreferences) {
            $time = (Find-ById $window 'ReminderTimeBox').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
            $time.SetValue('00:00')
            Invoke-ById $window 'AddReminderButton'
        }
        $deadline = (Get-Date).AddSeconds(10)
        do {
            $grid = Find-ById $window 'ReminderGrid'
            $elements = $grid.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
            $recovered = $false
            foreach ($element in $elements) { if ($element.Current.Name -like '*M2 smoke task*') { $recovered = $true; break } }
            if ($recovered) { break }
            Start-Sleep -Milliseconds 100
        } while ((Get-Date) -lt $deadline)
        if (!$recovered) { throw 'Reminder was not recovered in the inbox.' }
        Invoke-ById $window 'CalendarButton'
        if ($changePreferences) {
            $block = (Find-ById $window 'BlockTitleBox').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
            $block.SetValue('M3 smoke block')
            Invoke-ById $window 'AddBlockButton'
        }
        $deadline = (Get-Date).AddSeconds(5)
        do {
            $grid = Find-ById $window 'CalendarGrid'
            $elements = $grid.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
            $blockFound = $false
            foreach ($element in $elements) { if ($element.Current.Name -like '*M3 smoke block*') { $blockFound = $true; break } }
            if ($blockFound) { break }
            Start-Sleep -Milliseconds 100
        } while ((Get-Date) -lt $deadline)
        if (!$blockFound) { throw 'Calendar block was not visible after creation or restart.' }
        return $count
    } finally {
        if (!$process.HasExited) {
            if ($forceCrash) { $process.Kill(); $process.WaitForExit() }
            else {
                $null = $process.CloseMainWindow()
                if (!$process.WaitForExit(5000)) { $process.Kill(); throw 'App did not close cleanly.' }
            }
        }
        $process.Dispose()
    }
}
$first = Run-Smoke $true $true
$second = Run-Smoke $false
if ($second -ne ($first + 1)) { throw 'Session did not persist across desktop restart.' }
Write-Output 'PASS: Windows launch, storage, notice, navigation, preferences, task capture, reminder recovery, calendar block, forced process termination and restart persistence.'
