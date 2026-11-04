# Durable work log

## 2026-11-04 — M6 hardening; final release gate open
- Full SQLite online backup, integrity checks, offline validated restore with atomic replacement/safety copy, pre-migration snapshots, per-workspace exclusive instance lock, redacted diagnostics and guarded workflow async errors.
- Added portable ZIP/checksum packaging and extracted-package Windows smoke. Deployment/recovery and focused security review are in WINDOWS_RELEASE.md and SECURITY_REVIEW.md.
- PASS locally: 38 tests, Release build and formatting; NuGet direct/transitive audit reported no known vulnerabilities from the configured source. Linux synthetic 10k run: import 269 ms, search 11 ms, backup plus restore 99 ms. These are single cloud-run measurements, not target-device guarantees.
- Windows CI pending. Final gate remains OPEN: clean-PC acceptance, signing/IT approval, manual accessibility, physical sleep/reboot/toasts, representative hardware profiling and retained feature extensions.


## 2026-11-04 — M5 advanced productivity
- Schema v7 adds manual time entries, automation rules/history, completion events and saved productivity settings. Advanced page exposes workload metrics, daily completion bars, capacity suggestions, bulk priority updates, filters, task template and JSON/CSV task exchange.
- Rules support overdue/tomorrow triggers, text conditions and priority actions. First matching rule wins, transactions prevent duplicate actions, and guarded undo preserves later edits. No network integrations or messages.
- Timer explicitly includes elapsed time while closed; task exchange is not a full database backup and imports task fields without project/category relationships. Planner uses user-entered remaining capacity and a shared effort estimate; no deadlines change.
- PASS: [CI run 41809199980](https://github.com/petcuiulian/finance-os/actions/runs/41809199980), implementation `6ae77b5`: locked restore, Release build, 34 tests and formatting on Linux and Windows. Windows development/published desktop smoke includes Productivity dashboard rendering; native toast API smoke also passed.
- Remaining detailed master-spec extensions are retained in ROADMAP; physical M3/M4 checks and manual M5 usability/accessibility review remain open. Next implementation milestone is M6 hardening; do not treat task exchange as a full backup.


## 2026-11-04 — M4 workflow implementation and automated acceptance
- Added transactional schema v6 and five editable finance templates, editable Kanban stages/cards, stage checklists, display statuses and entry-relative stage deadlines/reminders.
- Added waiting-for-response with contact, expected/follow-up dates, business-day count/default, local reminder and durable history. Added daily/weekly/monthly task generation with month-end anchors and cycle-checked dependencies that block premature completion.
- **PASS:** [CI run 41807602883](https://github.com/petcuiulian/finance-os/actions/runs/41807602883), commit `b9c3fb8`: locked restore, Release build, formatting and 29/29 tests on Linux and Windows. Windows development/published smoke verified Kanban card visibility after process termination/restart; toast API smoke also passed. Integration tests cover workflow persistence, reminders, recurrence, dependencies and a synthetic 100-task AP/closing/approval scenario.
- **UNVERIFIED:** physical high-volume UI review. M3 sleep/reboot/tray/visible-toast target-device checks remain open by explicit separation. No external communication is sent.
- **Next:** M5 advanced productivity when requested, while retaining M3 physical validation and M4 physical UX review.

## 2026-11-04 — M3 implementation; lifecycle gate remains open
- Added schema v5 reminder definitions, unique durable inbox occurrences, persisted notification options and UTC time blocks. Existing schemas upgrade transactionally.
- Implemented recurrence/DST policies, snooze/dismiss/cancel, task navigation, quiet-hour/weekday toast eligibility, opt-in sound/toasts, explicit tray mode, launch/timer/resume reconciliation and calendar ranges with deadlines/time blocks.
- Toast submission is at-most-once per occurrence/snooze; the durable inbox remains available if a crash or policy blocks the OS effect. No automatic startup or background service was installed.
- **PASS:** [CI run 41804426325](https://github.com/petcuiulian/finance-os/actions/runs/41804426325), implementation `b681ed1`: locked restore, Release build, formatting, 24/24 tests on Linux and Windows. Windows development/published UI smoke verified reminder and calendar persistence after forced process termination. Native Windows toast smoke verified the submitted notification in OS history.
- Fixed overlapping timer/user reconciliation by serializing updates and preserving selected inbox alert. OS activation registration failure cannot block app startup.
- **UNVERIFIED:** physical laptop sleep/resume, Windows reboot, manual tray restoration, visible toast/DND behavior, accessibility/DPI and target-device performance. History receipt is not proof of a visible or read banner.
- **Next:** perform the target-device steps and finish remaining extensions in `M3_VALIDATION.md`. Keep M3 gate open; M4 has not started.

## 2026-11-04 — M2 complete; M3 next
- Added version 3 task/project/category/reference/checklist schema and version 4 Trash marker. Version 1–3 upgrades preserve existing settings and tasks. Due dates are local calendar dates; exact reminder instants/timezones remain M3 work.
- Added quick capture (Ctrl+N), Tasks/My Day/Archive/Trash views, task details, due date/priority, search/filter/sort, status actions and recoverable deletion. Simple Mode hides advanced editor fields while updates preserve their values. Both modes share one database.
- **PASS:** [CI run 41735248313](https://github.com/petcuiulian/finance-os/actions/runs/41735248313), implementation `974c63d`: locked restore, Release build, formatting and 15/15 tests on Linux and Windows; Windows development/published desktop smoke checks verified task capture and restart persistence. Tests cover migration, task lifecycle, search, due boundaries, foreign keys and Trash status preservation.
- Diagnosed earlier Windows test fixture failure: a pooled temporary SQLite connection blocked file cleanup. Fixed the fixture and reran the affected checks.
- **UNVERIFIED — target laptop/manual review:** visual/DPI/accessibility and high-volume performance. Project/category/checklist editors and the advanced master-spec task fields remain planned, not canceled.
- **Next:** M3 reminder scheduling and calendar reliability, with Windows lifecycle validation. Stop before M3 in this task.

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
