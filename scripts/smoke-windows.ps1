$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$exe = Join-Path $PSScriptRoot '../artifacts/win-x64/FinanceOS.exe'
function Read-LaunchCount($window) {
    $elements = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($element in $elements) {
        if ($element.Current.Name -match '^Local SQLite ready · schema 1 · workspace launches (\d+)$') { return [long]$Matches[1] }
    }
    return $null
}
function Run-Smoke {
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
            $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
            $button = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
            if ($null -eq $button -or !$button.Current.IsEnabled) { throw "Missing or disabled control: $id" }
            $pattern = $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
            $pattern.Invoke()
        }
        $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'NoticeText')
        $deadline = (Get-Date).AddSeconds(5)
        do {
            $notice = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
            if ($notice.Current.Name -like '*local notification adapter is working*') { break }
            Start-Sleep -Milliseconds 100
        } while ((Get-Date) -lt $deadline)
        if ($notice.Current.Name -notlike '*local notification adapter is working*') { throw 'Notification action failed.' }
        return $count
    } finally {
        if (!$process.HasExited) {
            $null = $process.CloseMainWindow()
            if (!$process.WaitForExit(5000)) { $process.Kill(); throw 'App did not close cleanly.' }
        }
        $process.Dispose()
    }
}
$first = Run-Smoke
$second = Run-Smoke
if ($second -ne ($first + 1)) { throw 'Session did not persist across desktop restart.' }
Write-Output 'PASS: Windows launch, storage, in-app notice, close and restart persistence.'
