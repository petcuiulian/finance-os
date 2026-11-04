using FinanceOS.Core;
using FinanceOS.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FinanceOS.Tests;

public sealed class StorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "finance-os-tests", Guid.NewGuid().ToString("N"));
    private string Database => AppDataPaths.DatabasePath(_root);

    [Fact]
    public async Task MigrationAndSessionSurviveReopening()
    {
        var first = await new SqliteAppStore(Database).OpenSessionAsync();
        Assert.Equal(new StorageStatus(5, 1), first);
        var second = new SqliteAppStore(Database);
        Assert.Equal(first, await second.ReadStatusAsync());
        Assert.Equal(new StorageStatus(5, 2), await second.OpenSessionAsync());
        Assert.Equal(new StorageStatus(5, 2), await new SqliteAppStore(Database).ReadStatusAsync());
    }

    [Fact]
    public async Task ExistingM0DatabaseMigratesWithoutLosingLaunches()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Database)!);
        Execute("CREATE TABLE app_state (id INTEGER PRIMARY KEY, launch_count INTEGER NOT NULL); INSERT INTO app_state VALUES (1, 7); PRAGMA user_version = 1;");
        var store = new SqliteAppStore(Database);
        Assert.Equal(new StorageStatus(5, 8), await store.OpenSessionAsync());
        Assert.Equal(new WorkspacePreferences("Light", "en", "Simple", false), await store.ReadPreferencesAsync());
    }

    [Fact]
    public async Task PreferencesPersistAcrossReopeningAndRejectInvalidValues()
    {
        var store = new SqliteAppStore(Database);
        await store.OpenSessionAsync();
        var settings = new WorkspacePreferences("Dark", "ro", "Advanced", true);
        await store.SavePreferencesAsync(settings);
        Assert.Equal(settings, await new SqliteAppStore(Database).ReadPreferencesAsync());
        await Assert.ThrowsAsync<ArgumentException>(() => store.SavePreferencesAsync(settings with { Theme = "Unknown" }));
        Assert.Equal(settings, await store.ReadPreferencesAsync());
        Assert.Equal(1, (await store.ReadStatusAsync()).LaunchCount);
    }

    [Fact]
    public async Task NewerSchemaIsRejectedWithoutChangingData()
    {
        var store = new SqliteAppStore(Database);
        await store.OpenSessionAsync();
        Execute("PRAGMA user_version = 99;");
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.OpenSessionAsync());
        Assert.Equal(new StorageStatus(99, 1), await store.ReadStatusAsync());
    }

    [Fact]
    public async Task FailedMigrationRollsBackAndPreservesExistingTable()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Database)!);
        Execute("CREATE TABLE app_state (sentinel TEXT); INSERT INTO app_state VALUES ('preserve');");
        await Assert.ThrowsAsync<SqliteException>(() => new SqliteAppStore(Database).OpenSessionAsync());
        Assert.Equal(0L, Scalar("PRAGMA user_version;"));
        Assert.Equal("preserve", Scalar("SELECT sentinel FROM app_state;"));
    }

    [Fact]
    public async Task CorruptFileIsNotReplaced()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Database)!);
        var bytes = Enumerable.Repeat((byte)0xAB, 512).ToArray();
        await File.WriteAllBytesAsync(Database, bytes);
        await Assert.ThrowsAsync<SqliteException>(() => new SqliteAppStore(Database).OpenSessionAsync());
        Assert.Equal(bytes, await File.ReadAllBytesAsync(Database));
    }

    [Fact]
    public async Task ConcurrentSessionsSerializeWithoutLostUpdates()
    {
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => new SqliteAppStore(Database).OpenSessionAsync()));
        Assert.Equal(8, (await new SqliteAppStore(Database).ReadStatusAsync()).LaunchCount);
    }

    [Fact]
    public async Task CancellationDoesNotCreateDatabase()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new SqliteAppStore(Database).OpenSessionAsync(new CancellationToken(true)));
        Assert.False(File.Exists(Database));
    }

    [Fact]
    public void DataPathIsUnderUserDirectory()
    {
        Assert.Equal(Path.Combine(_root, "FinanceOS", "finance-os.db"), Database);
        Assert.Throws<ArgumentException>(() => AppDataPaths.DatabasePath("relative"));
        Assert.Throws<ArgumentException>(() => new SqliteAppStore("relative.db"));
    }

    private object? Scalar(string sql)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Database, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private void Execute(string sql) => Scalar(sql);
    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
