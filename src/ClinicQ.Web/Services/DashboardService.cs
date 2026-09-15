using ClinicQ.Domain.Appointments;
using ClinicQ.Domain.Scheduling;
using ClinicQ.Web.Data.Repositories;

namespace ClinicQ.Web.Services;

public sealed record DoctorUtilization(int DoctorId, string DoctorName, string Specialty, string BranchName, int Scheduled, int Completed, int NoShows, int Capacity, double UtilizationPercent);

public sealed record BranchWaitTime(int BranchId, string BranchName, int Samples, double AverageWaitMinutes);

public sealed record BranchRevenue(int BranchId, string BranchName, int InvoiceCount, decimal TotalInvoiced, decimal TotalCollected, decimal Outstanding);

public sealed record DashboardMetrics(
    DateTime From,
    DateTime To,
    IReadOnlyList<DoctorUtilization> DoctorUtilization,
    IReadOnlyList<BranchWaitTime> WaitTimes,
    double OverallAverageWaitMinutes,
    IReadOnlyList<BranchRevenue> Revenue,
    IReadOnlyDictionary<AppointmentStatus, int> StatusCounts);

/// <summary>
/// Aggregates dashboard KPIs: doctor utilization against slot capacity, average patient wait time and
/// revenue per branch for a date range.
/// </summary>
public sealed class DashboardService
{
    private readonly IDashboardRepository _dashboard;
    private readonly IBranchRepository _branches;
    private readonly SlotGenerator _slots;

    public DashboardService(IDashboardRepository dashboard, IBranchRepository branches, SlotGenerator slots)
    {
        _dashboard = dashboard;
        _branches = branches;
        _slots = slots;
    }

    public async Task<DashboardMetrics> GetMetricsAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var branches = await _branches.GetAllAsync(cancellationToken);
        var branchNames = branches.ToDictionary(b => b.Id, b => b.Name);
        var capacityPerBranch = new Dictionary<int, int>();
        foreach (var branch in branches)
        {
            var rule = await _branches.GetSlotRuleAsync(branch.Id, cancellationToken);
            capacityPerBranch[branch.Id] = rule is null ? 0 : CapacityFor(rule, from, to);
        }

        var utilizationRows = await _dashboard.GetDoctorUtilizationAsync(from, to, cancellationToken);
        var utilization = utilizationRows.Select(r =>
        {
            var capacity = capacityPerBranch.GetValueOrDefault(r.BranchId);
            var percent = capacity == 0 ? 0 : Math.Round(100.0 * r.Scheduled / capacity, 1);
            return new DoctorUtilization(r.DoctorId, r.DoctorName, r.Specialty, r.BranchName, r.Scheduled, r.Completed, r.NoShows, capacity, percent);
        }).ToList();

        var waitRows = await _dashboard.GetWaitTimesAsync(from, to, cancellationToken);
        var waits = waitRows
            .GroupBy(w => w.BranchId)
            .Select(g => new BranchWaitTime(
                g.Key,
                branchNames.GetValueOrDefault(g.Key, $"Branch {g.Key}"),
                g.Count(),
                Math.Round(g.Average(w => (w.ConsultationStartedAt - w.CheckedInAt).TotalMinutes), 1)))
            .OrderBy(w => w.BranchName)
            .ToList();
        var overallWait = waitRows.Count == 0 ? 0 : Math.Round(waitRows.Average(w => (w.ConsultationStartedAt - w.CheckedInAt).TotalMinutes), 1);

        var revenueRows = await _dashboard.GetBranchRevenueAsync(from, to, cancellationToken);
        var revenue = revenueRows
            .Select(r => new BranchRevenue(r.BranchId, r.BranchName, r.InvoiceCount, r.TotalInvoiced, r.TotalCollected, r.TotalInvoiced - r.TotalCollected))
            .ToList();

        var statusRows = await _dashboard.GetStatusCountsAsync(from, to, cancellationToken);
        var statuses = Enum.GetValues<AppointmentStatus>().ToDictionary(s => s, _ => 0);
        foreach (var row in statusRows)
        {
            statuses[row.Status] = row.Count;
        }

        return new DashboardMetrics(from, to, utilization, waits, overallWait, revenue, statuses);
    }

    /// <summary>
    /// Schedulable slots for one doctor at a branch across the period. Overbooking capacity
    /// (MaxBookingsPerSlot &gt; 1) is deliberately excluded, so an overbooked doctor can exceed 100%.
    /// </summary>
    public int CapacityFor(BranchSlotRule rule, DateTime from, DateTime to)
    {
        var capacity = 0;
        var noBookings = new Dictionary<DateTime, int>();
        for (var day = DateOnly.FromDateTime(from); day < DateOnly.FromDateTime(to); day = day.AddDays(1))
        {
            // Evaluate each day as if it were "yesterday" so lead-time and advance-booking limits do not trim the grid.
            var reference = day.AddDays(-1).ToDateTime(TimeOnly.MinValue);
            capacity += _slots.Generate(rule, day, noBookings, reference).Count;
        }

        return capacity;
    }
}
