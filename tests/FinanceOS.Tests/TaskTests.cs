using FinanceOS.Core;
using FinanceOS.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FinanceOS.Tests;

public sealed class TaskTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "finance-os-task-tests", Guid.NewGuid().ToString("N"));
    private string Database => AppDataPaths.DatabasePath(_root);
    private SqliteTaskStore Tasks => new(Database);
    private async Task OpenAsync() => await new SqliteAppStore(Database).OpenSessionAsync();

    [Fact]
    public async Task TaskLifecycleSurvivesRestartAndPreservesAdvancedFields()
    {
        await OpenAsync();
        var due = new DateOnly(2026, 11, 4);
        var created = await Tasks.CreateAsync("Review invoice", "AP details", due, "High");
        Assert.Single(await Tasks.ListAsync(new TaskQuery("MyDay", Today: due)));
        await Tasks.UpdateAsync(created with { Title = "Review invoice 42" });
        var second = new SqliteTaskStore(Database);
        var fromDisk = Assert.Single(await second.ListAsync(new TaskQuery("Tasks")));
        Assert.Equal("AP details", fromDisk.Notes);
        Assert.Equal("High", fromDisk.Priority);
        Assert.Equal(due, fromDisk.DueDate);
        await second.SetStatusAsync(created.Id, "Done");
        Assert.Empty(await Tasks.ListAsync(new TaskQuery("MyDay", Today: due)));
        await second.SetStatusAsync(created.Id, "Archived");
        Assert.Empty(await Tasks.ListAsync(new TaskQuery("Tasks")));
        Assert.Single(await Tasks.ListAsync(new TaskQuery("Archive")));
        await second.SetStatusAsync(created.Id, "Open");
        Assert.Single(await Tasks.ListAsync(new TaskQuery("Tasks")));
        await Tasks.DeleteAsync(created.Id);
        Assert.Empty(await second.ListAsync(new TaskQuery("Tasks")));
        Assert.Single(await second.ListAsync(new TaskQuery("Trash")));
        await Tasks.RestoreAsync(created.Id);
        Assert.Single(await second.ListAsync(new TaskQuery("Tasks")));
    }

    [Fact]
    public async Task TrashRestoreKeepsCompletedState()
    {
        await OpenAsync();
        var task = await Tasks.CreateAsync("Approved invoice");
        await Tasks.SetStatusAsync(task.Id, "Done");
        await Tasks.DeleteAsync(task.Id);
        Assert.Equal("Deleted", Assert.Single(await Tasks.ListAsync(new TaskQuery("Trash"))).Status);
        await new SqliteTaskStore(Database).RestoreAsync(task.Id);
        Assert.Equal("Done", Assert.Single(await Tasks.ListAsync(new TaskQuery("Tasks"))).Status);
    }

    [Fact]
    public async Task SearchFilterAndDueBoundaryWork()
    {
        await OpenAsync();
        var today = new DateOnly(2026, 11, 4);
        await Tasks.CreateAsync("Month end close", "Bank reconciliation", today, "Urgent");
        await Tasks.CreateAsync("Tomorrow call", dueDate: today.AddDays(1));
        var done = await Tasks.CreateAsync("Closed invoice", dueDate: today);
        await Tasks.SetStatusAsync(done.Id, "Done");
        Assert.Single(await Tasks.ListAsync(new TaskQuery("Tasks", "BANK")));
        Assert.Single(await Tasks.ListAsync(new TaskQuery("Tasks", Filter: "Done")));
        Assert.Single(await Tasks.ListAsync(new TaskQuery("MyDay", Today: today)));
        Assert.Equal(2, (await Tasks.ListAsync(new TaskQuery("Tasks", Filter: "Open"))).Count);
    }

    [Fact]
    public async Task M1MigrationKeepsPreferencesAndAddsTaskTables()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Database)!);
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Database, Pooling = false }.ToString()))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE app_state (id INTEGER PRIMARY KEY, launch_count INTEGER NOT NULL);
                INSERT INTO app_state VALUES (1,4);
                CREATE TABLE workspace_preferences (id INTEGER PRIMARY KEY, theme TEXT, language TEXT, mode TEXT, onboarding_complete INTEGER);
                INSERT INTO workspace_preferences VALUES (1,'Dark','ro','Advanced',1);
                PRAGMA user_version=2;
                """;
            cmd.ExecuteNonQuery();
        }
        var app = new SqliteAppStore(Database);
        Assert.Equal(new StorageStatus(4, 5), await app.OpenSessionAsync());
        Assert.Equal(new WorkspacePreferences("Dark", "ro", "Advanced", true), await app.ReadPreferencesAsync());
        Assert.Empty(await Tasks.ListAsync(new TaskQuery("Tasks")));
    }

    [Fact]
    public async Task InvalidInputAndForeignKeyFailureDoNotCreateTask()
    {
        await OpenAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => Tasks.CreateAsync(" "));
        await Assert.ThrowsAsync<ArgumentException>(() => Tasks.CreateAsync("Valid", priority: "Broken"));
        await Assert.ThrowsAsync<SqliteException>(() => Tasks.CreateAsync("Valid", projectId: "missing"));
        Assert.Empty(await Tasks.ListAsync(new TaskQuery("Tasks")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
