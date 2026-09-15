using ClinicQ.Domain.Entities;

namespace ClinicQ.Web.Data.Repositories;

public interface IPatientRepository
{
    Task<IReadOnlyList<Patient>> SearchAsync(string? search, int offset = 0, int limit = 100, CancellationToken cancellationToken = default);
    Task<int> CountAsync(string? search = null, CancellationToken cancellationToken = default);
    Task<Patient?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Patient?> GetByMrnAsync(string mrn, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(Patient patient, CancellationToken cancellationToken = default);
    Task UpdateAsync(Patient patient, CancellationToken cancellationToken = default);
    Task<string> NextMrnAsync(CancellationToken cancellationToken = default);
}
