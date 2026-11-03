# FINANCE OS — verification contract

## Test pyramid
- **Domain unit:** task status/priority, due dates, recurring rules, business days, waiting state, dependency cycles, automation loop/idempotence.
- **Persistence/integration:** CRUD transactions, migrations, referential integrity, archive/restore, backup/restore, import/export, safe handling of malformed data, restart durability.
- **UI flows:** quick add/edit/complete/snooze, Simple/Advanced switch, search, filters, dashboard widgets/settings retention, keyboard focus, dialogs and EN/RO.
- **Windows manual/automated:** initial run/install without dev tools, portable packaging if supported, WebView2 prerequisites, notifications, tray/background, optional startup consent, resume from sleep, restart, crash and missed-reminder reconciliation, DPI scaling and window placement.
- **Performance:** record measured cold/warm launch times, idle RAM/CPU, interaction latency, search/list scrolling with 1k–10k tasks on representative hardware.
- **Privacy:** no unapproved network calls or telemetry; log redaction; permissions; secret handling; encryption/key recovery if implemented.

## Task acceptance policy
- For each bounded coding task: tests relevant to the changed behavior; typecheck/lint if configured; relevant application build.
- For each milestone: broader integration and smoke testing.
- If toolchain/platform unavailable, report `NOT RUN` or `UNVERIFIED — Windows required`. Never label them PASS.
- Record commands and outcomes tersely in the work log; never paste thousands of lines of build output into the conversation.

## Essential manual scenarios from master §27
1. Simple: five tasks, reminders, completion, no confusion.
2. Finance: 35 daily requests plus intercompany and AP, pending approvals and exceptions.
3. Follow-up: missing requester info, due in two business days, visible Waiting status.
4. Month end: reusable closing checklist with stages/dependencies.
5. Laptop restart: tasks intact, missed reminders reconciled.
6. Advanced → Simple → Advanced: no loss of data or complex workflow state.
