using System.Data.Common;

namespace ClinicQ.Web.Data;

/// <summary>
/// Creates opened ADO.NET connections for the configured provider. Repositories use Dapper on top of this.
/// </summary>
public interface IDbConnectionFactory
{
    DatabaseProvider Provider { get; }

    Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default);
}
