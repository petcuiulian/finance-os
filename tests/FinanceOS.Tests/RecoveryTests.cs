using System.Diagnostics;
using FinanceOS.Core;
using FinanceOS.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;
using Xunit.Abstractions;

namespace FinanceOS.Tests;

public sealed class RecoveryTests(ITestOutputHelper output) : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "finance-recovery", Guid.NewGuid().ToString("N"));
    private string Database => Path.Combine(_root, "workspace.db");
    [Fact]
    public async Task FullBackupRestoresTasksRulesWaitingAndPreferencesWithSafetyCopy()
    {
        await new SqliteAppStore(Database).OpenSessionAsync();
        var task = await new SqliteTaskStore(Database).CreateAsync("Synthetic recovery task");
        await new SqliteWorkflowStore(Database).StartWaitingAsync(task.Id, "Synthetic contact", null, new DateOnly(2026, 11, 5), "UTC");
        await new SqliteProductivityStore(Database).AddRuleAsync("Synthetic rule", "Overdue", "", "Urgent");
        var backup = Path.Combine(_root, "backup.db");
        await DatabaseRecovery.BackupAsync(Database, backup);
        await new SqliteTaskStore(Database).CreateAsync("After backup");
        var safety = await DatabaseRecovery.RestoreOfflineAsync(backup, Database);
        Assert.NotNull(safety);
        Assert.Single(await new SqliteTaskStore(Database).ListAsync(new TaskQuery("Tasks")));
        Assert.Equal(2, (await new SqliteTaskStore(safety!).ListAsync(new TaskQuery("Tasks"))).Count);
        Assert.Single(await new SqliteWorkflowStore(Database).WaitingAsync());
        Assert.Single(await new SqliteProductivityStore(Database).RulesAsync());
        DatabaseRecovery.CheckIntegrity(Database);
    }
    [Fact]
    public async Task InvalidBackupAndFutureSchemaNeverReplaceWorkspace()
    {
        await new SqliteAppStore(Database).OpenSessionAsync();
        var before = await File.ReadAllBytesAsync(Database);
        var invalid = Path.Combine(_root, "invalid.db"); await File.WriteAllTextAsync(invalid, "not a database");
        await Assert.ThrowsAsync<SqliteException>(() => DatabaseRecovery.RestoreOfflineAsync(invalid, Database));
        Assert.Equal(before, await File.ReadAllBytesAsync(Database));
        var future = Path.Combine(_root, "future.db"); await DatabaseRecovery.BackupAsync(Database, future);
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = future, Pooling = false }.ToString()))
        { db.Open(); using var c = db.CreateCommand(); c.CommandText = "PRAGMA user_version=999;"; c.ExecuteNonQuery(); }
        await Assert.ThrowsAsync<InvalidDataException>(() => DatabaseRecovery.RestoreOfflineAsync(future, Database));
        Assert.Equal(before, await File.ReadAllBytesAsync(Database));
        await Assert.ThrowsAsync<ArgumentException>(() => DatabaseRecovery.BackupAsync(Database, Database));
    }
    [Fact]
    public async Task MigrationRetainsOriginalSnapshot()
    {
        Directory.CreateDirectory(_root);
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Database, Pooling = false }.ToString()))
        { db.Open(); using var c = db.CreateCommand(); c.CommandText = "CREATE TABLE app_state(id INTEGER PRIMARY KEY,launch_count INTEGER NOT NULL); INSERT INTO app_state VALUES(1,8); PRAGMA user_version=1;"; c.ExecuteNonQuery(); }
        await new SqliteAppStore(Database).OpenSessionAsync();
        var snapshot = Assert.Single(Directory.GetFiles(_root, "*.before-v*.db"));
        var old = await new SqliteAppStore(snapshot).ReadStatusAsync();
        Assert.Equal(new StorageStatus(1, 8), old);
    }
    [Fact]
    public async Task TenThousandTasksSearchBackupAndRestoreKeepIntegrity()
    {
        await new SqliteAppStore(Database).OpenSessionAsync(); var now = DateTimeOffset.UtcNow;
        var tasks = Enumerable.Range(0, 10000).Select(i => new WorkTask($"synthetic-{i}", $"Synthetic invoice {i:D5}", "", new DateOnly(2026, 11, 4), "Normal", "Open", null, null, now, now)).ToList();
        var clock = Stopwatch.StartNew();
        Assert.Equal(10000, await new SqliteProductivityStore(Database).ImportAsync(tasks));
        output.WriteLine($"10k import: {clock.ElapsedMilliseconds} ms"); clock.Restart();
        Assert.Single(await new SqliteTaskStore(Database).ListAsync(new TaskQuery("Tasks", "invoice 09999")));
        output.WriteLine($"10k search: {clock.ElapsedMilliseconds} ms"); clock.Restart();
        var backup = Path.Combine(_root, "volume.db"); await DatabaseRecovery.BackupAsync(Database, backup);
        await DatabaseRecovery.RestoreOfflineAsync(backup, Database);
        output.WriteLine($"10k backup and restore: {clock.ElapsedMilliseconds} ms");
        Assert.Equal(10000, (await new SqliteTaskStore(Database).ListAsync(new TaskQuery("Tasks"))).Count);
        DatabaseRecovery.CheckIntegrity(Database);
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
