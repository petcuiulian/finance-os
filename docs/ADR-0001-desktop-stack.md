# ADR 0001 — Native .NET/WPF desktop

Accepted for M0, 2026-11-04. Windows runtime validation remains outstanding.

| Criterion | Tauri 2 + React/TypeScript | .NET 10 + WPF |
| --- | --- | --- |
| Corporate deployment | WebView2 must be present or distributed under IT policy; Rust and JS toolchains | Self-contained per-user folder, no WebView2; still subject to IT allowlisting |
| Footprint | Small host but separate web runtime processes | Larger self-contained distribution; no browser runtime; measure actual RAM/startup on Windows |
| UI craftsmanship | Strong CSS/component ecosystem | Native vector styling, resource dictionaries, animation, high DPI; custom premium styling requires care |
| Accessibility | Semantic web UI and WebView accessibility bridge | Native controls, keyboard focus and UI Automation; visual/assistive testing still required |
| Notifications | Windows plugin/identity/lifecycle constraints | Direct Windows integration; identity, toast activation and lifecycle constraints still apply |
| Offline storage | Rust SQLite and IPC boundary | Microsoft.Data.Sqlite, typed in-process boundaries, no IPC attack surface |
| Maintainability | Rust + TypeScript + frontend dependency ecosystem | C#/XAML, one toolchain, shared platform-neutral domain/storage tests |
| Distribution | Windows toolchain, WebView2 bootstrap/fixed runtime, signed installer | Windows-targeted build, self-contained folder first; signed installer/update strategy in M6 |

Choose .NET 10 LTS/WPF because Windows is the exclusive target and native integration,
corporate deployment and reliability outweigh web component reuse. No product feature
is removed. The master explicitly permits this choice. Native WPF does not establish
IT approval or eliminate signing requirements. Windows 10 support must be checked
against the enterprise's supported OS servicing edition and .NET runtime policy.

SDK 10.0.403 (`global.json`, latest patch roll-forward); C# compiler supplied by SDK;
Microsoft.Data.Sqlite 10.0.14; transitive package locks committed. NuGet sources retain
TLS/signature verification. .NET analyzers, nullable checks and warnings-as-errors
are enabled. xUnit tests run on Linux and Windows. WPF cross-compilation on Linux
checks C#/XAML, not rendering, notifications or Windows runtime behavior.

Boundaries: Core contains contracts and path policy; Infrastructure owns SQLite;
Desktop composes dependencies and owns WPF lifecycle and in-app notifications.
Future domain/scheduler/reporting/integration modules follow the original architecture;
no speculative services or task schema in M0. Views must not own SQL or scheduling.

Storage is `%LOCALAPPDATA%/FinanceOS/finance-os.db`, never beside the executable.
Schema version is SQLite `user_version`. Migrations and session writes share one
immediate transaction; foreign keys are enabled; rollback journal and SQLite's default
FULL synchronous durability are retained. Newer schemas are rejected, failed migrations
roll back, corrupt files are preserved. No automatic delete/reset/recovery. Backup/restore
and explicit migration history evolve in M6 before real upgrades require them. No
credentials or corporate fixtures; no network clients, telemetry or automatic startup.
Unencrypted SQLite relies on per-user filesystem access, not application encryption.

M0's notification adapter produces in-app notices only and reports system capability
as false. M3 must persist reminder state before delivery, reconcile missed events, and
verify Windows toasts/actions, sleep/restart, tray and consent-based startup. Exiting
M0 stops the app; it promises no background alarms.

Build path: `scripts/check.ps1`, `scripts/publish-windows.ps1`, then
`scripts/smoke-windows.ps1` on a Windows interactive desktop. A Windows CI job is
provided, but its result is not claimed until it runs. Linux can compile the Windows
target and run storage tests. Signing, installer, real Windows launch, accessibility,
DPI and performance are separate unverified release checks.
