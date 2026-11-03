# Durable work log

## 2026-11-04 — M1 complete; M2 next
- Added the WPF Overview and Settings shell, semantic light/dark themes, custom button/card/navigation styles with focus and disabled states, first-run welcome, English/Romanian shell text and disabled future module links.
- SQLite schema v2 migrates M0 databases in one transaction and persists theme, language, mode and onboarding state. Simple/Advanced uses the same database; no task schema or M2 feature was started.
- **PASS:** [CI run 41733304661](https://github.com/petcuiulian/finance-os/actions/runs/41733304661), implementation `9a95e50`: locked restore, Release build, format/analyzers and 9/9 tests on Linux and Windows. Windows development and published app smoke checks exercised navigation, settings changes, in-app notice and restart persistence. Linux `scripts/check.sh` also passed.
- **UNVERIFIED — target laptop/manual review:** visual contrast, DPI, assistive technology and narrow layout. Corporate release checks and OS notifications remain later milestones.
- **Next:** M2 task/project schema and usable local task manager. Stop before M2 in this task.

## 2026-11-04 — M0 COMPLETE — READY FOR SOL MEDIUM
- Imported the supplied starter into the empty `work` branch. `MASTER_SPEC.md` is byte-for-byte unchanged (SHA256 `8d815acd90b17bd7c6acb73309bfc3616b66b3ab4e7d3e807e58f217dafb63c2`).
- Chose .NET 10/WPF over Tauri; see ADR 0001. Native Windows scope, no WebView2 dependency, single C#/XAML toolchain. SDK 10.0.403, Microsoft.Data.Sqlite 10.0.14, committed transitive locks.
- Added Core/Infrastructure/Desktop/test projects, typed storage and notification boundaries, light/dark semantic resource dictionaries, readable resizable shell, real database status/check action and in-app notice action. No fake task controls or OS toast claims. Theme selection and all M1 work remain pending.
- SQLite lives under per-user LocalApplicationData. Transactional schema version 1 and session counter verify persistence. Foreign keys on; rollback journal retained. Newer schema/corruption failures preserve files, errors omit paths/SQL, no automatic reset or startup/background services.
- Added locked restore/build/test/format scripts, unsigned self-contained Windows folder publish, Windows UI Automation smoke script and Linux/Windows CI.
- Fixed initial restore security failure by updating SQLite dependency to patched .NET 10 servicing release; no audit/verification suppression. Fixed publish lock mismatch by declaring win-x64 runtime graph for every project.
- **PASS (Linux):** `scripts/check.sh` — locked restore, Release cross-build including WPF XAML (0 warnings/errors), 7/7 xUnit tests, format/analyzer verification. Tests cover fresh migration/reopening, newer schema rejection, failed migration rollback, corruption preservation, concurrent cold starts, cancellation and per-user path policy.
- **PASS (Linux):** `dotnet publish src/FinanceOS.Desktop -c Release -r win-x64 --self-contained true -p:PublishTrimmed=false -p:PublishSingleFile=false -p:RestoreLockedMode=true -o artifacts/win-x64`. PE x64 GUI executable and SQLite DLL produced; whole output folder ~143 MB, not a RAM measurement.
- **PASS (actual Windows CI):** [run 41729389594](https://github.com/petcuiulian/finance-os/actions/runs/41729389594), implementation commit `fb65280d22832fd349d02c2400c5b5f82df262a4`: locked restore, Release build, 7/7 tests, formatting/analyzers, development executable smoke, self-contained publish and published executable smoke. Both smoke runs verified visible storage ready state, storage/notice button actions, clean shutdown and restart counter persistence via Windows UI Automation. The Linux CI job also passed.
- **UNVERIFIED — target Windows 10/11 laptop required:** visual/keyboard/accessibility/DPI review, notification/tray lifecycle, performance targets and clean-machine packaging/signing. Windows hosted-runner smoke is not corporate deployment certification.
- Existing cloud SDK: `/workspace/.dotnet`; set PATH, DOTNET_CLI_HOME and NUGET_PACKAGES as in README. No repo worktree needed. App has no credentials or service prerequisites.
- Windows CI first run passed build and 7 tests but caught Git CRLF checkout conflicting with EditorConfig. Added explicit LF checkout policy, preserving original master CRLF; added development-output smoke alongside published-output smoke.
- **Next:** M1 design system with Sol Medium. M0 is implemented and its applicable checks pass. Stop here; no M1 implementation or later milestone was started. All original features remain in scope.
