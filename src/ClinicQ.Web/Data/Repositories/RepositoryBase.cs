namespace ClinicQ.Web.Data.Repositories;

/// <summary>
/// Shared plumbing for Dapper repositories: connection factory plus the provider dialect.
/// </summary>
public abstract class RepositoryBase
{
    protected RepositoryBase(IDbConnectionFactory connections)
    {
        Connections = connections ?? throw new ArgumentNullException(nameof(connections));
        Dialect = SqlDialectFactory.For(connections.Provider);
    }

    protected IDbConnectionFactory Connections { get; }

    protected ISqlDialect Dialect { get; }
}
