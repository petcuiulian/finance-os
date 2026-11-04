# Windows release candidate

This is an unsigned x64 portable release candidate, not an IT-approved final release.

## Run and upgrade

Windows 10 build 19041 or later, or Windows 11, x64. Extract the entire ZIP to a user-writable application folder and launch `FinanceOS.exe`. The folder includes the .NET runtime and native SQLite library; no separate .NET SDK/runtime or WebView2 is required. Keep all files together. Verify the ZIP checksum against the CI artifact's `package-sha256.txt`; `SHA256SUMS.txt` lists extracted file checksums. These checks detect accidental damage, not publisher authenticity. Corporate IT must approve the unsigned binary and its origin; do not bypass application controls or antivirus.

Before upgrading, create a backup in Settings, close the app (including tray mode), and replace the executable folder. Data lives in `%LOCALAPPDATA%\FinanceOS\finance-os.db`, outside the installation. Do not downgrade the executable against a newer database. Pre-migration snapshots are retained next to the database. The app does not silently remove them; manage retention according to company policy after verifying a separate backup.

## Backup and recovery

Settings → Back up workspace uses SQLite's online snapshot API, then checks integrity and foreign keys before publishing the backup. Backups contain all workspace data, including private notes and contacts. Store them in an approved protected location. Database and backup files are not application-encrypted; Windows profile permissions and company disk encryption remain relevant. No authentication secrets are stored.

For restore, close all instances and database tools, preserve the current database and any `-wal`, `-shm` or `-journal` files together, then run:

```powershell
.\FinanceOS.exe --restore "C:\approved-backups\finance-os-backup.db"
```

Confirm the in-app replacement prompt. The app verifies a staged copy, rejects corruption and future schemas, applies supported migrations to that copy, and atomically replaces the workspace. A `.before-restore-<id>.db` copy of the prior workspace is retained. Existing SQLite recovery sidecars block restore; do not delete them to get past the check. Failed validation leaves the workspace intact. Restore requires exclusive app access; the app holds a file lock across Windows sessions. Task CSV/JSON is not a substitute for full backup.

If normal startup fails, preserve all files first. Settings → Export diagnostics outputs only app/schema/runtime/OS versions and integrity status; it excludes task text, contacts, file paths and exception stacks. An app that cannot start needs the visible error and the app version supplied to support. Never upload the database without explicit organizational authorization.

## Verification and outstanding acceptance

Automated CI builds and tests on Linux/Windows, runs development and self-contained desktop UI smoke, native toast history smoke, then extracts the portable ZIP and repeats desktop smoke. Hosted-runner launch-to-ready time and memory are printed as `MEASURE` in the smoke log. These are observations on CI hardware, not cold-start or idle-memory certification on an office laptop. A hosted runner has developer tools; passing there is not proof of a clean-PC install.

Before final release, test on an approved clean Windows 10/11 laptop with no developer tools: extraction and double-click launch, offline use, backup/restore, upgrade, 125/150/200% DPI, keyboard-only navigation, screen reader, high contrast, 10k-task scrolling, CPU/RAM after idle, cold/warm start, tray recovery, physical sleep/resume, reboot and visible toast/DND behavior. See M3_VALIDATION.md and TEST_PLAN.md. Publisher signing, allowlisting, final installer choice, manual accessibility and lifecycle evidence remain open. No self-updater, startup service or external integration is installed.

The detailed functional carryovers from M2–M5 remain in ROADMAP and MASTER_SPEC. This candidate must not be represented as every original requirement being complete.
