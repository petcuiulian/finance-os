# FINANCE OS

Offline-first Windows 10 (build 19041+)/11 finance productivity application. M0–M3 provide a native .NET 10/WPF shell, persistent preferences and a working local task manager. Quick Add, My Day, search, editing, completion, Archive and recoverable Trash work without login or network access. Reminders, notification inbox, Windows toasts and calendar ranges are implemented; advanced finance workflows remain on the roadmap. This is not the finished release product.

## Develop

Install the .NET SDK pinned in `global.json` (10.0.403; newer patches in the same
feature band are accepted). Use the existing checkout; cloud tasks are already
isolated and do not need extra Git worktrees. No database server, login or secrets
are required. Restore uses nuget.org and its official package endpoints.

From the repository root on Windows:

```powershell
./scripts/check.ps1
dotnet run --project src/FinanceOS.Desktop
./scripts/publish-windows.ps1
./scripts/smoke-windows.ps1
```

The smoke script requires an interactive Windows desktop, creates a harmless local
"M2 smoke task", and opens/closes two instances sequentially. Do not run it
alongside another FINANCE OS instance. It verifies visible ready state, working
buttons, task capture and restart durability. It does not certify toasts or release readiness.

The unsigned self-contained output is `artifacts/win-x64/FinanceOS.exe`. Distribute
its **whole folder**, including native SQLite libraries. No administrator elevation
is requested. Corporate execution policy/allowlisting still applies. Installer,
signing, update/recovery and clean-PC acceptance belong to M6.

On Linux (cross-build and platform-neutral tests only):

```bash
./scripts/check.sh
```

In the prepared cloud workspace, first activate the installed SDK/cache:

```bash
export PATH="/workspace/.dotnet:$PATH"
export DOTNET_CLI_HOME=/workspace/.dotnet-home
export NUGET_PACKAGES=/workspace/.nuget/packages
export DOTNET_CLI_TELEMETRY_OPTOUT=1
```

Network-dependent commands may need the environment's approved execution access.
Keep proxy/TLS verification intact. `dotnet format FinanceOS.slnx --no-restore`
applies formatting; `check` verifies it and runs compiler/analyzer checks. Package
locks are committed; update them deliberately with `dotnet restore --force-evaluate`
after dependency changes, then run all checks.

## Data and safety

Windows data: `%LOCALAPPDATA%/FinanceOS/finance-os.db`. The database lives outside the executable directory. Schema v5 migrates previous versions in place. It stores preferences, tasks, projects, categories, references, checklist items and a recoverable Trash marker. Task actions currently cover the core list workflow; project/category/checklist editors and advanced workflow states remain later work.

Use **Ctrl+N** to focus Quick Add, type a title and press Enter. Tasks and My Day show open work; choose a row to edit details or change its state. Simple mode keeps advanced details hidden while preserving them. Advanced mode exposes notes, date and priority. Archive and Trash have dedicated views; Restore returns a trashed task to its previous status. Move to Trash never erases the row. The app stores due *dates* as local calendar dates; reminder instants and original time zones are stored separately.

Light/dark theme, EN/RO language and Simple/Advanced mode controls save immediately to the same SQLite database. The first-run welcome can be dismissed. Future modules are shown as disabled navigation items. "Check local storage" reads the database without incrementing the counter. "Test in-app notice" changes the in-app message and sends no OS toast. Closing the window ends the process.

SQLite migrations and writes are transactional; foreign keys are enabled; newer
schemas and corrupt files fail without reset. If startup fails, preserve the database,
check directory permissions and the app version. Do not delete a database to fix an
upgrade. Backup/restore tooling is a later milestone. Error UI omits raw paths/SQL.
No app telemetry, cloud access or external integrations are enabled.

## Reminders and calendar

Select an open task, then open Reminders to add one-time or recurring reminders. Multiple reminders may reference the same task. Daily/weekly/monthly schedules retain a local-time anchor and timezone; custom intervals use elapsed minutes. The inbox survives restart and supports snooze, dismiss, schedule cancellation and navigation to the task.

Windows notifications are opt-in. Sound, weekday-only delivery and 09:00–17:00 delivery options persist locally. Keep FINANCE OS open or use its explicit tray command for live reminders. Exiting stops delivery until the next launch; no automatic startup or background service is installed. Missed reminders are recovered into the durable inbox. A toast attempt can be suppressed by Windows or interrupted by a crash; the inbox remains authoritative.

Calendar offers day/week/month and a 90-day agenda, showing deadlines and time blocks. See [M3 validation](docs/M3_VALIDATION.md) for recurrence rules, verification evidence and the physical sleep/resume and tray checks still required on a target laptop.

## Structure and handoff

- `src/FinanceOS.Core`: contracts, task models and per-user path policy.
- `src/FinanceOS.Infrastructure`: SQLite migrations, task queries and writes.
- `src/FinanceOS.Desktop`: composition, WPF shell, theme resources, notices.
- `tests/FinanceOS.Tests`: migrations, integrity, task lifecycle and persistence tests.
- `docs/ADR-0001-desktop-stack.md`: Tauri vs WPF decision and platform risks.
- `docs/MASTER_SPEC.md`: unchanged complete original specification.
- `docs/ROADMAP.md` and `docs/WORK_LOG.md`: milestone status and next task.

Windows CI checks are configured in `.github/workflows/validate.yml`. Configuration
is not evidence of a successful run. Windows launch/rendering, accessibility, DPI,
notifications and clean-machine packaging must be reported separately from a Linux
cross-build. See the work log for checks actually executed.
