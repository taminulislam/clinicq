using ClinicQ.Domain.Entities;

namespace ClinicQ.Web.Data.Repositories;

public interface ILabReportRepository
{
    Task<LabReport?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LabReport>> ListByPatientAsync(int patientId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LabReport>> ListRecentAsync(int limit = 50, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(LabReport report, CancellationToken cancellationToken = default);
}
