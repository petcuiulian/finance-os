# FINANCE OS

Offline-first Windows 10/11 finance productivity application. M0 introduces a native
.NET 10/WPF window, local SQLite bootstrap and an in-app notification adapter. Task
management and all later features remain planned in `docs/ROADMAP.md`; this is not a
finished finance product. M0 checks passed on Linux and Windows, including development
and published app launch/restart smoke tests ([CI evidence](https://github.com/petcuiulian/finance-os/actions/runs/41729389594)).

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

The smoke script requires an interactive Windows desktop, writes only the app's
per-user launch counter and opens/closes two instances sequentially. Do not run it
alongside another FINANCE OS instance. It verifies visible ready state, working
buttons and restart durability. It does not certify toasts or release readiness.

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

Windows data: `%LOCALAPPDATA%/FinanceOS/finance-os.db`. The database lives outside the
executable directory. The M0 schema stores a launch counter to exercise migrations
and restart durability; it contains no task schema yet. "Check local storage" reads
that database without incrementing the counter. "Test in-app notice" changes the
in-app message and sends no OS toast. Closing the window ends the process.

SQLite migrations and writes are transactional; foreign keys are enabled; newer
schemas and corrupt files fail without reset. If startup fails, preserve the database,
check directory permissions and the app version. Do not delete a database to fix an
upgrade. Backup/restore tooling is a later milestone. Error UI omits raw paths/SQL.
No app telemetry, cloud access or external integrations are enabled.

## Structure and handoff

- `src/FinanceOS.Core`: contracts and per-user path policy.
- `src/FinanceOS.Infrastructure`: SQLite lifecycle and migrations.
- `src/FinanceOS.Desktop`: composition, WPF shell, theme resources, notices.
- `tests/FinanceOS.Tests`: migration, corruption, concurrency and persistence tests.
- `docs/ADR-0001-desktop-stack.md`: Tauri vs WPF decision and platform risks.
- `docs/MASTER_SPEC.md`: unchanged complete original specification.
- `docs/ROADMAP.md` and `docs/WORK_LOG.md`: milestone status and next task.

Windows CI checks are configured in `.github/workflows/validate.yml`. Configuration
is not evidence of a successful run. Windows launch/rendering, accessibility, DPI,
notifications and clean-machine packaging must be reported separately from a Linux
cross-build. See the work log for checks actually executed.
