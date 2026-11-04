using System.Globalization;
using FinanceOS.Core;
using Microsoft.Data.Sqlite;

namespace FinanceOS.Infrastructure;

public sealed class SqliteWorkflowStore(string path)
{
    private SqliteConnection Open()
    {
        var db = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = path, Mode = SqliteOpenMode.ReadWrite, ForeignKeys = true, Pooling = false, DefaultTimeout = 10 }.ToString());
        try { db.Open(); return db; } catch { db.Dispose(); throw; }
    }
    private static SqliteCommand Command(SqliteConnection db, string sql, params (string, object?)[] values)
    {
        var cmd = db.CreateCommand(); cmd.CommandText = sql;
        foreach (var (key, value) in values) cmd.Parameters.AddWithValue(key, value ?? DBNull.Value);
        return cmd;
    }
    private static string Date(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static string ValidName(string value, int max = 120)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > max) throw new ArgumentException($"Enter 1–{max} characters.");
        return value.Trim();
    }

    public Task<IReadOnlyList<WorkflowTemplate>> TemplatesAsync() => Task.Run<IReadOnlyList<WorkflowTemplate>>(() =>
    {
        using var db = Open(); using var cmd = Command(db, "SELECT id,name FROM workflow_templates ORDER BY name;");
        var result = new List<WorkflowTemplate>(); using var reader = cmd.ExecuteReader();
        while (reader.Read()) result.Add(new(reader.GetString(0), reader.GetString(1)));
        return result;
    });
    public Task<string> CreateTemplateAsync(string name) => Task.Run(() =>
    {
        var id = Guid.NewGuid().ToString("N"); using var db = Open();
        using var cmd = Command(db, "INSERT INTO workflow_templates VALUES($id,$name);", ("$id", id), ("$name", ValidName(name)));
        cmd.ExecuteNonQuery(); return id;
    });
    public Task RenameTemplateAsync(string id, string name) => Task.Run(() =>
    {
        using var db = Open(); using var cmd = Command(db, "UPDATE workflow_templates SET name=$name WHERE id=$id;", ("$id", id), ("$name", ValidName(name)));
        if (cmd.ExecuteNonQuery() != 1) throw new KeyNotFoundException("Template missing.");
    });
    public Task<IReadOnlyList<WorkflowStage>> StagesAsync(string templateId) => Task.Run<IReadOnlyList<WorkflowStage>>(() =>
    {
        using var db = Open(); using var cmd = Command(db, "SELECT id,template_id,name,position FROM workflow_stages WHERE template_id=$id ORDER BY position;", ("$id", templateId));
        var result = new List<WorkflowStage>(); using var reader = cmd.ExecuteReader();
        while (reader.Read()) result.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3)));
        return result;
    });
    public Task<string> AddStageAsync(string templateId, string name) => Task.Run(() =>
    {
        var id = Guid.NewGuid().ToString("N"); using var db = Open(); using var transaction = db.BeginTransaction();
        using var cmd = Command(db, """
            INSERT INTO workflow_stages(id,template_id,name,position)
            SELECT $id,id,$name,COALESCE((SELECT MAX(position)+1 FROM workflow_stages WHERE template_id=$template),0)
            FROM workflow_templates WHERE id=$template;
            """, ("$id", id), ("$name", ValidName(name)), ("$template", templateId));
        cmd.Transaction = transaction;
        if (cmd.ExecuteNonQuery() != 1) throw new KeyNotFoundException("Template missing.");
        transaction.Commit(); return id;
    });
    public Task RenameStageAsync(string id, string name) => Task.Run(() =>
    {
        using var db = Open(); using var cmd = Command(db, "UPDATE workflow_stages SET name=$name WHERE id=$id;", ("$id", id), ("$name", ValidName(name)));
        if (cmd.ExecuteNonQuery() != 1) throw new KeyNotFoundException("Stage missing.");
    });
    public Task MoveStageAsync(string id, int delta) => Task.Run(() =>
    {
        if (delta is not (-1 or 1)) throw new ArgumentException("Move by one position.");
        using var db = Open(); using var transaction = db.BeginTransaction();
        string template; int position;
        using (var get = Command(db, "SELECT template_id,position FROM workflow_stages WHERE id=$id;", ("$id", id)))
        { get.Transaction = transaction; using var row = get.ExecuteReader(); if (!row.Read()) throw new KeyNotFoundException("Stage missing."); template = row.GetString(0); position = row.GetInt32(1); }
        string? other;
        using (var get = Command(db, "SELECT id FROM workflow_stages WHERE template_id=$t AND position=$p;", ("$t", template), ("$p", position + delta)))
        { get.Transaction = transaction; other = get.ExecuteScalar() as string; }
        if (other is null) return;
        using var swap = Command(db, """
            UPDATE workflow_stages SET position=-1 WHERE id=$id;
            UPDATE workflow_stages SET position=$old WHERE id=$other;
            UPDATE workflow_stages SET position=$new WHERE id=$id;
            """, ("$id", id), ("$other", other), ("$old", position), ("$new", position + delta));
        swap.Transaction = transaction; swap.ExecuteNonQuery(); transaction.Commit();
    });
    public Task<IReadOnlyList<WorkflowCard>> CardsAsync(string templateId) => Task.Run<IReadOnlyList<WorkflowCard>>(() =>
    {
        using var db = Open(); using var cmd = Command(db, """
            SELECT t.id,t.title,s.id,s.name,COALESCE((SELECT name FROM custom_statuses WHERE id=t.custom_status_id),t.status) FROM task_workflows w
            JOIN tasks t ON t.id=w.task_id JOIN workflow_stages s ON s.id=w.stage_id
            WHERE w.template_id=$id AND t.deleted_utc IS NULL AND t.status!='Archived'
            ORDER BY s.position,t.title;
            """, ("$id", templateId));
        var result = new List<WorkflowCard>(); using var reader = cmd.ExecuteReader();
        while (reader.Read()) result.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4)));
        return result;
    });
    public Task AssignTaskAsync(string taskId, string templateId, string stageId) => Task.Run(() =>
    {
        using var db = Open(); using var transaction = db.BeginTransaction();
        using var cmd = Command(db, """
            INSERT INTO task_workflows(task_id,template_id,stage_id)
            SELECT t.id,s.template_id,s.id FROM tasks t JOIN workflow_stages s ON s.id=$stage
            WHERE t.id=$task AND t.deleted_utc IS NULL AND s.template_id=$template
            ON CONFLICT(task_id) DO UPDATE SET template_id=excluded.template_id,stage_id=excluded.stage_id;
            """, ("$task", taskId), ("$stage", stageId), ("$template", templateId));
        cmd.Transaction = transaction;
        if (cmd.ExecuteNonQuery() != 1) throw new ArgumentException("Select a task and stage from the same workflow.");
        using (var cancel = Command(db, "UPDATE reminders SET enabled=0 WHERE id=(SELECT reminder_id FROM task_stage_deadlines WHERE task_id=$task);", ("$task", taskId)))
        { cancel.Transaction = transaction; cancel.ExecuteNonQuery(); }
        using (var clear = Command(db, "DELETE FROM task_stage_deadlines WHERE task_id=$task;", ("$task", taskId)))
        { clear.Transaction = transaction; clear.ExecuteNonQuery(); }
        using var settings = Command(db, "SELECT due_offset_days,reminder_minutes FROM workflow_stages WHERE id=$stage;", ("$stage", stageId));
        settings.Transaction = transaction;
        using (var row = settings.ExecuteReader())
        {
            if (row.Read() && !row.IsDBNull(0))
            {
                var due = DateOnly.FromDateTime(DateTime.Today).AddDays(row.GetInt32(0));
                var minutes = row.IsDBNull(1) ? (int?)null : row.GetInt32(1);
                row.Close();
                string? reminderId = null;
                if (minutes is not null)
                {
                    reminderId = Guid.NewGuid().ToString("N");
                    var local = due.ToDateTime(new TimeOnly(9, 0)).AddMinutes(-minutes.Value);
                    var zone = TimeZoneInfo.Local.Id;
                    using var reminder = Command(db, """
                        INSERT INTO reminders(id,task_id,due_utc,anchor_local,timezone,recurrence,interval_minutes)
                        VALUES($id,$task,$due,$local,$zone,'Once',60);
                        """, ("$id", reminderId), ("$task", taskId),
                        ("$due", ReminderSchedule.ToUtc(local, zone, shiftGap: true).ToUnixTimeMilliseconds()),
                        ("$local", local.ToString("O", CultureInfo.InvariantCulture)), ("$zone", zone));
                    reminder.Transaction = transaction; reminder.ExecuteNonQuery();
                }
                using var deadline = Command(db, "INSERT INTO task_stage_deadlines VALUES($task,$stage,$due,$reminder);",
                    ("$task", taskId), ("$stage", stageId), ("$due", Date(due)), ("$reminder", reminderId));
                deadline.Transaction = transaction; deadline.ExecuteNonQuery();
            }
        }
        transaction.Commit();
    });
    public Task<(int? Days, int? ReminderMinutes)> StageDeadlineAsync(string stageId) => Task.Run<(int? Days, int? ReminderMinutes)>(() =>
    {
        using var db = Open(); using var cmd = Command(db, "SELECT due_offset_days,reminder_minutes FROM workflow_stages WHERE id=$id;", ("$id", stageId));
        using var row = cmd.ExecuteReader(); if (!row.Read()) throw new KeyNotFoundException("Stage missing.");
        return (row.IsDBNull(0) ? null : row.GetInt32(0), row.IsDBNull(1) ? null : row.GetInt32(1));
    });
    public Task<DateOnly?> CardDeadlineAsync(string taskId) => Task.Run<DateOnly?>(() =>
    {
        using var db = Open(); using var cmd = Command(db, "SELECT due_date FROM task_stage_deadlines WHERE task_id=$id;", ("$id", taskId));
        var value = cmd.ExecuteScalar() as string;
        return value is null ? null : DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    });
    public Task<IReadOnlyList<(string Id, string Name)>> StatusDefinitionsAsync() => Task.Run<IReadOnlyList<(string Id, string Name)>>(() =>
    {
        using var db = Open(); using var cmd = Command(db, "SELECT id,name FROM custom_statuses ORDER BY name;");
        var result = new List<(string, string)>(); using var reader = cmd.ExecuteReader();
        while (reader.Read()) result.Add((reader.GetString(0), reader.GetString(1)));
        return result;
    });
    public Task<IReadOnlyList<string>> StatusesAsync() => Task.Run<IReadOnlyList<string>>(() =>
    {
        using var db = Open(); using var cmd = Command(db, "SELECT name FROM custom_statuses ORDER BY name;");
        var result = new List<string>(); using var reader = cmd.ExecuteReader(); while (reader.Read()) result.Add(reader.GetString(0)); return result;
    });
    public Task AddStatusAsync(string name) => Task.Run(() =>
    {
        using var db = Open(); using var cmd = Command(db, "INSERT INTO custom_statuses VALUES($id,$name);", ("$id", Guid.NewGuid().ToString("N")), ("$name", ValidName(name, 80))); cmd.ExecuteNonQuery();
    });
    public Task SetCustomStatusAsync(string taskId, string? name) => Task.Run(() =>
    {
        using var db = Open(); using var transaction = db.BeginTransaction();
        if (name is not null)
        {
            using var check = Command(db, "SELECT id FROM custom_statuses WHERE name=$name;", ("$name", name));
            check.Transaction = transaction;
            if (check.ExecuteScalar() is null) throw new ArgumentException("Status missing.");
        }
        using var cmd = Command(db, """
            UPDATE tasks SET custom_status_id=(SELECT id FROM custom_statuses WHERE name=$name)
            WHERE id=$task AND deleted_utc IS NULL;
            """, ("$task", taskId), ("$name", name));
        cmd.Transaction = transaction;
        if (cmd.ExecuteNonQuery() != 1) throw new KeyNotFoundException("Task missing.");
        transaction.Commit();
    });
    public Task<IReadOnlyList<WaitingTask>> WaitingAsync() => Task.Run<IReadOnlyList<WaitingTask>>(() =>
    {
        using var db = Open(); using var cmd = Command(db, """
            SELECT t.id,t.title,w.contact,w.expected_date,w.followup_date,w.started_utc,w.active
            FROM task_waiting w JOIN tasks t ON t.id=w.task_id WHERE w.active=1 AND t.deleted_utc IS NULL
            ORDER BY w.followup_date,t.title;
            """);
        var result = new List<WaitingTask>(); using var reader = cmd.ExecuteReader();
        while (reader.Read()) result.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2),
            reader.IsDBNull(3) ? null : DateOnly.ParseExact(reader.GetString(3), "yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateOnly.ParseExact(reader.GetString(4), "yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture), reader.GetInt32(6) == 1));
        return result;
    });
    public Task StartWaitingAsync(string taskId, string contact, DateOnly? expected, DateOnly followup, string zoneId, string note = "") => Task.Run(() =>
    {
        contact = ValidName(contact, 200); if (note.Length > 2000) throw new ArgumentException("Note too long.");
        var local = followup.ToDateTime(new TimeOnly(9, 0));
        var utc = ReminderSchedule.ToUtc(local, zoneId, shiftGap: true);
        using var db = Open(); using var transaction = db.BeginTransaction();
        var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        using (var previous = Command(db, """
            UPDATE reminders SET enabled=0 WHERE id=(SELECT reminder_id FROM task_waiting WHERE task_id=$task);
            """, ("$task", taskId))) { previous.Transaction = transaction; previous.ExecuteNonQuery(); }
        var reminderId = Guid.NewGuid().ToString("N");
        using (var reminder = Command(db, """
            INSERT INTO reminders(id,task_id,due_utc,anchor_local,timezone,recurrence,interval_minutes)
            SELECT $id,id,$due,$local,$zone,'Once',60 FROM tasks
            WHERE id=$task AND status='Open' AND deleted_utc IS NULL;
            """, ("$id", reminderId), ("$task", taskId), ("$due", utc.ToUnixTimeMilliseconds()),
            ("$local", local.ToString("O", CultureInfo.InvariantCulture)), ("$zone", zoneId)))
        { reminder.Transaction = transaction; if (reminder.ExecuteNonQuery() != 1) throw new ArgumentException("Select an open task."); }
        using var cmd = Command(db, """
            INSERT INTO task_waiting(task_id,contact,expected_date,followup_date,started_utc,reminder_id,active)
            SELECT id,$contact,$expected,$followup,$now,$reminder,1 FROM tasks WHERE id=$task AND status='Open' AND deleted_utc IS NULL
            ON CONFLICT(task_id) DO UPDATE SET contact=excluded.contact,expected_date=excluded.expected_date,
                followup_date=excluded.followup_date,started_utc=excluded.started_utc,reminder_id=excluded.reminder_id,active=1;
            """, ("$task", taskId), ("$contact", contact), ("$expected", expected is null ? null : Date(expected.Value)),
            ("$followup", Date(followup)), ("$now", now), ("$reminder", reminderId));
        cmd.Transaction = transaction;
        if (cmd.ExecuteNonQuery() != 1) throw new ArgumentException("Select an open task.");
        using var history = Command(db, "INSERT INTO waiting_history VALUES($id,$task,'Waiting',$note,$now);",
            ("$id", Guid.NewGuid().ToString("N")), ("$task", taskId), ("$note", note), ("$now", now));
        history.Transaction = transaction; history.ExecuteNonQuery(); transaction.Commit();
    });
    public Task ResolveWaitingAsync(string taskId, string note = "") => Task.Run(() =>
    {
        if (note.Length > 2000) throw new ArgumentException("Note too long.");
        using var db = Open(); using var transaction = db.BeginTransaction();
        using var cmd = Command(db, "UPDATE task_waiting SET active=0 WHERE task_id=$task AND active=1;", ("$task", taskId));
        cmd.Transaction = transaction;
        if (cmd.ExecuteNonQuery() != 1) throw new KeyNotFoundException("Task is not waiting.");
        using (var cancel = Command(db, "UPDATE reminders SET enabled=0 WHERE id=(SELECT reminder_id FROM task_waiting WHERE task_id=$task);", ("$task", taskId)))
        { cancel.Transaction = transaction; cancel.ExecuteNonQuery(); }
        using var history = Command(db, "INSERT INTO waiting_history VALUES($id,$task,'Resolved',$note,$now);",
            ("$id", Guid.NewGuid().ToString("N")), ("$task", taskId), ("$note", note), ("$now", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
        history.Transaction = transaction; history.ExecuteNonQuery(); transaction.Commit();
    });
    public Task<IReadOnlyList<string>> WaitingHistoryAsync(string taskId) => Task.Run<IReadOnlyList<string>>(() =>
    {
        using var db = Open(); using var cmd = Command(db, "SELECT action||' · '||note||' · '||at_utc FROM waiting_history WHERE task_id=$task ORDER BY at_utc;", ("$task", taskId));
        var result = new List<string>(); using var reader = cmd.ExecuteReader(); while (reader.Read()) result.Add(reader.GetString(0)); return result;
    });
    public Task<IReadOnlyList<(string Id, string Title, bool Done)>> ChecklistAsync(string taskId, string stageId) => Task.Run<IReadOnlyList<(string Id, string Title, bool Done)>>(() =>
    {
        using var db = Open(); using var cmd = Command(db, "SELECT id,title,is_done FROM checklist_items WHERE task_id=$task AND stage_id=$stage ORDER BY position;",
            ("$task", taskId), ("$stage", stageId));
        var items = new List<(string, string, bool)>(); using var reader = cmd.ExecuteReader();
        while (reader.Read()) items.Add((reader.GetString(0), reader.GetString(1), reader.GetInt32(2) == 1)); return items;
    });
    public Task AddChecklistAsync(string taskId, string stageId, string title) => Task.Run(() =>
    {
        using var db = Open(); using var cmd = Command(db, """
            INSERT INTO checklist_items(id,task_id,title,is_done,position,stage_id)
            SELECT $id,w.task_id,$title,0,COALESCE((SELECT MAX(position)+1 FROM checklist_items WHERE task_id=$task AND stage_id=$stage),0),w.stage_id
            FROM task_workflows w WHERE w.task_id=$task AND w.stage_id=$stage;
            """, ("$id", Guid.NewGuid().ToString("N")), ("$task", taskId), ("$stage", stageId), ("$title", ValidName(title, 300)));
        if (cmd.ExecuteNonQuery() != 1) throw new ArgumentException("Choose a task card in this stage.");
    });
    public Task SetChecklistAsync(string id, bool done) => Task.Run(() =>
    {
        using var db = Open(); using var cmd = Command(db, "UPDATE checklist_items SET is_done=$done WHERE id=$id;", ("$id", id), ("$done", done ? 1 : 0));
        if (cmd.ExecuteNonQuery() != 1) throw new KeyNotFoundException("Checklist item missing.");
    });
    public Task AddStageDeadlineAsync(string stageId, int daysFromEntry, int? reminderMinutesBefore) => Task.Run(() =>
    {
        if (daysFromEntry is < 0 or > 3660 || reminderMinutesBefore is < 0 or > 525600) throw new ArgumentException("Invalid stage deadline or reminder offset.");
        using var db = Open(); using var cmd = Command(db, "UPDATE workflow_stages SET due_offset_days=$days,reminder_minutes=$minutes WHERE id=$id;",
            ("$id", stageId), ("$days", daysFromEntry), ("$minutes", reminderMinutesBefore));
        if (cmd.ExecuteNonQuery() != 1) throw new KeyNotFoundException("Stage missing.");
    });

    public Task AddDependencyAsync(string taskId, string dependsOnId) => Task.Run(() =>
    {
        if (taskId == dependsOnId) throw new ArgumentException("A task cannot depend on itself.");
        using var db = Open(); using var transaction = db.BeginTransaction();
        using var cycle = Command(db, """
            WITH RECURSIVE chain(id) AS (
                SELECT depends_on_id FROM task_dependencies WHERE task_id=$parent
                UNION SELECT d.depends_on_id FROM task_dependencies d JOIN chain c ON d.task_id=c.id
            ) SELECT 1 FROM chain WHERE id=$child LIMIT 1;
            """, ("$parent", dependsOnId), ("$child", taskId));
        cycle.Transaction = transaction;
        if (cycle.ExecuteScalar() is not null) throw new ArgumentException("Dependency would create a cycle.");
        using var cmd = Command(db, "INSERT INTO task_dependencies VALUES($task,$depends);", ("$task", taskId), ("$depends", dependsOnId));
        cmd.Transaction = transaction; cmd.ExecuteNonQuery(); transaction.Commit();
    });
    public Task<IReadOnlyList<string>> DependenciesAsync(string taskId) => Task.Run<IReadOnlyList<string>>(() =>
    {
        using var db = Open(); using var cmd = Command(db, "SELECT depends_on_id FROM task_dependencies WHERE task_id=$id;", ("$id", taskId));
        var result = new List<string>(); using var reader = cmd.ExecuteReader(); while (reader.Read()) result.Add(reader.GetString(0)); return result;
    });
    private static DateOnly NextMonthly(DateOnly date, int anchorDay)
    {
        var first = new DateOnly(date.Year, date.Month, 1).AddMonths(1);
        return new DateOnly(first.Year, first.Month, Math.Min(anchorDay, DateTime.DaysInMonth(first.Year, first.Month)));
    }
    public Task AddRecurringAsync(string taskId, string repeat, DateOnly firstDate) => Task.Run(() =>
    {
        if (repeat is not ("Daily" or "Weekly" or "Monthly")) throw new ArgumentException("Choose daily, weekly or monthly.");
        using var db = Open(); using var cmd = Command(db, "INSERT INTO recurring_rules VALUES($id,$task,$repeat,$next,$day,1);",
            ("$id", Guid.NewGuid().ToString("N")), ("$task", taskId), ("$repeat", repeat), ("$next", Date(firstDate)), ("$day", firstDate.Day)); cmd.ExecuteNonQuery();
    });
    public Task<int> ReconcileRecurringAsync(DateOnly today) => Task.Run(() =>
    {
        using var db = Open(); using var transaction = db.BeginTransaction();
        using var get = Command(db, "SELECT r.id,r.source_task_id,r.recurrence,r.next_date,r.anchor_day FROM recurring_rules r JOIN tasks t ON t.id=r.source_task_id WHERE r.enabled=1 AND t.deleted_utc IS NULL;");
        get.Transaction = transaction;
        var rules = new List<(string Id, string Task, string Repeat, DateOnly Next, int AnchorDay)>();
        using (var reader = get.ExecuteReader()) while (reader.Read()) rules.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), DateOnly.ParseExact(reader.GetString(3), "yyyy-MM-dd", CultureInfo.InvariantCulture), reader.GetInt32(4)));
        var count = 0;
        foreach (var rule in rules)
        {
            var next = rule.Next;
            while (next <= today)
            {
                if (++count > 3660) throw new InvalidOperationException("Too many missed recurring tasks; source rule preserved.");
                var id = Guid.NewGuid().ToString("N");
                using var create = Command(db, """
                    INSERT INTO tasks(id,title,notes,due_date,priority,status,project_id,category_id,created_utc,updated_utc)
                    SELECT $new,title,notes,$date,priority,'Open',project_id,category_id,$now,$now FROM tasks WHERE id=$source;
                    INSERT INTO recurring_instances VALUES($rule,$date,$new);
                    """, ("$new", id), ("$date", Date(next)), ("$now", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)),
                    ("$source", rule.Task), ("$rule", rule.Id));
                create.Transaction = transaction; create.ExecuteNonQuery();
                next = rule.Repeat switch { "Daily" => next.AddDays(1), "Weekly" => next.AddDays(7), _ => NextMonthly(next, rule.AnchorDay) };
            }
            using var update = Command(db, "UPDATE recurring_rules SET next_date=$date WHERE id=$id;", ("$date", Date(next)), ("$id", rule.Id));
            update.Transaction = transaction; update.ExecuteNonQuery();
        }
        transaction.Commit(); return count;
    });
}
