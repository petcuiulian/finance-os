# M6 focused security and recovery review

Reviewed local persistence, task exchange, automation, recovery and startup paths. This is a code review and automated regression record, not a penetration-test certificate.

- SQL values use bound parameters. Database connections enforce foreign keys; multi-row imports, bulk edits, rules and migrations are transactional.
- CSV exports escape spreadsheet formula prefixes. Imports have file/row limits, validation, duplicate-ID protection and rollback on malformed rows. Task exchange intentionally omits relational workspace data.
- Backup uses SQLite snapshots, temporary files and integrity checks. Restore validates before atomic replacement, retains the previous database, rejects incompatible schemas and recovery sidecars, and requires exclusive startup access. Pre-migration backups retain the old schema before any upgrade writes.
- Only one application process can hold the per-workspace file lock. The operating system releases it after process termination.
- Diagnostics contain versions and integrity status only. No remote diagnostics, telemetry, secret collection or network integration is enabled.
- Automated tests cover corrupt/future backup rejection, unchanged original data on failure, rollback, restore safety copies and a 10k-task dataset.

Remaining release work: independent security review, signed distribution, clean-machine acceptance, manual accessibility and lifecycle tests, configured data-retention UX, and an explicit encryption/key-recovery design if required by IT. Backups are sensitive, unencrypted workspace files. Do not delete migration/restore copies automatically until retention and recovery policy is approved.
