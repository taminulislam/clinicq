using ClinicQ.Web.Infrastructure;
using ClinicQ.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClinicQ.Web.Api.V1;

public sealed class DashboardController : ApiControllerBase
{
    private readonly DashboardService _dashboard;
    private readonly IClock _clock;

    public DashboardController(DashboardService dashboard, IClock clock)
    {
        _dashboard = dashboard;
        _clock = clock;
    }

    /// <summary>Doctor utilization, average wait time and revenue per branch. Defaults to the last 30 days.</summary>
    [HttpGet("metrics")]
    public async Task<ActionResult<DashboardMetrics>> Metrics([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
    {
        var end = (to ?? _clock.Today.AddDays(1).ToDateTime(TimeOnly.MinValue)).Date;
        var start = (from ?? end.AddDays(-30)).Date;
        if (start >= end)
        {
            return BadRequest(new ProblemDetails { Status = 400, Title = "from must be before to" });
        }

        return Ok(await _dashboard.GetMetricsAsync(start, end, cancellationToken));
    }
}
