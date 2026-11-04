using FinanceOS.Core;
using Microsoft.Data.Sqlite;

namespace FinanceOS.Infrastructure;

public sealed class SqliteAppStore : IAppStore
{
    public const int CurrentSchemaVersion = 7;
    private readonly string _path;

    public SqliteAppStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Path.IsPathFullyQualified(path))
            throw new ArgumentException("Database path must be absolute.", nameof(path));
        _path = path;
    }

    // SQLite's synchronous disk work is kept off the desktop dispatcher.
    public Task<StorageStatus> OpenSessionAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => OpenSession(cancellationToken), cancellationToken);

    private StorageStatus OpenSession(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        using var connection = OpenConnection();
        using (var probe = connection.CreateCommand())
        {
            probe.CommandText = "PRAGMA user_version;";
            var existingVersion = Convert.ToInt32(probe.ExecuteScalar());
            if (existingVersion > 0 && existingVersion < CurrentSchemaVersion)
                DatabaseRecovery.Snapshot(_path, _path + ".before-v" + CurrentSchemaVersion + "-" + Guid.NewGuid().ToString("N") + ".db");
        }
        using var transaction = connection.BeginTransaction();
        var version = Convert.ToInt32(ExecuteScalar(connection, transaction, "PRAGMA user_version;"));
        if (version > CurrentSchemaVersion)
            throw new InvalidOperationException("This database requires a newer version of FINANCE OS.");
        if (version == 0)
        {
            Execute(connection, transaction, """
                CREATE TABLE app_state (
                    id INTEGER PRIMARY KEY CHECK (id = 1),
                    launch_count INTEGER NOT NULL CHECK (launch_count >= 0)
                );
                INSERT INTO app_state (id, launch_count) VALUES (1, 0);
                PRAGMA user_version = 1;
                """);
        }
        if (version <= 1)
        {
            Execute(connection, transaction, """
                CREATE TABLE workspace_preferences (
                    id INTEGER PRIMARY KEY CHECK (id = 1),
                    theme TEXT NOT NULL CHECK (theme IN ('Light', 'Dark')),
                    language TEXT NOT NULL CHECK (language IN ('en', 'ro')),
                    mode TEXT NOT NULL CHECK (mode IN ('Simple', 'Advanced')),
                    onboarding_complete INTEGER NOT NULL CHECK (onboarding_complete IN (0, 1))
                );
                INSERT INTO workspace_preferences VALUES (1, 'Light', 'en', 'Simple', 0);
                PRAGMA user_version = 2;
                """);
        }
        if (version <= 2)
        {
            Execute(connection, transaction, """
                CREATE TABLE projects (
                    id TEXT PRIMARY KEY, name TEXT NOT NULL CHECK (length(trim(name)) BETWEEN 1 AND 120),
                    created_utc TEXT NOT NULL
                );
                CREATE TABLE categories (
                    id TEXT PRIMARY KEY, name TEXT NOT NULL CHECK (length(trim(name)) BETWEEN 1 AND 80)
                );
                CREATE TABLE tasks (
                    id TEXT PRIMARY KEY, title TEXT NOT NULL CHECK (length(trim(title)) BETWEEN 1 AND 300),
                    notes TEXT NOT NULL DEFAULT '', due_date TEXT,
                    priority TEXT NOT NULL DEFAULT 'Normal' CHECK (priority IN ('Low','Normal','High','Urgent')),
                    status TEXT NOT NULL DEFAULT 'Open' CHECK (status IN ('Open','Done','Archived')),
                    project_id TEXT REFERENCES projects(id) ON DELETE SET NULL,
                    category_id TEXT REFERENCES categories(id) ON DELETE SET NULL,
                    created_utc TEXT NOT NULL, updated_utc TEXT NOT NULL
                );
                CREATE INDEX ix_tasks_status_due ON tasks(status, due_date);
                CREATE INDEX ix_tasks_project ON tasks(project_id);
                CREATE TABLE task_references (
                    id TEXT PRIMARY KEY, task_id TEXT NOT NULL REFERENCES tasks(id) ON DELETE CASCADE,
                    label TEXT NOT NULL, value TEXT NOT NULL
                );
                CREATE INDEX ix_task_references_task ON task_references(task_id);
                CREATE TABLE checklist_items (
                    id TEXT PRIMARY KEY, task_id TEXT NOT NULL REFERENCES tasks(id) ON DELETE CASCADE,
                    title TEXT NOT NULL, is_done INTEGER NOT NULL DEFAULT 0 CHECK (is_done IN (0,1)),
                    position INTEGER NOT NULL
                );
                CREATE INDEX ix_checklist_items_task ON checklist_items(task_id, position);
                PRAGMA user_version = 3;
                """);
        }
        if (version <= 3)
        {
            Execute(connection, transaction, """
                ALTER TABLE tasks ADD COLUMN deleted_utc TEXT;
                CREATE INDEX ix_tasks_deleted ON tasks(deleted_utc);
                PRAGMA user_version = 4;
                """);
        }
        if (version <= 4)
        {
            Execute(connection, transaction, """
                CREATE TABLE notification_options (id INTEGER PRIMARY KEY CHECK(id=1), toasts INTEGER NOT NULL,
                    sound INTEGER NOT NULL, working_hours INTEGER NOT NULL, weekdays INTEGER NOT NULL);
                INSERT INTO notification_options VALUES(1,0,0,0,0);
                CREATE TABLE reminders (
                    id TEXT PRIMARY KEY, task_id TEXT NOT NULL REFERENCES tasks(id),
                    due_utc INTEGER NOT NULL, anchor_local TEXT NOT NULL, timezone TEXT NOT NULL,
                    recurrence TEXT NOT NULL CHECK(recurrence IN ('Once','Daily','Weekly','Monthly','Interval')),
                    interval_minutes INTEGER NOT NULL CHECK(interval_minutes BETWEEN 1 AND 525600),
                    enabled INTEGER NOT NULL DEFAULT 1 CHECK(enabled IN (0,1))
                );
                CREATE INDEX ix_reminders_due ON reminders(enabled,due_utc);
                CREATE TABLE reminder_alerts (
                    id TEXT PRIMARY KEY, reminder_id TEXT NOT NULL REFERENCES reminders(id),
                    scheduled_utc INTEGER NOT NULL, available_utc INTEGER NOT NULL,
                    dismissed INTEGER NOT NULL DEFAULT 0, toast_attempted INTEGER NOT NULL DEFAULT 0,
                    UNIQUE(reminder_id,scheduled_utc)
                );
                CREATE INDEX ix_alerts_available ON reminder_alerts(dismissed,available_utc);
                CREATE TABLE time_blocks (
                    id TEXT PRIMARY KEY, title TEXT NOT NULL CHECK(length(trim(title)) BETWEEN 1 AND 300),
                    start_utc INTEGER NOT NULL, end_utc INTEGER NOT NULL CHECK(end_utc>start_utc), timezone TEXT NOT NULL
                );
                CREATE INDEX ix_time_blocks_start ON time_blocks(start_utc,end_utc);
                PRAGMA user_version = 5;
                """);
        }
        if (version <= 5)
        {
            Execute(connection, transaction, """
                CREATE TABLE workflow_templates (id TEXT PRIMARY KEY, name TEXT NOT NULL UNIQUE);
                CREATE TABLE workflow_stages (id TEXT PRIMARY KEY, template_id TEXT NOT NULL REFERENCES workflow_templates(id) ON DELETE CASCADE,
                    name TEXT NOT NULL, position INTEGER NOT NULL, due_offset_days INTEGER, reminder_minutes INTEGER,
                    UNIQUE(template_id,position));
                CREATE TABLE task_workflows (task_id TEXT PRIMARY KEY REFERENCES tasks(id) ON DELETE CASCADE,
                    template_id TEXT NOT NULL REFERENCES workflow_templates(id), stage_id TEXT NOT NULL REFERENCES workflow_stages(id));
                CREATE TABLE task_stage_deadlines (task_id TEXT PRIMARY KEY REFERENCES tasks(id) ON DELETE CASCADE,
                    stage_id TEXT NOT NULL REFERENCES workflow_stages(id), due_date TEXT NOT NULL,
                    reminder_id TEXT REFERENCES reminders(id));
                CREATE TABLE task_waiting (task_id TEXT PRIMARY KEY REFERENCES tasks(id) ON DELETE CASCADE,
                    contact TEXT NOT NULL, expected_date TEXT, followup_date TEXT NOT NULL, started_utc TEXT NOT NULL, reminder_id TEXT REFERENCES reminders(id), active INTEGER NOT NULL CHECK(active IN (0,1)));
                CREATE TABLE waiting_history (id TEXT PRIMARY KEY, task_id TEXT NOT NULL REFERENCES tasks(id) ON DELETE CASCADE,
                    action TEXT NOT NULL, note TEXT NOT NULL, at_utc TEXT NOT NULL);
                CREATE INDEX ix_waiting_followup ON task_waiting(active,followup_date);
                CREATE TABLE task_dependencies (task_id TEXT NOT NULL REFERENCES tasks(id) ON DELETE CASCADE,
                    depends_on_id TEXT NOT NULL REFERENCES tasks(id) ON DELETE CASCADE,
                    PRIMARY KEY(task_id,depends_on_id), CHECK(task_id!=depends_on_id));
                CREATE TABLE recurring_rules (id TEXT PRIMARY KEY, source_task_id TEXT NOT NULL REFERENCES tasks(id),
                    recurrence TEXT NOT NULL CHECK(recurrence IN ('Daily','Weekly','Monthly')),
                    next_date TEXT NOT NULL, anchor_day INTEGER NOT NULL CHECK(anchor_day BETWEEN 1 AND 31), enabled INTEGER NOT NULL DEFAULT 1 CHECK(enabled IN (0,1)));
                CREATE TABLE recurring_instances (rule_id TEXT NOT NULL REFERENCES recurring_rules(id),
                    occurrence_date TEXT NOT NULL, task_id TEXT NOT NULL REFERENCES tasks(id), PRIMARY KEY(rule_id,occurrence_date));
                CREATE TABLE custom_statuses (id TEXT PRIMARY KEY, name TEXT NOT NULL UNIQUE);
                ALTER TABLE tasks ADD COLUMN custom_status_id TEXT REFERENCES custom_statuses(id);
                ALTER TABLE checklist_items ADD COLUMN stage_id TEXT REFERENCES workflow_stages(id);
                INSERT INTO workflow_templates VALUES ('intercompany','Intercompany Invoicing'),('ap','Accounts Payable'),
                    ('month-end','Month-End Closing'),('follow-up','Request Follow-Up'),('controls','Recurring Financial Controls');
                INSERT INTO workflow_stages(id,template_id,name,position) VALUES
                    ('ic-receive','intercompany','Receive request',0),('ic-validate','intercompany','Validate information',1),
                    ('ic-process','intercompany','Process invoice',2),('ic-verify','intercompany','Verify posting',3),('ic-close','intercompany','Close',4),
                    ('ap-receive','ap','Receive invoice',0),('ap-docs','ap','Check documentation',1),('ap-approval','ap','Verify approval',2),
                    ('ap-resolve','ap','Resolve discrepancies',3),('ap-process','ap','Process invoice',4),('ap-close','ap','Confirm completion',5),
                    ('me-prepare','month-end','Prepare closing checklist',0),('me-reconcile','month-end','Reconcile accounts',1),
                    ('me-exceptions','month-end','Resolve exceptions',2),('me-controls','month-end','Complete controls',3),
                    ('me-report','month-end','Submit reporting',4),('me-close','month-end','Confirm closure',5),
                    ('fu-received','follow-up','Request received',0),('fu-info','follow-up','Information needed',1),
                    ('fu-wait','follow-up','Waiting for requester',2),('fu-response','follow-up','Response received',3),
                    ('fu-close','follow-up','Completed',4),
                    ('rc-daily','controls','Daily controls',0),('rc-weekly','controls','Weekly checks',1),
                    ('rc-monthly','controls','Monthly reconciliation',2),('rc-quarterly','controls','Quarterly reporting',3),
                    ('rc-yearly','controls','Year-end procedures',4);
                PRAGMA user_version = 6;
                """);
        }
        if (version <= 6)
        {
            Execute(connection, transaction, """
                CREATE TABLE productivity_settings (key TEXT PRIMARY KEY, value TEXT NOT NULL);
                CREATE TABLE time_entries (id TEXT PRIMARY KEY, task_id TEXT NOT NULL REFERENCES tasks(id),
                    start_utc INTEGER NOT NULL, end_utc INTEGER CHECK(end_utc>=start_utc));
                CREATE UNIQUE INDEX ix_single_timer ON time_entries((1)) WHERE end_utc IS NULL;
                CREATE TABLE automation_rules (id TEXT PRIMARY KEY, name TEXT NOT NULL, trigger TEXT NOT NULL,
                    contains_text TEXT NOT NULL, priority TEXT NOT NULL, enabled INTEGER NOT NULL DEFAULT 1);
                CREATE TABLE automation_history (id TEXT PRIMARY KEY, rule_id TEXT NOT NULL REFERENCES automation_rules(id),
                    task_id TEXT NOT NULL REFERENCES tasks(id), occurrence TEXT NOT NULL, before_priority TEXT NOT NULL,
                    after_priority TEXT NOT NULL, undone INTEGER NOT NULL DEFAULT 0, UNIQUE(rule_id,task_id,occurrence));
                CREATE TABLE completion_events (id INTEGER PRIMARY KEY, task_id TEXT NOT NULL REFERENCES tasks(id), at_utc TEXT NOT NULL);
                CREATE TRIGGER track_completion AFTER UPDATE OF status ON tasks
                    WHEN NEW.status='Done' AND OLD.status!='Done'
                    BEGIN INSERT INTO completion_events(task_id,at_utc) VALUES(NEW.id,strftime('%Y-%m-%dT%H:%M:%SZ','now')); END;
                PRAGMA user_version=7;
                """);
        }
        cancellationToken.ThrowIfCancellationRequested();
        Execute(connection, transaction, "UPDATE app_state SET launch_count = launch_count + 1 WHERE id = 1;");
        var launches = Convert.ToInt64(ExecuteScalar(connection, transaction, "SELECT launch_count FROM app_state WHERE id = 1;")
            ?? throw new InvalidDataException("Application state is missing."));
        transaction.Commit();
        return new StorageStatus(CurrentSchemaVersion, launches);
    }

    public Task<StorageStatus> ReadStatusAsync(CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = OpenConnection(readOnly: true);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT launch_count FROM app_state WHERE id = 1;";
        var launches = Convert.ToInt64(command.ExecuteScalar()
            ?? throw new InvalidDataException("Application state is missing."));
        command.CommandText = "PRAGMA user_version;";
        return new StorageStatus(Convert.ToInt32(command.ExecuteScalar()), launches);
    }, cancellationToken);

    public Task<WorkspacePreferences> ReadPreferencesAsync(CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = OpenConnection(readOnly: true);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT theme, language, mode, onboarding_complete FROM workspace_preferences WHERE id = 1;";
        using var reader = command.ExecuteReader();
        if (!reader.Read()) throw new InvalidDataException("Workspace preferences are missing.");
        return new WorkspacePreferences(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3) == 1);
    }, cancellationToken);

    public Task SavePreferencesAsync(WorkspacePreferences preferences, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        ArgumentNullException.ThrowIfNull(preferences);
        if (preferences.Theme is not ("Light" or "Dark") || preferences.Language is not ("en" or "ro") ||
            preferences.Mode is not ("Simple" or "Advanced"))
            throw new ArgumentException("Invalid workspace preference.", nameof(preferences));
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE workspace_preferences SET theme = $theme, language = $language,
            mode = $mode, onboarding_complete = $onboarding WHERE id = 1;
            """;
        command.Parameters.AddWithValue("$theme", preferences.Theme);
        command.Parameters.AddWithValue("$language", preferences.Language);
        command.Parameters.AddWithValue("$mode", preferences.Mode);
        command.Parameters.AddWithValue("$onboarding", preferences.OnboardingComplete ? 1 : 0);
        if (command.ExecuteNonQuery() != 1) throw new InvalidDataException("Workspace preferences are missing.");
        cancellationToken.ThrowIfCancellationRequested();
        transaction.Commit();
    }, cancellationToken);

    private SqliteConnection OpenConnection(bool readOnly = false)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _path,
            Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
            Pooling = false,
            DefaultTimeout = 10
        }.ToString());
        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static object? ExecuteScalar(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
