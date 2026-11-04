# Proposed architecture — decision log

**Status:** .NET 10/WPF selected in [ADR 0001](ADR-0001-desktop-stack.md). Core and SQLite are platform-neutral; Windows development/published launch smoke passed in CI; target-laptop and signed release packaging checks remain pending. The original logical modules and all product features remain in scope.

## Original candidate evaluated (superseded by ADR 0001)
- **Desktop:** Tauri 2 / Rust host, React + TypeScript frontend, Vite, Tailwind CSS, shadcn/ui and Lucide; local SQLite persisted under Windows per-user AppData.
- **Why candidate:** rich premium UI, efficient shell compared to a full embedded Chromium runtime, modular TypeScript views, Rust host for privileged I/O and OS integration.
- **Primary risks:** installed WebView2 availability on locked-down PCs; signed packaging/SmartScreen; OS notification semantics across minimized/tray/closed states; permitted startup mechanisms; complexity of Rust build and Tauri plugins.
- **Fallback to assess:** native .NET 8+/WPF (deployment options, resources, OS integration, corporate allowlisting) if Tauri assumptions fail. Do not change stack impulsively once settled.

## Logical modules (no microservices)
1. `desktop`: window, tray, lifecycle, background/startup preferences, OS notification adapter.
2. `ui`: design system, routes, dashboard, Simple/Advanced presentation, keyboard access.
3. `domain`: tasks, projects, workflow stages, reminders, recurrence, follow-ups, rules, planning.
4. `persistence`: SQLite access, transactions, schema migrations, backup/restore, typed contracts.
5. `scheduler`: due-event reconciliation, snooze, idempotent notification delivery, recovery.
6. `reporting`: workload and productivity aggregates; private, local only.
7. `integration`: optional approved adapters (disabled by default), import/export.
8. `diagnostics`: redacted logs, crash-safe errors and diagnostics export.

## Guardrails and design decisions
- Keep domain decisions separate from WPF views and SQLite statements. Validate inputs; use least privileges and restricted file handling.
- Model due times/time zones explicitly; store absolute instants with time-zone metadata as needed for user-facing recurrences. Define DST and business-day behavior.
- Transact all multi-step updates; use versioned migrations and compatibility checks. Prefer SQLite foreign-key enforcement, indexes for date/status/search, and a safe backup method.
- Task/reminder states must be persisted before publishing notification effects. Design **idempotent** reconcile process to avoid duplicate alarms on resume/restart.
- Never assume OS toasts fire while the app is fully exited. App-in-tray and approved startup are distinct design cases; missed reminders appear on next run.
- Windows notifications/actions require real OS validation. Feature-detect or mark unsupported paths; no fake UI.
- Per-user data remains intact when executable upgrades/replacements occur. Safe import/export, migration and restore.
- Zero remote data transfer by default. Document any optional integration's permissions, auditability and user enablement.

## M0 decision record
ADR 0001 evaluates candidate stacks against: WebView2 availability, corporate policy, distribution, lightweight footprint, notifications, offline storage, accessibility, UI craftsmanship, maintainability and verified Windows build path. Record selected stack and tradeoffs before creating source scaffolding.

## M1 persistence extension
SQLite schema v2 adds one typed, constrained workspace preference row for theme, language, mode and first-run completion. Version 1 databases migrate transactionally without replacing the launch counter. Both presentation modes use this same local database; task entities remain M2 scope.

## M2 task data
SQLite schema v3 adds tasks, projects, categories, external references and checklist rows with foreign keys and status/due indexes. Schema v4 adds a deletion marker without replacing existing task rows; Trash hides rows from normal views and Restore clears the marker while keeping the prior task status. Due dates are local calendar dates; exact instants, recurrence and reminder state require explicit timezone rules in M3. Single-row task changes are SQLite atomic statements. WPF views call a typed task store; the same database backs both presentation modes. Optional future task fields are planned, not removed.

## M3 reminder decisions
Schema v5 adds notification consent/options, reminder schedules, uniquely keyed durable alerts and UTC time blocks. Reconciliation is transactional; OS toast attempts are at-most-once per occurrence/snooze, while the durable inbox provides recovery if OS submission is lost or denied. Recurrence uses its saved timezone and local anchor; custom intervals use elapsed UTC minutes. See `M3_VALIDATION.md` for DST, missed-occurrence and lifecycle semantics. The WPF target is Windows 10 build 19041 or later for native toast integration. Automatic startup is not registered.

## M4 workflow decisions
Schema v6 keeps five seeded but editable workflow templates in the same SQLite database. Stage positions, per-task card assignment and stage checklists are durable. A stage's optional deadline is computed when a card enters it and stored separately from the task's original due date; its optional reminder is canceled when the card moves or completes. Custom status is a display layer over the task's Open/Done/Archived lifecycle, so workflow labels cannot bypass dependency completion checks. Waiting-for-response records contact, dates and a history trail and creates a one-time local 09:00 follow-up reminder; completing or resolving the task cancels it. Business days currently skip weekends and can accept a holiday set in the domain API, but no holiday-calendar editor exists yet. Recurring rules generate dated task instances transactionally, preserve month-end anchors and reconcile on launch/day rollover; no external requests or automatic messages are sent. M4 automated volume coverage uses synthetic data and does not replace physical UI review.

## M5 productivity decisions
Schema v7 records completion transitions prospectively rather than guessing completion dates from historical edits. Manual timers allow one active entry and persist across process exit. The planner proposes deadline/priority order against explicitly supplied remaining capacity and a shared effort estimate; it does not write schedules. Rules are deliberately non-recursive: a transactional snapshot selects the first matching enabled rule per task, keyed by rule/task/deadline, with guarded reversal. More trigger/action types remain extensions. CSV exports escape spreadsheet formula prefixes; import validates all rows transactionally and never overwrites an existing ID. JSON/CSV task exchange excludes workflow/reminder data and is not backup/restore. Optional adapters have no credentials or automatic network execution.
