using System.Reflection;
using Dapper;

namespace ClinicQ.Web.Data;

/// <summary>
/// Prepares the database at startup. For SQLite the embedded schema script is applied (idempotent);
/// for SQL Server the schema is expected to have been deployed from db/*.sql and is only verified.
/// </summary>
public sealed class DatabaseInitializer
{
    private const string SqliteSchemaResource = "ClinicQ.Web.Data.Scripts.sqlite_schema.sql";

    private readonly IDbConnectionFactory _connections;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(IDbConnectionFactory connections, ILogger<DatabaseInitializer> logger)
    {
        _connections = connections;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _connections.OpenAsync(cancellationToken);

        if (_connections.Provider == DatabaseProvider.Sqlite)
        {
            var script = await ReadEmbeddedScriptAsync(SqliteSchemaResource, cancellationToken);
            await db.ExecuteAsync(new CommandDefinition(script, cancellationToken: cancellationToken));
            _logger.LogInformation("SQLite schema ensured");
            return;
        }

        const string check = "SELECT COUNT(*) FROM sys.tables WHERE name IN ('Branches', 'Appointments', 'Invoices')";
        var tables = await db.ExecuteScalarAsync<int>(new CommandDefinition(check, cancellationToken: cancellationToken));
        if (tables < 3)
        {
            throw new InvalidOperationException(
                "SQL Server schema not found. Deploy db/001_schema.sql, db/002_seed.sql and db/003_procs.sql before starting the application.");
        }

        _logger.LogInformation("SQL Server schema verified");
    }

    private static async Task<string> ReadEmbeddedScriptAsync(string resourceName, CancellationToken cancellationToken)
    {
        var assembly = Assembly.GetExecutingAssembly();
        await using var stream = assembly.GetManifestResourceStream(resourceName)
                                 ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
