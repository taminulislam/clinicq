using ClinicQ.Domain.Entities;
using Dapper;

namespace ClinicQ.Web.Data.Repositories;

/// <summary>
/// SQLite implementation of billing reconciliation. Mirrors dbo.usp_ReconcileBilling with inline SQL.
/// </summary>
public sealed class SqliteBillingReconciliationRepository : RepositoryBase, IBillingReconciliationRepository
{
    public SqliteBillingReconciliationRepository(IDbConnectionFactory connections) : base(connections)
    {
    }

    public async Task<ReconciliationRun> ReconcileAsync(int branchId, DateTime periodStart, DateTime periodEnd, DateTime runAt, CancellationToken cancellationToken = default)
    {
        const string totalsSql = """
            SELECT COUNT(*)                 AS InvoiceCount,
                   COALESCE(SUM(Total), 0)      AS TotalInvoiced,
                   COALESCE(SUM(AmountPaid), 0) AS TotalPaid
              FROM Invoices
             WHERE BranchId = @BranchId
               AND Status <> 'Void'
               AND IssuedAt >= @PeriodStart AND IssuedAt < @PeriodEnd
            """;
        const string unbilledSql = """
            SELECT COUNT(*)
              FROM Appointments a
             WHERE a.BranchId = @BranchId
               AND a.Status = 'Completed'
               AND a.CompletedAt >= @PeriodStart AND a.CompletedAt < @PeriodEnd
               AND NOT EXISTS (SELECT 1 FROM Invoices i WHERE i.AppointmentId = a.Id AND i.Status <> 'Void')
            """;
        var insertSql = $"""
            INSERT INTO ReconciliationRuns (BranchId, RunAt, PeriodStart, PeriodEnd, InvoiceCount, TotalInvoiced, TotalPaid, Outstanding, UnbilledCompletedAppointments, Notes)
            VALUES (@BranchId, @RunAt, @PeriodStart, @PeriodEnd, @InvoiceCount, @TotalInvoiced, @TotalPaid, @Outstanding, @UnbilledCompletedAppointments, @Notes);
            {Dialect.SelectInsertedId}
            """;

        var period = new { BranchId = branchId, PeriodStart = periodStart, PeriodEnd = periodEnd };

        await using var db = await Connections.OpenAsync(cancellationToken);
        await using var tx = await db.BeginTransactionAsync(cancellationToken);

        var totals = await db.QuerySingleAsync<TotalsRow>(new CommandDefinition(totalsSql, period, tx, cancellationToken: cancellationToken));
        var unbilled = await db.ExecuteScalarAsync<int>(new CommandDefinition(unbilledSql, period, tx, cancellationToken: cancellationToken));

        var run = new ReconciliationRun
        {
            BranchId = branchId,
            RunAt = runAt,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            InvoiceCount = totals.InvoiceCount,
            TotalInvoiced = totals.TotalInvoiced,
            TotalPaid = totals.TotalPaid,
            Outstanding = totals.TotalInvoiced - totals.TotalPaid,
            UnbilledCompletedAppointments = unbilled,
            Notes = unbilled > 0
                ? $"{unbilled} completed appointment(s) have no invoice."
                : "All completed appointments are billed."
        };

        run.Id = await db.ExecuteScalarAsync<int>(new CommandDefinition(insertSql, run, tx, cancellationToken: cancellationToken));
        await tx.CommitAsync(cancellationToken);
        return run;
    }

    public async Task<IReadOnlyList<ReconciliationRun>> ListRunsAsync(int? branchId = null, int limit = 50, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT * FROM ReconciliationRuns WHERE (@BranchId IS NULL OR BranchId = @BranchId) ORDER BY RunAt DESC {Dialect.Paging}";
        await using var db = await Connections.OpenAsync(cancellationToken);
        var rows = await db.QueryAsync<ReconciliationRun>(new CommandDefinition(sql, new { BranchId = branchId, Offset = 0, Limit = limit }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    private sealed class TotalsRow
    {
        public int InvoiceCount { get; set; }
        public decimal TotalInvoiced { get; set; }
        public decimal TotalPaid { get; set; }
    }
}
