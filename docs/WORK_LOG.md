# Durable work log

## 2026-11-04 — M0 foundation implemented; Windows gate pending
- Imported the supplied starter into the empty `work` branch. `MASTER_SPEC.md` is byte-for-byte unchanged (SHA256 `8d815acd90b17bd7c6acb73309bfc3616b66b3ab4e7d3e807e58f217dafb63c2`).
- Chose .NET 10/WPF over Tauri; see ADR 0001. Native Windows scope, no WebView2 dependency, single C#/XAML toolchain. SDK 10.0.403, Microsoft.Data.Sqlite 10.0.14, committed transitive locks.
- Added Core/Infrastructure/Desktop/test projects, typed storage and notification boundaries, light/dark semantic resource dictionaries, readable resizable shell, real database status/check action and in-app notice action. No fake task controls or OS toast claims. Theme selection and all M1 work remain pending.
- SQLite lives under per-user LocalApplicationData. Transactional schema version 1 and session counter verify persistence. Foreign keys on; rollback journal retained. Newer schema/corruption failures preserve files, errors omit paths/SQL, no automatic reset or startup/background services.
- Added locked restore/build/test/format scripts, unsigned self-contained Windows folder publish, Windows UI Automation smoke script and Linux/Windows CI.
- Fixed initial restore security failure by updating SQLite dependency to patched .NET 10 servicing release; no audit/verification suppression. Fixed publish lock mismatch by declaring win-x64 runtime graph for every project.
- **PASS (Linux):** `scripts/check.sh` — locked restore, Release cross-build including WPF XAML (0 warnings/errors), 7/7 xUnit tests, format/analyzer verification. Tests cover fresh migration/reopening, newer schema rejection, failed migration rollback, corruption preservation, concurrent cold starts, cancellation and per-user path policy.
- **PASS (Linux):** `dotnet publish src/FinanceOS.Desktop -c Release -r win-x64 --self-contained true -p:PublishTrimmed=false -p:PublishSingleFile=false -p:RestoreLockedMode=true -o artifacts/win-x64`. PE x64 GUI executable and SQLite DLL produced; whole output folder ~143 MB, not a RAM measurement.
- **UNVERIFIED — Windows required:** WPF development/published launch, smoke script execution, visual/keyboard/accessibility/DPI checks, notification/tray lifecycle, clean-machine packaging/signing. PowerShell scripts/CI supplied but not claimed executed here.
- Existing cloud SDK: `/workspace/.dotnet`; set PATH, DOTNET_CLI_HOME and NUGET_PACKAGES as in README. No repo worktree needed. App has no credentials or service prerequisites.
- **Next:** run Windows M0 launch/smoke checks and record their evidence; then hand off M1 design system to Sol Medium. Do not start M1 automatically. No features were removed or later milestones checked off.
