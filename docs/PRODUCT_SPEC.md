# FINANCE OS — Navigable product specification

## Authority
`MASTER_SPEC.md` contains the full, unchanged user specification (30 sections). This file is an implementation index, **not** a substitute. If a feature, edge case, or requirement isn't restated here, it is still required by the original specification. Use section references rather than repeatedly pasting the master into task prompts.

## Product definition and non-negotiables — master §§1–4, 20–22, 28–30
- Standalone premium Windows 10/11 corporate-finance productivity app; launch by double-click and distribute executable/installer/portable when possible. No dev tools or admin rights needed for ordinary use where technically possible; corporate IT controls respected.
- Local-first; core works offline without cloud accounts; minimal resources (cold start goal <3s, idle RAM target ~200 MB if feasible, measure rather than claim); no silent telemetry.
- Persist and protect tasks, notes, reminders and preferences. SQLite candidate; migrations, backup/restore, safe deletion, export, timezone, integrity, recovery. Store per-user application data outside executable.
- Reliable lifecycle and user-visible failures. Never silently lose or misrepresent work. No inert visible controls. Test on Windows before asserting Windows-specific success.

## Experience — master §§5–7, 15–17, 23
- World-class, restrained, visually coherent design: light/dark, accessible typography and focus, high-DPI support, intuitive navigation, responsive keyboard interactions, reduced motion.
- Desktop shell: navigation (Overview, My Day, Tasks, Calendar, Reminders, Workflows, Projects, Analytics, Archive, Settings), global search, quick add, notification center, customizable widgets, optional context panel.
- One shared dataset, **two presentation modes**: Simple (today's tasks, reminders, deadlines, mini calendar, quick notes) and Advanced (workflows, dependencies, filtering, automation, analytics etc.); toggling must preserve all data.
- Dashboard answers what is urgent/today/overdue/waiting/next; customizable widgets and layout persisted. Short skippable first-run onboarding, EN/RO localization. Optional micro-tools hidden unless needed.

## Task & planning domain — master §§8, 11–12, 16
- Rich task model: title, description, status, priority, category, project, start/due timestamps, duration, reminders, tags, checklists/subtasks, notes, references, requester, responsible person, external link, dependencies, recurrence, history.
- Default statuses: Not Started, In Progress, Waiting for Response, Blocked, Completed, Cancelled; user-definable Advanced statuses.
- List, Today, Upcoming, Overdue, Waiting, Completed, Kanban, Calendar, Timeline; sorting/filtering, undo, quick editing, drag/drop, bulk actions, templates, archive/restore, soft delete.
- Dedicated Waiting-for-Response flow with expected response date, follow-up date, contact, elapsed time, follow-up history, configurable business-day reminder and return to active.
- Daily/weekly/monthly/agenda calendar, time blocking, working hours, holidays, recurring events; planner only suggests and never silently changes deadlines.
- Global search across tasks, descriptions, notes, tags, references and archived items; fast with thousands of tasks; quick capture may parse date language locally without paid AI dependencies.

## Reminder subsystem — master §9 (release-critical)
- One-time, recurring, deadline-relative, multi-reminder, overdue and follow-up alerts; Windows toast/in-app center, optional sound, snooze/dismiss/action where supported.
- Respect DND, focus, work hours and weekends. Define recoverable states, duplicate prevention and overdue reconciliation after restart, crash, sleep or relaunch.
- Distinguish live app, tray/background, and explicitly authorized Windows startup/scheduling. **If app and any approved background mechanism are both stopped, do not promise delivery at that instant**; reconcile on next launch and make limitation visible.

## Corporate-finance modules — master §§10–11, 13–14
- Customizable templates: Intercompany Invoicing, Accounts Payable, Month-End Closing, Request Follow-Up and recurring controls; stage checklists, reminders, history and dependencies; no Bosch-specific internal hardcoding.
- Rule engine WHEN/IF/THEN with safe recursion/duplication/conflict controls, auditability and reversal where feasible. Advanced only, incremental implementation.
- Private productivity analytics: completed counts, turnaround, overdue %, workload, wait times and time tracking. Do not implement surveillance or pretend counts equal performance.

## Security, deployment and integrations — master §§18–19, 21–22
- Approved optional adapters for Outlook, Teams, Microsoft 365, SharePoint, Excel, OneDrive, SAP links, CSV. They are **future optional** features and must not block offline use. No scraping credentials or unauthorized access.
- Sensitive data minimal by design; safe logs, secret storage (Windows DPAPI or Credential Manager when needed), cautious exports, no third-party data collection. Encryption-at-rest, if used, needs explicit key recovery plan.
- Windows-native window, icon, tray, notification surfaces, DPI, window restoration, startup preference, keyboard shortcuts, appropriate packaging and IT-approved deployment.

## Acceptance and delivery — master §§24–29
- Robust failures and diagnostics, automated domain/integration/UI tests, migrations and backup/restore tests, calendar/reminder edge cases, actual Windows notification tests and reproducible Windows packaging.
- Deliver source, docs, tests, functional SQLite, build scripts, installer/portable if possible, user guide, limitations, security information and roadmap. A UI preview is never the final product.

## Section cross-reference
| Original § | Subject | Primary implementation area |
|---|---|---|
| 1–4 | Vision, requirements, technology | Architecture, shell |
| 5–7 | UI design, modes, dashboard | UI, preferences |
| 8–9 | Tasks, notification engine | Domain, persistence, Windows shell |
| 10–11 | Finance workflows and waiting | Workflows |
| 12–14 | Calendar, automation, analytics | Planning, rules, metrics |
| 15–17 | Personalization, capture, micro-tools | Settings, search, utilities |
| 18–20 | Integrations, security, database | Adapters, storage |
| 21–23 | Performance, desktop, onboarding | Performance, packaging, UX |
| 24–26 | Error handling, tests, execution | QA, roadmap |
| 27–30 | User scenarios, deliverables, standards | Acceptance, release |
