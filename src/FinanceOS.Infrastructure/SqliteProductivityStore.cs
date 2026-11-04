using System.Globalization;
using FinanceOS.Core;
using Microsoft.Data.Sqlite;

namespace FinanceOS.Infrastructure;

public sealed class SqliteProductivityStore(string path)
{
    private SqliteConnection Open()
    {
        var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWrite, ForeignKeys = true, Pooling = false, DefaultTimeout = 10 }.ToString());
        try { db.Open(); return db; } catch { db.Dispose(); throw; }
    }
    private static SqliteCommand Cmd(SqliteConnection db, string sql, params (string, object?)[] args)
    {
        var c = db.CreateCommand(); c.CommandText = sql;
        foreach (var (key, value) in args) c.Parameters.AddWithValue(key, value ?? DBNull.Value);
        return c;
    }
    public Task<IReadOnlyDictionary<string, int>> CompletionTrendAsync() => Task.Run<IReadOnlyDictionary<string, int>>(() =>
    {
        using var db = Open(); using var c = Cmd(db, "SELECT substr(at_utc,1,10),COUNT(*) FROM completion_events WHERE at_utc>=date('now','-6 days') GROUP BY substr(at_utc,1,10) ORDER BY 1;");
        using var r = c.ExecuteReader(); var result = new Dictionary<string, int>();
        while (r.Read()) result.Add(r.GetString(0), r.GetInt32(1)); return result;
    });
    public Task SaveSettingAsync(string key, string value) => Task.Run(() =>
    {
        using var db = Open(); using var c = Cmd(db, "INSERT INTO productivity_settings VALUES($k,$v) ON CONFLICT(key) DO UPDATE SET value=excluded.value;", ("$k", key), ("$v", value)); c.ExecuteNonQuery();
    });
    public Task<IReadOnlyDictionary<string, string>> SettingsAsync() => Task.Run<IReadOnlyDictionary<string, string>>(() =>
    {
        using var db = Open(); using var c = Cmd(db, "SELECT key,value FROM productivity_settings;"); using var r = c.ExecuteReader();
        var result = new Dictionary<string, string>(); while (r.Read()) result.Add(r.GetString(0), r.GetString(1)); return result;
    });
    public Task StartTimerAsync(string taskId, DateTimeOffset now) => Task.Run(() =>
    {
        using var db = Open(); using var c = Cmd(db, "INSERT INTO time_entries SELECT $id,id,$now,NULL FROM tasks WHERE id=$task AND status='Open' AND deleted_utc IS NULL;",
            ("$id", Guid.NewGuid().ToString("N")), ("$task", taskId), ("$now", now.ToUnixTimeMilliseconds()));
        if (c.ExecuteNonQuery() != 1) throw new ArgumentException("Select an open task.");
    });
    public Task StopTimerAsync(DateTimeOffset now) => Task.Run(() =>
    {
        using var db = Open(); using var c = Cmd(db, "UPDATE time_entries SET end_utc=MAX(start_utc,$now) WHERE end_utc IS NULL;", ("$now", now.ToUnixTimeMilliseconds())); c.ExecuteNonQuery();
    });
    public Task<IReadOnlyList<TimeEntry>> TimeEntriesAsync() => Task.Run<IReadOnlyList<TimeEntry>>(() =>
    {
        using var db = Open(); using var c = Cmd(db, "SELECT id,task_id,start_utc,end_utc FROM time_entries ORDER BY start_utc DESC;"); using var r = c.ExecuteReader();
        var result = new List<TimeEntry>(); while (r.Read()) result.Add(new(r.GetString(0), r.GetString(1), DateTimeOffset.FromUnixTimeMilliseconds(r.GetInt64(2)), r.IsDBNull(3) ? null : DateTimeOffset.FromUnixTimeMilliseconds(r.GetInt64(3)))); return result;
    });
    public Task AddRuleAsync(string name, string trigger, string contains, string priority) => Task.Run(() =>
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120 || contains.Length > 300 || trigger is not ("Overdue" or "Due tomorrow") || priority is not ("Low" or "Normal" or "High" or "Urgent")) throw new ArgumentException("Invalid rule.");
        using var db = Open(); using var c = Cmd(db, "INSERT INTO automation_rules VALUES($id,$name,$trigger,$contains,$priority,1);",
            ("$id", Guid.NewGuid().ToString("N")), ("$name", name.Trim()), ("$trigger", trigger), ("$contains", contains), ("$priority", priority)); c.ExecuteNonQuery();
    });
    public Task<IReadOnlyList<AutomationRule>> RulesAsync() => Task.Run<IReadOnlyList<AutomationRule>>(() =>
    {
        using var db = Open(); using var c = Cmd(db, "SELECT id,name,trigger,contains_text,priority FROM automation_rules WHERE enabled=1 ORDER BY rowid;"); using var r = c.ExecuteReader();
        var result = new List<AutomationRule>(); while (r.Read()) result.Add(new(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4))); return result;
    });
    public Task DisableRuleAsync(string id) => Task.Run(() => { using var db = Open(); using var c = Cmd(db, "UPDATE automation_rules SET enabled=0 WHERE id=$id;", ("$id", id)); c.ExecuteNonQuery(); });
    public Task<int> RunRulesAsync(DateOnly today) => Task.Run(() =>
    {
        using var db = Open(); using var tx = db.BeginTransaction();
        using var query = Cmd(db, """
            SELECT r.id,t.id,t.priority,r.priority,t.due_date FROM automation_rules r JOIN tasks t
            WHERE r.enabled=1 AND t.status='Open' AND t.deleted_utc IS NULL
            AND instr(lower(t.title||' '||t.notes),lower(r.contains_text))>0
            AND ((r.trigger='Overdue' AND t.due_date<$today) OR (r.trigger='Due tomorrow' AND t.due_date=$tomorrow))
            ORDER BY r.rowid,t.id;
            """, ("$today", today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), ("$tomorrow", today.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        query.Transaction = tx; var changes = new List<(string Rule, string Task, string Before, string After, string Date)>();
        using (var r = query.ExecuteReader()) while (r.Read()) changes.Add((r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4)));
        var claimed = new HashSet<string>(); var count = 0;
        foreach (var change in changes)
        {
            // First matching rule wins, even on retries. Rules cannot trigger one another.
            if (!claimed.Add(change.Task)) continue;
            using var history = Cmd(db, "INSERT OR IGNORE INTO automation_history VALUES($id,$rule,$task,$date,$before,$after,0);",
                ("$id", Guid.NewGuid().ToString("N")), ("$rule", change.Rule), ("$task", change.Task), ("$date", change.Date), ("$before", change.Before), ("$after", change.After));
            history.Transaction = tx; if (history.ExecuteNonQuery() == 0) continue;
            using var update = Cmd(db, "UPDATE tasks SET priority=$priority WHERE id=$task;", ("$priority", change.After), ("$task", change.Task)); update.Transaction = tx; update.ExecuteNonQuery(); count++;
        }
        tx.Commit(); return count;
    });
    public Task<IReadOnlyList<AutomationChange>> HistoryAsync() => Task.Run<IReadOnlyList<AutomationChange>>(() =>
    {
        using var db = Open(); using var c = Cmd(db, "SELECT id,task_id,before_priority,after_priority,undone FROM automation_history ORDER BY rowid DESC;"); using var r = c.ExecuteReader();
        var result = new List<AutomationChange>(); while (r.Read()) result.Add(new(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetBoolean(4))); return result;
    });
    public Task UndoAsync(string id) => Task.Run(() =>
    {
        using var db = Open(); using var tx = db.BeginTransaction();
        using var c = Cmd(db, """
            UPDATE tasks SET priority=(SELECT before_priority FROM automation_history WHERE id=$id)
            WHERE id=(SELECT task_id FROM automation_history WHERE id=$id AND undone=0)
            AND priority=(SELECT after_priority FROM automation_history WHERE id=$id);
            """, ("$id", id)); c.Transaction = tx;
        if (c.ExecuteNonQuery() != 1) throw new InvalidOperationException("Task changed since automation; undo was not applied.");
        c.CommandText = "UPDATE automation_history SET undone=1 WHERE id=$id;"; c.ExecuteNonQuery(); tx.Commit();
    });
    public Task SetPrioritiesAsync(IEnumerable<string> taskIds, string priority) => Task.Run(() =>
    {
        if (priority is not ("Low" or "Normal" or "High" or "Urgent")) throw new ArgumentException("Invalid priority.");
        using var db = Open(); using var tx = db.BeginTransaction();
        foreach (var id in taskIds.Distinct())
        {
            using var c = Cmd(db, "UPDATE tasks SET priority=$p WHERE id=$id AND deleted_utc IS NULL;", ("$p", priority), ("$id", id)); c.Transaction = tx;
            if (c.ExecuteNonQuery() != 1) throw new ArgumentException("A selected task no longer exists.");
        }
        tx.Commit();
    });
    public Task<int> ImportAsync(IReadOnlyList<WorkTask> tasks) => Task.Run(() =>
    {
        if (tasks.Count > 10000) throw new ArgumentException("Import limit is 10,000 tasks.");
        using var db = Open(); using var tx = db.BeginTransaction(); var count = 0;
        foreach (var t in tasks)
        {
            if (string.IsNullOrWhiteSpace(t.Id) || string.IsNullOrWhiteSpace(t.Title) || t.Title.Length > 300 || t.Notes is null || t.Notes.Length > 100000 || t.Status is not ("Open" or "Done" or "Archived") || t.Priority is not ("Low" or "Normal" or "High" or "Urgent")) throw new ArgumentException("Invalid task in import.");
            using var c = Cmd(db, """
                INSERT OR IGNORE INTO tasks(id,title,notes,due_date,priority,status,created_utc,updated_utc)
                VALUES($id,$title,$notes,$due,$priority,$status,$created,$updated);
                """, ("$id", t.Id), ("$title", t.Title), ("$notes", t.Notes), ("$due", t.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                ("$priority", t.Priority), ("$status", t.Status), ("$created", t.CreatedUtc.ToString("O")), ("$updated", t.UpdatedUtc.ToString("O")));
            c.Transaction = tx; count += c.ExecuteNonQuery();
        }
        tx.Commit(); return count;
    });
}
