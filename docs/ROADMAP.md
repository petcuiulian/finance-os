# FINANCE OS — implementation roadmap

**Status notation:** `[ ]` not done, `[x]` verified done, `[-]` in progress. Mark complete only after implementation and applicable tests. All original requirements remain in scope (see `MASTER_SPEC.md`).

## M0 — Architecture and repository foundation
- [x] Compare Tauri 2 and .NET/WPF against corporate Windows, UX, WebView2, notifications and deployment needs; document ADR.
- [x] Choose stack, toolchain versions and a reproducible project scaffold; create basic app window and clean developer setup.
- [x] Establish formatting, typing, tests, repository scripts, lint and build smoke checks.
- [x] Establish persistent migration scheme, per-user data location and safe security defaults.
**Gate: PASS.** [CI run 41729389594](https://github.com/petcuiulian/finance-os/actions/runs/41729389594) verified implementation commit `fb65280` on Linux and Windows.
- Locked restore, Release C#/XAML build, seven integration tests and formatting/analyzers passed on both operating systems.
- Windows development and self-contained published apps launched; UI Automation exercised storage and notice controls, clean close and restart persistence.
- **UNVERIFIED — target Windows 10/11 laptop required:** visual/accessibility/DPI review, performance targets, clean-machine deployment and corporate signing/allowlisting. OS notifications/tray/resume remain M3/M6 work. These release checks are not claimed by M0's CI smoke test.

## M1 — Beautiful functional shell and design system
- [x] Implement light/dark semantic tokens, typography, reusable card/button/navigation styles and a resizable Windows shell.
- [x] Build real Overview and Settings views; show future modules as disabled navigation items.
- [x] Persist theme, language, mode and onboarding state; scaffold English/Romanian shell copy and first-run dismissal.
- [x] Implement Simple/Advanced presentation toggle bound to a persisted preference in the same SQLite workspace.
**Gate: PASS.** [CI run 41733304661](https://github.com/petcuiulian/finance-os/actions/runs/41733304661) verified implementation commit `9a95e50` on Linux and Windows. Locked restore, Release build, nine tests and formatting passed on both platforms. Windows UI Automation exercised development and published apps: navigation, buttons, theme/language/mode persistence, notice, shutdown and restart. Native buttons provide focus and access keys; future feature buttons are disabled.
- **UNVERIFIED — target Windows laptop/manual review required:** fine visual polish, contrast and assistive technology, high DPI and narrow-screen behavior. These remain release quality checks; UI behavior was exercised on a Windows hosted runner.

## M2 — Real local task manager
- [x] Define SQLite task/project/category/reference/checklist model with foreign keys, indexes and versioned migrations.
- [x] Implement create/edit/complete/archive/restore and recoverable delete; support status and priority.
- [x] Provide My Day, task list, due dates, quick capture, sorting/filtering and search; verify restart persistence and version 3 task upgrade.
- [x] Keep quick creation available in Simple Mode and preserve notes, due date and priority when advanced fields are hidden.
**Gate: PASS.** [CI run 41735248313](https://github.com/petcuiulian/finance-os/actions/runs/41735248313) verified implementation commit `974c63d` on Linux and Windows: locked restore, Release build, formatting and 15 tests. Windows development and published app smoke checks exercised task capture and visibility after restart. CRUD, filters, migration and Trash recovery are covered by integration tests.
- Project/category/reference/checklist tables are foundation for later editors; advanced workflow fields, reminders and calendar remain in M3–M5. No feature from the master specification is removed.
- **UNVERIFIED — target Windows laptop/manual review required:** accessibility, high DPI, visual polish, large data performance and corporate deployment. UI Automation covers the core quick-capture flow on a Windows hosted runner.

## M3 — Reminder and calendar reliability
- [ ] Design reminder DB, scheduling, recurrence, snooze, dismiss, duplicate prevention and missed-alarm reconciliation.
- [ ] In-app alerts and supported Windows toast integration; tray mode / approved startup behavior as appropriate.
- [ ] Test app restart, crash, sleep/resume, DST/time zone, overdue and missed notifications; document platform limitations.
- [ ] Implement daily/weekly/monthly calendar + agenda, time blocks and deadlines.
**Gate:** critical workflows verified; **Windows lifecycle/toast tests mandatory for final acceptance**.

## M4 — Corporate finance workflows
- [ ] Customizable workflow templates: Intercompany, AP, Month-End, Follow-Up, Recurring Controls.
- [ ] Waiting-for-Response: requester/contact, expected response and follow-up, business-day logic and history.
- [ ] Recurring tasks, dependencies, configurable statuses, editable Kanban and stage-based checklists.
**Gate:** realistic high-volume accountant scenario including approvals/requests/closing works without spreadsheets.

## M5 — Advanced productivity
- [ ] WHEN/IF/THEN rule engine with loop/conflict/idempotency safeguards and traceability.
- [ ] Time tracking, analytics (private local), planner suggestions, workload forecasts and customizable dashboard.
- [ ] Advanced views, bulk actions, saved filters, templates, global search and optional mini-tools.
- [ ] Optional adapter interfaces and offline CSV/JSON import/export; external integrations only when approved and configured.
**Gate:** Advanced mode adds real capabilities without making Simple Mode cluttered or requiring cloud.

## M6 — Hardening and Windows release
- [ ] Automated domain/integration/UI regression; realistic 1k+/10k task datasets where practical.
- [ ] Security review, backup/restore, migration recovery, corruption errors and redacted diagnostics.
- [ ] Accessibility and performance profiling (start target <3s, idle RAM target ~200MB if feasible); record actual metrics.
- [ ] Build and **test** Windows installer/portable executable on a Windows environment with no installed developer tools.
- [ ] Document IT approval, signature / allowlisting, WebView2/dependency requirements, setup, troubleshooting and known limitations.
**Gate:** independently verified desktop product; not just a working dev server.

## Current next task
**M3 — Reminder and calendar reliability (Astra High).** M2 is complete; M3 has not started. Keep the existing detailed M1–M6 milestones and all original features; the shorter model-routing table does not remove scope.

## Scope management
- No feature is canceled because it is in a later milestone.
- Record any material design tradeoff in `docs/ARCHITECTURE.md`.
- At the end of every Codex task, tick verified items, update the next concrete objective above, and append a short handoff to `docs/WORK_LOG.md`.
