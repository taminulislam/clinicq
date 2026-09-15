using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace ClinicQ.Web.Data;

/// <summary>
/// SQL Server / Azure SQL connection factory. Expects the schema from db/*.sql to already exist.
/// </summary>
public sealed class SqlServerConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlServerConnectionFactory(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public DatabaseProvider Provider => DatabaseProvider.SqlServer;

    public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
