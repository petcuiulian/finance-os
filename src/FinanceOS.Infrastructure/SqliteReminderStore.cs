using System.Globalization;
using FinanceOS.Core;
using Microsoft.Data.Sqlite;

namespace FinanceOS.Infrastructure;

public sealed class SqliteReminderStore(string path)
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

    public Task<NotificationOptions> ReadOptionsAsync() => Task.Run(() =>
    {
        using var db = Open(); using var cmd = Command(db, "SELECT toasts,sound,working_hours,weekdays FROM notification_options WHERE id=1;");
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) throw new InvalidDataException("Notification preferences missing.");
        return new NotificationOptions(reader.GetInt32(0) == 1, reader.GetInt32(1) == 1, reader.GetInt32(2) == 1, reader.GetInt32(3) == 1);
    });

    public Task SaveOptionsAsync(NotificationOptions options) => Task.Run(() =>
    {
        using var db = Open(); using var cmd = Command(db,
            "UPDATE notification_options SET toasts=$toasts,sound=$sound,working_hours=$hours,weekdays=$days WHERE id=1;",
            ("$toasts", options.Toasts ? 1 : 0), ("$sound", options.Sound ? 1 : 0), ("$hours", options.WorkingHoursOnly ? 1 : 0), ("$days", options.WeekdaysOnly ? 1 : 0));
        if (cmd.ExecuteNonQuery() != 1) throw new InvalidDataException("Notification preferences missing.");
    });

    public Task AddAsync(ReminderDraft draft) => Task.Run(() =>
    {
        var due = ReminderSchedule.ToUtc(draft.LocalTime, draft.TimeZoneId);
        if (draft.IntervalMinutes is < 1 or > 525600) throw new ArgumentException("Interval must be 1–525600 minutes.");
        _ = ReminderSchedule.NextAfter(draft, due);
        using var db = Open();
        using var cmd = Command(db, """
            INSERT INTO reminders(id,task_id,due_utc,anchor_local,timezone,recurrence,interval_minutes)
            SELECT $id,id,$due,$anchor,$zone,$repeat,$interval FROM tasks WHERE id=$task AND status='Open' AND deleted_utc IS NULL;
            """, ("$id", Guid.NewGuid().ToString("N")), ("$task", draft.TaskId), ("$due", due.ToUnixTimeMilliseconds()),
            ("$anchor", draft.LocalTime.ToString("O", CultureInfo.InvariantCulture)), ("$zone", draft.TimeZoneId),
            ("$repeat", draft.Recurrence), ("$interval", draft.IntervalMinutes));
        if (cmd.ExecuteNonQuery() != 1) throw new ArgumentException("Choose an open task first.");
    });

    // One durable inbox entry coalesces a missed recurring period. Next occurrence is strictly future.
    // Persisting before external effects guarantees that a crash cannot silently lose the inbox alert.
    public Task ReconcileAsync(DateTimeOffset now) => Task.Run(() =>
    {
        using var db = Open(); using var transaction = db.BeginTransaction();
        using var query = Command(db, """
            SELECT r.id,r.task_id,r.due_utc,r.anchor_local,r.timezone,r.recurrence,r.interval_minutes
            FROM reminders r JOIN tasks t ON t.id=r.task_id
            WHERE r.enabled=1 AND r.due_utc<=$now AND t.status='Open' AND t.deleted_utc IS NULL;
            """, ("$now", now.ToUnixTimeMilliseconds()));
        query.Transaction = transaction;
        var due = new List<(string Id, long Due, ReminderDraft Draft)>();
        using (var reader = query.ExecuteReader())
            while (reader.Read()) due.Add((reader.GetString(0), reader.GetInt64(2), new ReminderDraft(reader.GetString(1),
                DateTime.Parse(reader.GetString(3), CultureInfo.InvariantCulture), reader.GetString(4), reader.GetString(5), reader.GetInt32(6))));
        foreach (var item in due)
        {
            using var insert = Command(db, """
                INSERT OR IGNORE INTO reminder_alerts(id,reminder_id,scheduled_utc,available_utc)
                VALUES($id,$reminder,$due,$now);
                """, ("$id", Guid.NewGuid().ToString("N")), ("$reminder", item.Id), ("$due", item.Due), ("$now", now.ToUnixTimeMilliseconds()));
            insert.Transaction = transaction; insert.ExecuteNonQuery();
            var next = ReminderSchedule.NextAfter(item.Draft, now);
            using var update = Command(db, "UPDATE reminders SET due_utc=$next,enabled=$enabled WHERE id=$id;",
                ("$id", item.Id), ("$next", next?.ToUnixTimeMilliseconds() ?? item.Due), ("$enabled", next.HasValue ? 1 : 0));
            update.Transaction = transaction; update.ExecuteNonQuery();
        }
        transaction.Commit();
    });

    public Task<IReadOnlyList<ReminderScheduleEntry>> SchedulesAsync() => Task.Run<IReadOnlyList<ReminderScheduleEntry>>(() =>
    {
        using var db = Open(); using var cmd = Command(db, """
            SELECT r.id,t.title,r.due_utc,r.recurrence,r.timezone FROM reminders r JOIN tasks t ON t.id=r.task_id
            WHERE r.enabled=1 AND t.status='Open' AND t.deleted_utc IS NULL ORDER BY r.due_utc;
            """);
        var result = new List<ReminderScheduleEntry>(); using var reader = cmd.ExecuteReader();
        while (reader.Read()) result.Add(new ReminderScheduleEntry(reader.GetString(0), reader.GetString(1),
            DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(2)), reader.GetString(3), reader.GetString(4)));
        return result;
    });

    public Task CancelScheduleAsync(string id) => Task.Run(() =>
    {
        using var db = Open(); using var transaction = db.BeginTransaction();
        using var cmd = Command(db, "UPDATE reminders SET enabled=0 WHERE id=$id; UPDATE reminder_alerts SET dismissed=1 WHERE reminder_id=$id;", ("$id", id));
        cmd.Transaction = transaction; cmd.ExecuteNonQuery(); transaction.Commit();
    });

    public Task<IReadOnlyList<ReminderAlert>> AlertsAsync(DateTimeOffset now) => Task.Run<IReadOnlyList<ReminderAlert>>(() =>
    {
        using var db = Open(); using var cmd = Command(db, """
            SELECT a.id,t.id,t.title,a.scheduled_utc,a.available_utc FROM reminder_alerts a
            JOIN reminders r ON r.id=a.reminder_id JOIN tasks t ON t.id=r.task_id
            WHERE a.dismissed=0 AND a.available_utc<=$now AND t.status='Open' AND t.deleted_utc IS NULL
            ORDER BY a.scheduled_utc;
            """, ("$now", now.ToUnixTimeMilliseconds()));
        var result = new List<ReminderAlert>(); using var reader = cmd.ExecuteReader();
        while (reader.Read()) result.Add(new ReminderAlert(reader.GetString(0), reader.GetString(1), reader.GetString(2),
            DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(3)), DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(4))));
        return result;
    });

    // Mark before OS submission: at most one toast attempt per occurrence/snooze, even across processes.
    public Task<bool> ClaimToastAsync(string id, DateTimeOffset now) => Task.Run(() =>
    {
        using var db = Open(); using var cmd = Command(db,
            """
            UPDATE reminder_alerts SET toast_attempted=1 WHERE id=$id AND toast_attempted=0 AND dismissed=0
            AND available_utc<=$now AND EXISTS (SELECT 1 FROM reminders r JOIN tasks t ON t.id=r.task_id
            WHERE r.id=reminder_alerts.reminder_id AND t.status='Open' AND t.deleted_utc IS NULL);
            """, ("$id", id), ("$now", now.ToUnixTimeMilliseconds()));
        return cmd.ExecuteNonQuery() == 1;
    });

    public Task DismissAsync(string id) => Task.Run(() =>
    {
        using var db = Open(); using var cmd = Command(db, "UPDATE reminder_alerts SET dismissed=1 WHERE id=$id;", ("$id", id));
        if (cmd.ExecuteNonQuery() != 1) throw new KeyNotFoundException("Alert no longer exists.");
    });

    public Task SnoozeAsync(string id, DateTimeOffset until, DateTimeOffset now) => Task.Run(() =>
    {
        if (until <= now) throw new ArgumentException("Snooze time must be in the future.");
        using var db = Open(); using var cmd = Command(db, """
            UPDATE reminder_alerts SET available_utc=$until,toast_attempted=0 WHERE id=$id AND dismissed=0;
            """, ("$id", id), ("$until", until.ToUnixTimeMilliseconds()));
        if (cmd.ExecuteNonQuery() != 1) throw new KeyNotFoundException("Alert no longer exists.");
    });

    public Task AddBlockAsync(string title, DateTime start, DateTime end, string zone) => Task.Run(() =>
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 300) throw new ArgumentException("Enter a title of 1–300 characters.");
        var from = ReminderSchedule.ToUtc(start, zone); var to = ReminderSchedule.ToUtc(end, zone);
        if (to <= from) throw new ArgumentException("End must be after start.");
        using var db = Open(); using var cmd = Command(db,
            "INSERT INTO time_blocks VALUES($id,$title,$start,$end,$zone);", ("$id", Guid.NewGuid().ToString("N")),
            ("$title", title.Trim()), ("$start", from.ToUnixTimeMilliseconds()), ("$end", to.ToUnixTimeMilliseconds()), ("$zone", zone));
        cmd.ExecuteNonQuery();
    });

    public Task DeleteBlockAsync(string id) => Task.Run(() =>
    {
        using var db = Open(); using var cmd = Command(db, "DELETE FROM time_blocks WHERE id=$id;", ("$id", id)); cmd.ExecuteNonQuery();
    });

    public Task<IReadOnlyList<CalendarEntry>> CalendarAsync(DateTime from, DateTime to, string zoneId) => Task.Run<IReadOnlyList<CalendarEntry>>(() =>
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        var start = ReminderSchedule.ToUtc(from, zoneId, true).ToUnixTimeMilliseconds();
        var end = ReminderSchedule.ToUtc(to, zoneId, true).ToUnixTimeMilliseconds();
        using var db = Open();
        using var blocks = Command(db, "SELECT id,title,start_utc,end_utc FROM time_blocks WHERE start_utc<$end AND end_utc>$start;",
            ("$start", start), ("$end", end));
        var result = new List<CalendarEntry>();
        using (var reader = blocks.ExecuteReader())
            while (reader.Read()) result.Add(new CalendarEntry(reader.GetString(0), reader.GetString(1), "Time block",
                TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(2)), zone).DateTime,
                TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(3)), zone).DateTime));
        using var tasks = Command(db, """
            SELECT id,title,due_date FROM tasks WHERE deleted_utc IS NULL AND status='Open' AND due_date >= $from AND due_date < $to;
            """, ("$from", from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), ("$to", to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        using (var reader = tasks.ExecuteReader())
            while (reader.Read()) result.Add(new CalendarEntry(reader.GetString(0), reader.GetString(1), "Deadline",
                DateTime.ParseExact(reader.GetString(2), "yyyy-MM-dd", CultureInfo.InvariantCulture), null));
        return result.OrderBy(x => x.Start).ToList();
    });
}
