using ClinicQ.Domain.Entities;

namespace ClinicQ.Web.Data.Repositories;

/// <summary>
/// Nightly billing reconciliation. SQL Server calls dbo.usp_ReconcileBilling; SQLite runs equivalent inline SQL.
/// </summary>
public interface IBillingReconciliationRepository
{
    Task<ReconciliationRun> ReconcileAsync(int branchId, DateTime periodStart, DateTime periodEnd, DateTime runAt, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReconciliationRun>> ListRunsAsync(int? branchId = null, int limit = 50, CancellationToken cancellationToken = default);
}
