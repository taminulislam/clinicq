using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace ClinicQ.Web.Data;

/// <summary>
/// SQLite connection factory used when no SQL Server connection string is configured.
/// For shared in-memory databases (tests) a sentinel connection is held open so the schema
/// survives between the short-lived connections Dapper repositories open per call.
/// </summary>
public sealed class SqliteConnectionFactory : IDbConnectionFactory, IDisposable
{
    private readonly string _connectionString;
    private readonly SqliteConnection? _sentinel;

    public SqliteConnectionFactory(string connectionString)
    {
        _connectionString = string.IsNullOrWhiteSpace(connectionString) ? "Data Source=app.db" : connectionString;

        var builder = new SqliteConnectionStringBuilder(_connectionString);
        if (builder.Mode == SqliteOpenMode.Memory)
        {
            _sentinel = new SqliteConnection(_connectionString);
            _sentinel.Open();
        }
    }

    public DatabaseProvider Provider => DatabaseProvider.Sqlite;

    public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var pragma = connection.CreateCommand())
        {
            // synchronous=NORMAL is safe with WAL and keeps per-statement commits cheap.
            pragma.CommandText = "PRAGMA foreign_keys = ON; PRAGMA synchronous = NORMAL;";
            await pragma.ExecuteNonQueryAsync(cancellationToken);
        }

        return connection;
    }

    public void Dispose() => _sentinel?.Dispose();
}
