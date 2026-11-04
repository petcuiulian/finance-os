using Microsoft.Data.Sqlite;

namespace FinanceOS.Infrastructure;

public static class DatabaseRecovery
{
    private static SqliteConnection Open(string path, SqliteOpenMode mode)
    {
        var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = mode, Pooling = false, ForeignKeys = true, DefaultTimeout = 10 }.ToString());
        try { db.Open(); return db; } catch { db.Dispose(); throw; }
    }
    private static void DifferentPaths(string source, string destination)
    {
        if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(destination), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new ArgumentException("Choose a different backup file.");
    }
    public static Task BackupAsync(string source, string destination) => Task.Run(() => Snapshot(source, destination));
    public static void Snapshot(string source, string destination)
    {
        DifferentPaths(source, destination);
        var temporary = destination + ".partial-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var original = Open(source, SqliteOpenMode.ReadOnly))
            using (var backup = Open(temporary, SqliteOpenMode.ReadWriteCreate)) original.BackupDatabase(backup);
            CheckIntegrity(temporary);
            File.Move(temporary, destination, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static void CheckIntegrity(string path)
    {
        using var db = Open(path, SqliteOpenMode.ReadOnly); using var c = db.CreateCommand();
        c.CommandText = "PRAGMA integrity_check;";
        if (!string.Equals(c.ExecuteScalar() as string, "ok", StringComparison.Ordinal)) throw new InvalidDataException("Database integrity check failed.");
        c.CommandText = "PRAGMA foreign_key_check;";
        using var r = c.ExecuteReader(); if (r.Read()) throw new InvalidDataException("Database references are inconsistent.");
    }
    // Caller must hold the application instance lock and stop all stores/timers before restoring.
    public static async Task<string?> RestoreOfflineAsync(string source, string destination)
    {
        DifferentPaths(source, destination);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        if (new[] { "-wal", "-shm", "-journal" }.Any(s => File.Exists(destination + s)))
            throw new InvalidOperationException("Database recovery files exist. Preserve the database and its sidecars before restoring.");
        var temporary = destination + ".restore-" + Guid.NewGuid().ToString("N");
        try
        {
            Snapshot(source, temporary);
            using (var db = Open(temporary, SqliteOpenMode.ReadOnly))
            using (var c = db.CreateCommand())
            {
                c.CommandText = "PRAGMA user_version;"; var version = Convert.ToInt32(c.ExecuteScalar());
                if (version < 1 || version > SqliteAppStore.CurrentSchemaVersion) throw new InvalidDataException("Unsupported backup schema.");
            }
            await new SqliteAppStore(temporary).OpenSessionAsync();
            CheckIntegrity(temporary);
            // Probe the complete current schema, not only a user_version marker.
            using (var db = Open(temporary, SqliteOpenMode.ReadOnly))
            using (var c = db.CreateCommand())
            {
                foreach (var table in new[] { "app_state", "workspace_preferences", "projects", "categories", "tasks", "task_references", "checklist_items", "notification_options", "reminders", "reminder_alerts", "time_blocks", "workflow_templates", "workflow_stages", "task_workflows", "task_stage_deadlines", "task_waiting", "waiting_history", "task_dependencies", "recurring_rules", "recurring_instances", "custom_statuses", "productivity_settings", "time_entries", "automation_rules", "automation_history", "completion_events" })
                {
                    c.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$name;";
                    c.Parameters.Clear(); c.Parameters.AddWithValue("$name", table);
                    if (c.ExecuteScalar() is null) throw new InvalidDataException("Backup is missing required workspace tables.");
                }
                c.Parameters.Clear();
                c.CommandText = "SELECT theme,language,mode FROM workspace_preferences; SELECT title,notes,custom_status_id FROM tasks; SELECT stage_id FROM checklist_items; SELECT due_utc FROM reminders; SELECT trigger FROM automation_rules; SELECT start_utc FROM time_entries;";
                using var r = c.ExecuteReader(); do { while (r.Read()) { } } while (r.NextResult());
            }
            string? safetyCopy = null;
            if (File.Exists(destination))
            {
                safetyCopy = destination + ".before-restore-" + Guid.NewGuid().ToString("N") + ".db";
                File.Replace(temporary, destination, safetyCopy);
            }
            else File.Move(temporary, destination);
            return safetyCopy;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
