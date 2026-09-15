using System.Data;
using ClinicQ.Domain.Entities;
using Dapper;

namespace ClinicQ.Web.Data.Repositories;

/// <summary>
/// SQL Server implementation backed by dbo.usp_ReconcileBilling (db/003_procs.sql).
/// </summary>
public sealed class SqlServerBillingReconciliationRepository : RepositoryBase, IBillingReconciliationRepository
{
    public SqlServerBillingReconciliationRepository(IDbConnectionFactory connections) : base(connections)
    {
    }

    public async Task<ReconciliationRun> ReconcileAsync(int branchId, DateTime periodStart, DateTime periodEnd, DateTime runAt, CancellationToken cancellationToken = default)
    {
        var args = new DynamicParameters();
        args.Add("@BranchId", branchId, DbType.Int32);
        args.Add("@PeriodStart", periodStart, DbType.DateTime2);
        args.Add("@PeriodEnd", periodEnd, DbType.DateTime2);
        args.Add("@RunAt", runAt, DbType.DateTime2);

        await using var db = await Connections.OpenAsync(cancellationToken);
        return await db.QuerySingleAsync<ReconciliationRun>(new CommandDefinition("dbo.usp_ReconcileBilling", args, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ReconciliationRun>> ListRunsAsync(int? branchId = null, int limit = 50, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT * FROM ReconciliationRuns WHERE (@BranchId IS NULL OR BranchId = @BranchId) ORDER BY RunAt DESC {Dialect.Paging}";
        await using var db = await Connections.OpenAsync(cancellationToken);
        var rows = await db.QueryAsync<ReconciliationRun>(new CommandDefinition(sql, new { BranchId = branchId, Offset = 0, Limit = limit }, cancellationToken: cancellationToken));
        return rows.ToList();
    }
}
