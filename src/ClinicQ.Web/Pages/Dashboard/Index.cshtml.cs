using ClinicQ.Domain.Appointments;
using ClinicQ.Web.Infrastructure;
using ClinicQ.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicQ.Web.Pages.Dashboard;

/// <summary>Operational dashboard: doctor utilization, average wait time and revenue per branch.</summary>
public sealed class IndexModel : PageModel
{
    private readonly DashboardService _dashboard;
    private readonly IClock _clock;

    public IndexModel(DashboardService dashboard, IClock clock)
    {
        _dashboard = dashboard;
        _clock = clock;
    }

    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }

    public DashboardMetrics Metrics { get; private set; } = null!;

    public int TotalVisits => Metrics.StatusCounts.GetValueOrDefault(AppointmentStatus.Completed);
    public int NoShows => Metrics.StatusCounts.GetValueOrDefault(AppointmentStatus.NoShow);
    public double NoShowRate
    {
        get
        {
            var denominator = TotalVisits + NoShows;
            return denominator == 0 ? 0 : Math.Round(100.0 * NoShows / denominator, 1);
        }
    }

    public decimal Revenue => Metrics.Revenue.Sum(r => r.TotalCollected);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        To ??= _clock.Today;
        From ??= To.Value.AddDays(-29);
        if (From > To)
        {
            (From, To) = (To, From);
        }

        Metrics = await _dashboard.GetMetricsAsync(From.Value.ToDateTime(TimeOnly.MinValue), To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), cancellationToken);
    }
}
