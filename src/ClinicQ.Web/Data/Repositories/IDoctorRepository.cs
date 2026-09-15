using ClinicQ.Domain.Entities;

namespace ClinicQ.Web.Data.Repositories;

public interface IDoctorRepository
{
    Task<IReadOnlyList<Doctor>> GetAllAsync(int? branchId = null, CancellationToken cancellationToken = default);
    Task<Doctor?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(Doctor doctor, CancellationToken cancellationToken = default);
}
