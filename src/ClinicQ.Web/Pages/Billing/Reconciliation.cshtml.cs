using ClinicQ.Domain.Entities;
using ClinicQ.Web.Data.Repositories;
using ClinicQ.Web.Infrastructure;
using ClinicQ.Web.Jobs;
using ClinicQ.Web.Ui;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicQ.Web.Pages.Billing;

/// <summary>History of nightly billing reconciliation runs with an on-demand trigger.</summary>
[Authorize(Roles = $"{Roles.Admin},{Roles.Billing}")]
public sealed class ReconciliationModel : PageModel
{
    private readonly IBillingReconciliationRepository _runs;
    private readonly IBranchRepository _branches;
    private readonly BillingReconciliationJob _job;
    private readonly IBackgroundJobClient _jobs;
    private readonly IClock _clock;

    public ReconciliationModel(IBillingReconciliationRepository runs, IBranchRepository branches, BillingReconciliationJob job, IBackgroundJobClient jobs, IClock clock)
    {
        _runs = runs;
        _branches = branches;
        _job = job;
        _jobs = jobs;
        _clock = clock;
    }

    public IReadOnlyList<ReconciliationRun> Runs { get; private set; } = Array.Empty<ReconciliationRun>();
    public IReadOnlyDictionary<int, string> BranchNames { get; private set; } = new Dictionary<int, string>();

    [BindProperty]
    public DateOnly Day { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Day = _clock.Today.AddDays(-1);
        BranchNames = (await _branches.GetAllAsync(cancellationToken)).ToDictionary(b => b.Id, b => b.Name);
        Runs = await _runs.ListRunsAsync(null, 200, cancellationToken);
    }

    /// <summary>Reconcile one day synchronously so the result is visible immediately.</summary>
    public async Task<IActionResult> OnPostRunAsync(CancellationToken cancellationToken)
    {
        var start = Day.ToDateTime(TimeOnly.MinValue);
        var runs = await _job.RunForPeriodAsync(start, start.AddDays(1), cancellationToken);
        this.Flash($"Reconciled {runs.Count} branch(es) for {Day:d MMM yyyy}; {runs.Sum(r => r.UnbilledCompletedAppointments)} unbilled visit(s).");
        return RedirectToPage();
    }

    /// <summary>Queue the recurring Hangfire job to run now in the background.</summary>
    public IActionResult OnPostEnqueue()
    {
        var jobId = _jobs.Enqueue<BillingReconciliationJob>(j => j.RunAsync(CancellationToken.None));
        this.Flash($"Nightly reconciliation queued as Hangfire job {jobId}.");
        return RedirectToPage();
    }
}
