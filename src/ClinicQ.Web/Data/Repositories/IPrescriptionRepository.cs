using ClinicQ.Domain.Entities;

namespace ClinicQ.Web.Data.Repositories;

public interface IPrescriptionRepository
{
    Task<Prescription?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Prescription>> ListByPatientAsync(int patientId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Prescription>> ListByAppointmentAsync(int appointmentId, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(Prescription prescription, CancellationToken cancellationToken = default);
}
