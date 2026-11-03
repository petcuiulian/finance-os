# FINANCE OS — Codex working agreements

## Mission
Build a real, premium, fast, secure, offline-first Windows 10/11 desktop productivity app for finance professionals. Deliver a double-clickable application, not mockups. Both Simple and Advanced modes share the same persistent data.

## Source of truth / scope
- `docs/MASTER_SPEC.md`: complete, unchanged original requirements. **Do not silently drop requirements.**
- `docs/PRODUCT_SPEC.md`: concise navigation by feature; original master wins in a conflict.
- `docs/ARCHITECTURE.md`: proposed technical decisions and known platform constraints.
- `docs/DESIGN_SYSTEM.md`: visual design and UX direction.
- `docs/ROADMAP.md`: ordered milestones and acceptance checks.
- `docs/TEST_PLAN.md`: quality and regression gates.
- `docs/WORK_LOG.md`: concise durable handoff.
- Read this file and the **relevant** docs/modules first. Consult specific original-spec sections for details, not the entire master for every small task. If implementation differs from master, document the reason and get approval for scope reductions.

## How to work (token-efficient, not corner-cutting)
1. Complete **one bounded milestone or feature slice** per task; no speculative rewrite of the entire app.
2. Inspect only necessary files and short command outputs; search targeted paths and patterns before opening large files. Do not reprint large files or logs.
3. Make the smallest correct integrated change. Favor straightforward code over abstractions, agents, or extra dependencies.
4. Keep Windows corporate deployment, local privacy, data integrity, recovery and accessibility as release gates.
5. Do not claim tests/builds passed unless executed. If Windows-specific checks cannot run here, label them **UNVERIFIED — Windows required**.
6. Run relevant targeted tests and lint/typecheck; run broad regression/build checks at milestone boundaries. Fix failures attributable to changes.
7. Never ship nonfunctional buttons as functional. Mark optional unavailable integrations explicitly; do not invent API access.
8. Update `docs/ROADMAP.md` status and `docs/WORK_LOG.md` after meaningful work, so a fresh task can continue without a long chat history.
9. Final response: **changed files; working behavior; tests actually run; blockers/next milestone**, concise.
10. Avoid parallel agents or broad research unless independent complex work really warrants them. Work sequentially by default.

## Platform / security rules
- Never bypass IT controls, enterprise authentication, app allowlisting, or install restrictions.
- No corporate data, credentials, or real invoice details in demos, logs, screenshots, fixtures, or commits.
- Local-first, offline core; no required login, telemetry, cloud, background mail reads, or unauthorized Microsoft/SAP integrations.
- Persist user data under per-user app-data location, **outside** the executable directory; migration/backup/recovery must be safe.
- Reminder guarantees depend on Windows lifecycle: distinguish app running, tray mode and explicitly approved startup/background mechanisms. Reconcile missed reminders after resume/relaunch; don't promise always-on alarms when app isn't running.
- Desktop packaging must ultimately be verified on Windows. Do not misrepresent a Linux/cloud build as tested Windows executable.

## Scope boundaries
- Follow `docs/ROADMAP.md` and keep all advanced features planned. Working MVP first, not a smaller final scope.
- Only ask questions if access, security compliance, or an irreversible architectural choice blocks progress. Otherwise decide sensibly and record assumptions.
