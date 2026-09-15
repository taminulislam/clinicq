using ClinicQ.Domain.Entities;
using ClinicQ.Web.Data.Repositories;
using ClinicQ.Web.Infrastructure;

namespace ClinicQ.Web.Jobs;

/// <summary>
/// Hangfire recurring job: nightly billing reconciliation for every branch covering the previous day.
/// </summary>
public sealed class BillingReconciliationJob
{
    private readonly IBranchRepository _branches;
    private readonly IBillingReconciliationRepository _reconciliation;
    private readonly IClock _clock;
    private readonly ILogger<BillingReconciliationJob> _logger;

    public BillingReconciliationJob(
        IBranchRepository branches,
        IBillingReconciliationRepository reconciliation,
        IClock clock,
        ILogger<BillingReconciliationJob> logger)
    {
        _branches = branches;
        _reconciliation = reconciliation;
        _clock = clock;
        _logger = logger;
    }

    public Task<IReadOnlyList<ReconciliationRun>> RunAsync(CancellationToken cancellationToken = default)
    {
        var periodEnd = _clock.Today.ToDateTime(TimeOnly.MinValue);
        return RunForPeriodAsync(periodEnd.AddDays(-1), periodEnd, cancellationToken);
    }

    public async Task<IReadOnlyList<ReconciliationRun>> RunForPeriodAsync(DateTime periodStart, DateTime periodEnd, CancellationToken cancellationToken = default)
    {
        var runs = new List<ReconciliationRun>();
        foreach (var branch in await _branches.GetAllAsync(cancellationToken))
        {
            var run = await _reconciliation.ReconcileAsync(branch.Id, periodStart, periodEnd, _clock.Now, cancellationToken);
            runs.Add(run);

            if (run.UnbilledCompletedAppointments > 0 || run.Outstanding > 0)
            {
                _logger.LogWarning("Reconciliation {Branch}: {Invoices} invoices, outstanding {Outstanding:C2}, {Unbilled} unbilled visits",
                    branch.Name, run.InvoiceCount, run.Outstanding, run.UnbilledCompletedAppointments);
            }
            else
            {
                _logger.LogInformation("Reconciliation {Branch}: balanced ({Invoices} invoices, {Total:C2})", branch.Name, run.InvoiceCount, run.TotalInvoiced);
            }
        }

        return runs;
    }
}
