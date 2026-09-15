using ClinicQ.Web.Data;
using Dapper;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ClinicQ.Web.Infrastructure;

/// <summary>Verifies the configured database answers a trivial query.</summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly IDbConnectionFactory _connections;

    public DatabaseHealthCheck(IDbConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await _connections.OpenAsync(cancellationToken);
            var one = await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT 1", cancellationToken: cancellationToken));
            return one == 1
                ? HealthCheckResult.Healthy($"{_connections.Provider} reachable")
                : HealthCheckResult.Degraded("Unexpected probe result");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"{_connections.Provider} unreachable", ex);
        }
    }
}
