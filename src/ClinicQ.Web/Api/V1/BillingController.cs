using ClinicQ.Domain.Entities;
using ClinicQ.Web.Api.Contracts;
using ClinicQ.Web.Data.Repositories;
using ClinicQ.Web.Infrastructure;
using ClinicQ.Web.Jobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicQ.Web.Api.V1;

/// <summary>Billing reconciliation (usp_ReconcileBilling on SQL Server) and its run history.</summary>
[Authorize(Roles = $"{Roles.Admin},{Roles.Billing}")]
public sealed class BillingController : ApiControllerBase
{
    private readonly BillingReconciliationJob _job;
    private readonly IBillingReconciliationRepository _runs;
    private readonly IClock _clock;

    public BillingController(BillingReconciliationJob job, IBillingReconciliationRepository runs, IClock clock)
    {
        _job = job;
        _runs = runs;
        _clock = clock;
    }

    /// <summary>Run reconciliation now for every branch. Defaults to the previous calendar day.</summary>
    [HttpPost("reconcile")]
    public async Task<ActionResult<IReadOnlyList<ReconciliationRunResponse>>> Reconcile([FromBody] ReconcileRequest? request, CancellationToken cancellationToken)
    {
        var end = request?.PeriodEnd ?? _clock.Today.ToDateTime(TimeOnly.MinValue);
        var start = request?.PeriodStart ?? end.AddDays(-1);
        var runs = await _job.RunForPeriodAsync(start, end, cancellationToken);
        return Ok(runs.Select(ReconciliationRunResponse.From).ToList());
    }

    [HttpGet("reconciliation-runs")]
    public async Task<ActionResult<IReadOnlyList<ReconciliationRunResponse>>> Runs([FromQuery] int? branchId, [FromQuery] int limit = 50, CancellationToken cancellationToken = default)
        => Ok((await _runs.ListRunsAsync(branchId, Math.Clamp(limit, 1, 200), cancellationToken)).Select(ReconciliationRunResponse.From).ToList());
}
