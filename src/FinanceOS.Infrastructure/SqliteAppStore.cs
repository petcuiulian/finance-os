using FinanceOS.Core;
using Microsoft.Data.Sqlite;

namespace FinanceOS.Infrastructure;

public sealed class SqliteAppStore : IAppStore
{
    public const int CurrentSchemaVersion = 1;
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
