# FINANCE OS — implementation roadmap

**Status notation:** `[ ]` not done, `[x]` verified done, `[-]` in progress. Mark complete only after implementation and applicable tests. All original requirements remain in scope (see `MASTER_SPEC.md`).

## M0 — Architecture and repository foundation
- [x] Compare Tauri 2 and .NET/WPF against corporate Windows, UX, WebView2, notifications and deployment needs; document ADR.
- [x] Choose stack, toolchain versions and a reproducible project scaffold; create basic app window and clean developer setup.
- [x] Establish formatting, typing, tests, repository scripts, lint and build smoke checks.
- [x] Establish persistent migration scheme, per-user data location and safe security defaults.
**Gate status: implementation and Linux checks complete; Windows launch verification pending.**
- Verified: locked restore, Release C#/XAML cross-build (zero warnings), seven integration tests, formatting/analyzers, self-contained win-x64 publish.
- **UNVERIFIED — Windows required:** development launch, UI interaction/rendering, DPI/accessibility, published executable launch and clean-machine packaging. Run `scripts/check.ps1`, `dotnet run --project src/FinanceOS.Desktop`, then publish and `scripts/smoke-windows.ps1` on Windows.
- M0 cannot be called fully verified until the desktop launch gate passes. Windows CI is configured; configuration alone is not a pass.


## M1 — Beautiful functional shell and design system
- [ ] Implement premium light/dark design tokens, typography, UI primitives, sidebar/header, responsive Windows-friendly layout.
- [ ] Build navigation with genuine working app views or visibly disabled future views (never misleading controls).
- [ ] Add settings persistence, EN/RO i18n scaffolding and initial onboarding.
- [ ] Implement Simple/Advanced mode toggle bound to persistent preference and shared dataset.
**Gate:** working shell with responsive keyboard support, mode switch and persisted settings; UI checked, not just static preview.

## M2 — Real local task manager
- [ ] Define SQLite task/project/category/reference/checklist model, migrations and transactions.
- [ ] Implement create/edit/complete/archive/restore/delete safely; status and priority.
- [ ] My Day, task list, due dates, quick capture, sortable/filterable search; smoke tests and restart persistence.
- [ ] Ensure quick creation is effortless in Simple Mode and advanced fields can be added without data loss.
**Gate:** actually usable personal task manager; task data survives restart and version upgrade.

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
**Finish M0 Windows verification** using the documented launch/smoke commands. After that gate passes: **M1 — Design System (Sol Medium)**. M1 has not started. Keep the existing detailed M1–M6 milestones and all original features; the shorter model-routing table does not remove scope.

## Scope management
- No feature is canceled because it is in a later milestone.
- Record any material design tradeoff in `docs/ARCHITECTURE.md`.
- At the end of every Codex task, tick verified items, update the next concrete objective above, and append a short handoff to `docs/WORK_LOG.md`.
