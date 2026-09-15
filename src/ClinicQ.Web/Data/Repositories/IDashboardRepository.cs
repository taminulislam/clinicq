using ClinicQ.Web.Data.Models;

namespace ClinicQ.Web.Data.Repositories;

public interface IDashboardRepository
{
    Task<IReadOnlyList<DoctorUtilizationRow>> GetDoctorUtilizationAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WaitTimeRow>> GetWaitTimesAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BranchRevenueRow>> GetBranchRevenueAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StatusCountRow>> GetStatusCountsAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
}
