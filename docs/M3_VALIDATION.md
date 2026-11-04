# M3 reminder and calendar validation

## Implemented behavior
- Multiple one-time, daily, weekly, monthly or fixed-minute-interval reminders per open task.
- UTC instants plus original local anchor and time-zone ID are stored in SQLite. Monthly recurrence clamps short months while retaining the original day for later months. Invalid initial DST times are rejected; recurring gap times move to the first valid minute, ambiguous times use the later instant.
- Reconciliation runs at launch, every 15 seconds while running, and after Windows resume. A transaction inserts the durable inbox occurrence and advances the schedule. A long missed recurring span is coalesced into one alert dated at its earliest missed occurrence; the next scheduled occurrence is in the future.
- Snooze (5/10/15/30/60 minutes, tomorrow at 09:00, custom time), dismissal, schedule cancellation, task navigation, and scheduled-reminder list.
- Completed/archived/trashed tasks suppress reminders and toast claims. Restoring/reopening an overdue task makes its eligible reminder visible on the next reconciliation.
- Toasts require explicit opt-in, persisted locally. Sound, weekday-only and 09:00–17:00 toast delivery are optional; quiet periods defer toast attempts, not durable inbox recovery. Windows handles its own Do Not Disturb rules.
- The inbox is authoritative. Toast attempts are marked before submission to avoid duplicate OS attempts after a crash. A crash in that gap or an OS rejection can omit a toast; the inbox entry remains. OS receipt/history is not proof that a banner was shown or read.
- Explicit tray mode keeps the process and scheduler alive. Closing/exiting stops them. No automatic startup or background service is registered. Opening a toast opens the inbox; snooze/dismiss actions are inside the app.
- Day/week/month/90-day agenda ranges show local task deadlines and time blocks. Blocks are stored as UTC intervals; overdue all-day task dates remain calendar dates. Time-block removal requires confirmation.

## Automated checks
`./scripts/check.sh` or `./scripts/check.ps1` runs platform-neutral tests, WPF compilation and formatting. Windows CI additionally exercises app creation/restart flows and `--toast-smoke`, which submits a synthetic toast and checks Windows notification history. The desktop smoke test forcibly terminates its first process after persisted changes and verifies inbox and time-block recovery in the next process.

Final automated evidence: [CI run 41804426325](https://github.com/petcuiulian/finance-os/actions/runs/41804426325), source `b681ed1`, passed 24 tests and builds/formatting on Linux and Windows. Both Windows desktop smoke runs passed forced-termination/relaunch recovery and calendar persistence. Native Windows notification-history verification passed. These are actual Windows runner results, not cross-build claims.

## Target-device acceptance still required
Use a disposable test profile/database; never change corporate clock/policy controls merely to run a test.
1. Enable Windows notifications, create a task reminder 2 minutes ahead, minimize explicitly to tray, and verify one notification and one inbox entry. Open the app from the tray and then by activating the toast.
2. Snooze 5 minutes; confirm no early toast, one renewed attempt when due, and persistent dismissal across restart.
3. Create a reminder, put the Windows laptop to sleep before it is due, wake it after the deadline. Verify one recovered inbox entry and one OS attempt; resume repeatedly and check for duplicates.
4. Repeat across an approved Windows restart. Since automatic startup is not enabled, no delivery is promised until FINANCE OS is launched.
5. Verify OS Do Not Disturb, disabled notification permissions, quiet hours and weekend options. The inbox must remain usable in every case.
6. Verify high DPI, keyboard navigation and screen-reader labels, and manually review calendar/reminder layouts in both themes.

Physical sleep/resume, OS restart, tray interaction and visible banners must not be marked PASS from unit tests or notification-history checks alone. Record actual device, Windows version, date and outcome before closing the M3 lifecycle gate.

## Further product scope
Per-reminder delivery overrides, configurable work-hour intervals/holidays, direct toast action buttons, drag-and-drop calendar rescheduling and richer calendar grids remain required product work. This implementation does not remove those requirements. No M4 work is started by M3 validation.
