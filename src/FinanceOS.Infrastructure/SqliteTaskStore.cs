using System.Globalization;
using FinanceOS.Core;
using Microsoft.Data.Sqlite;

namespace FinanceOS.Infrastructure;

public sealed class SqliteTaskStore(string path) : ITaskStore
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = path,
        Mode = SqliteOpenMode.ReadWrite,
        ForeignKeys = true,
        Pooling = false,
        DefaultTimeout = 10
    }.ToString();

    public Task<WorkTask> CreateAsync(string title, string notes = "", DateOnly? dueDate = null,
        string priority = "Normal", string? projectId = null, string? categoryId = null,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        Validate(title, notes, priority);
        cancellationToken.ThrowIfCancellationRequested();
        var now = DateTimeOffset.UtcNow;
        var item = new WorkTask(Guid.NewGuid().ToString("N"), title.Trim(), notes.Trim(), dueDate,
            priority, "Open", projectId, categoryId, now, now);
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = """
            INSERT INTO tasks (id,title,notes,due_date,priority,status,project_id,category_id,created_utc,updated_utc)
            VALUES ($id,$title,$notes,$due,$priority,$status,$project,$category,$created,$updated);
            """;
        Bind(command, item);
        command.ExecuteNonQuery();
        return item;
    }, cancellationToken);

    public Task UpdateAsync(WorkTask item, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        ArgumentNullException.ThrowIfNull(item);
        Validate(item.Title, item.Notes, item.Priority);
        cancellationToken.ThrowIfCancellationRequested();
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = """
            UPDATE tasks SET title=$title,notes=$notes,due_date=$due,priority=$priority,
            project_id=$project,category_id=$category,updated_utc=$updated WHERE id=$id;
            """;
        Bind(command, item with { UpdatedUtc = DateTimeOffset.UtcNow });
        if (command.ExecuteNonQuery() != 1) throw new KeyNotFoundException("Task was not found.");
    }, cancellationToken);

    public Task SetStatusAsync(string id, string status, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        if (status is not ("Open" or "Done" or "Archived")) throw new ArgumentException("Invalid task status.", nameof(status));
        cancellationToken.ThrowIfCancellationRequested();
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = "UPDATE tasks SET status=$status,updated_utc=$now WHERE id=$id;";
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$id", id);
        if (command.ExecuteNonQuery() != 1) throw new KeyNotFoundException("Task was not found.");
    }, cancellationToken);

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = "DELETE FROM tasks WHERE id=$id;";
        command.Parameters.AddWithValue("$id", id);
        if (command.ExecuteNonQuery() != 1) throw new KeyNotFoundException("Task was not found.");
    }, cancellationToken);

    public Task<IReadOnlyList<WorkTask>> ListAsync(TaskQuery query, CancellationToken cancellationToken = default) => Task.Run<IReadOnlyList<WorkTask>>(() =>
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.View is not ("Tasks" or "MyDay" or "Archive" or "Overview")) throw new ArgumentException("Invalid view.");
        if (query.Filter is not ("All" or "Open" or "Done")) throw new ArgumentException("Invalid filter.");
        cancellationToken.ThrowIfCancellationRequested();
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = """
            SELECT id,title,notes,due_date,priority,status,project_id,category_id,created_utc,updated_utc
            FROM tasks WHERE
              (($view='Archive' AND status='Archived') OR
               ($view='MyDay' AND status='Open' AND due_date IS NOT NULL AND due_date <= $today) OR
               ($view IN ('Tasks','Overview') AND status!='Archived'))
              AND ($filter='All' OR status=$filter)
              AND ($search='' OR instr(lower(title),lower($search))>0 OR instr(lower(notes),lower($search))>0)
            ORDER BY CASE WHEN due_date IS NULL THEN 1 ELSE 0 END, due_date,
                     CASE priority WHEN 'Urgent' THEN 0 WHEN 'High' THEN 1 WHEN 'Normal' THEN 2 ELSE 3 END,
                     created_utc DESC;
            """;
        command.Parameters.AddWithValue("$view", query.View);
        command.Parameters.AddWithValue("$today", (query.Today ?? DateOnly.FromDateTime(DateTime.Today)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$filter", query.Filter);
        command.Parameters.AddWithValue("$search", query.Search.Trim());
        var result = new List<WorkTask>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(new WorkTask(reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : DateOnly.ParseExact(reader.GetString(3), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                reader.GetString(4), reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7), DateTimeOffset.Parse(reader.GetString(8), CultureInfo.InvariantCulture),
                DateTimeOffset.Parse(reader.GetString(9), CultureInfo.InvariantCulture)));
        }
        return result;
    }, cancellationToken);

    private SqliteConnection Open()
    {
        var db = new SqliteConnection(_connectionString);
        try { db.Open(); return db; }
        catch { db.Dispose(); throw; }
    }

    private static void Validate(string title, string notes, string priority)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 300)
            throw new ArgumentException("Task title must contain 1–300 characters.", nameof(title));
        if (notes.Length > 10000) throw new ArgumentException("Notes are too long.", nameof(notes));
        if (priority is not ("Low" or "Normal" or "High" or "Urgent"))
            throw new ArgumentException("Invalid priority.", nameof(priority));
    }

    private static void Bind(SqliteCommand command, WorkTask item)
    {
        command.Parameters.AddWithValue("$id", item.Id);
        command.Parameters.AddWithValue("$title", item.Title);
        command.Parameters.AddWithValue("$notes", item.Notes);
        command.Parameters.AddWithValue("$due", (object?)item.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? DBNull.Value);
        command.Parameters.AddWithValue("$priority", item.Priority);
        command.Parameters.AddWithValue("$status", item.Status);
        command.Parameters.AddWithValue("$project", (object?)item.ProjectId ?? DBNull.Value);
        command.Parameters.AddWithValue("$category", (object?)item.CategoryId ?? DBNull.Value);
        command.Parameters.AddWithValue("$created", item.CreatedUtc.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$updated", item.UpdatedUtc.ToString("O", CultureInfo.InvariantCulture));
    }
}
